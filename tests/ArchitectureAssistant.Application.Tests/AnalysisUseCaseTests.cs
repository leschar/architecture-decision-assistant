using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Analysis;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Application.Tests;

public sealed class AnalysisUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetProjectAnalyses_MarksSnapshotsOutdatedByCatalogHashProjectHashAndRevision()
    {
        var id = Guid.Parse("8c98c39b-7422-4c0b-aa42-bc15b6474eb4");
        var created = Project.Create(id, "Orders", "Order processing platform", Now);
        Assert.True(created.TryGetValue(out var project));
        var projectRepository = new InMemoryProjectRepository(project);
        var catalog = new InMemoryKnowledgeCatalog("catalog-a");
        var analyses = new InMemoryAnalysisRepository();
        var analyze = new AnalyzeProjectHandler(
            projectRepository,
            catalog,
            analyses,
            new BasicAnalysisEngine(),
            new KnowledgeGraphProjector(),
            new AnalyzeProjectValidator(),
            new FixedTimeProvider(Now));
        var getHistory = new GetProjectAnalysesHandler(
            projectRepository,
            catalog,
            analyses,
            new GetProjectAnalysesValidator());

        var analyzed = await analyze.Handle(new AnalyzeProjectCommand(id), CancellationToken.None);
        Assert.True(analyzed.TryGetValue(out var analyzedSnapshot));
        Assert.NotNull(analyzedSnapshot.GraphProjection);
        Assert.False(Assert.Single((await getHistory.Handle(
            new GetProjectAnalysesQuery(id),
            CancellationToken.None)).Value!).IsOutdated);

        catalog.ContentHash = "catalog-b";
        var afterCatalogChange = await getHistory.Handle(new GetProjectAnalysesQuery(id), CancellationToken.None);
        var catalogStale = Assert.Single(afterCatalogChange.Value!);
        Assert.True(catalogStale.IsOutdated);
        Assert.Contains(AnalysisStalenessReason.CatalogChanged, catalogStale.StalenessReasons);

        Assert.True((await analyze.Handle(new AnalyzeProjectCommand(id), CancellationToken.None)).IsSuccess);
        var afterReanalysis = await getHistory.Handle(new GetProjectAnalysesQuery(id), CancellationToken.None);
        Assert.Contains(afterReanalysis.Value!, item => !item.IsOutdated);

        var changedContext = project.Context.Update(null, 5, null, []);
        Assert.True(changedContext.TryGetValue(out var context));
        var sameRevisionDifferentContent = Project.Restore(
            project.Id,
            project.Revision,
            project.Name,
            project.Purpose,
            project.CreatedAt,
            project.UpdatedAt,
            context,
            project.Requirements,
            project.Decisions);
        Assert.True(sameRevisionDifferentContent.TryGetValue(out var changedProject));
        projectRepository.Project = changedProject;
        var afterProjectHashChange = await getHistory.Handle(new GetProjectAnalysesQuery(id), CancellationToken.None);
        Assert.All(afterProjectHashChange.Value!, item => Assert.True(item.IsOutdated));
        Assert.All(afterProjectHashChange.Value!, item =>
            Assert.Contains(AnalysisStalenessReason.ProjectContentChanged, item.StalenessReasons));

        Assert.True((await analyze.Handle(new AnalyzeProjectCommand(id), CancellationToken.None)).IsSuccess);
        var newerContext = changedProject.Context.Update(null, 6, null, []);
        Assert.True(newerContext.TryGetValue(out var revisedContext));
        var revised = changedProject.UpdateContext(revisedContext, Now.AddMinutes(1));
        Assert.True(revised.TryGetValue(out var revisedProject));
        projectRepository.Project = revisedProject;
        var afterRevisionChange = await getHistory.Handle(new GetProjectAnalysesQuery(id), CancellationToken.None);
        Assert.All(afterRevisionChange.Value!, item => Assert.True(item.IsOutdated));
        Assert.All(afterRevisionChange.Value!, item =>
            Assert.Contains(AnalysisStalenessReason.ProjectRevisionChanged, item.StalenessReasons));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryProjectRepository(Project project) : IProjectRepository
    {
        public Project Project { get; set; } = project;

        public Task<Result<IReadOnlyList<Project>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<Project>>.Success([Project]));

        public Task<Result<Project>> LoadAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(projectId == Project.Id
                ? Result<Project>.Success(Project)
                : Result<Project>.Failure(new DomainError("project.not_found", "Not found.")));

        public Task<Result<Project>> SaveAsync(
            Project updatedProject,
            long? expectedRevision,
            CancellationToken cancellationToken)
        {
            Project = updatedProject;
            return Task.FromResult(Result<Project>.Success(updatedProject));
        }
    }

    private sealed class InMemoryKnowledgeCatalog(string contentHash) : IKnowledgeCatalog
    {
        public string ContentHash { get; set; } = contentHash;

        public Task<Result<KnowledgeCatalogSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<KnowledgeCatalogSnapshot>.Success(
                new KnowledgeCatalogSnapshot("1.0", ContentHash, [], [])));
    }

    private sealed class InMemoryAnalysisRepository : IAnalysisRepository
    {
        private readonly List<AnalysisSnapshot> _snapshots = [];

        public Task<Result<AnalysisSnapshot>> SaveAsync(
            AnalysisSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            _snapshots.Add(snapshot);
            return Task.FromResult(Result<AnalysisSnapshot>.Success(snapshot));
        }

        public Task<Result<IReadOnlyList<AnalysisSnapshot>>> ListByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<AnalysisSnapshot>>.Success(
                _snapshots.Where(snapshot => snapshot.ProjectId == projectId).ToArray()));
    }
}
