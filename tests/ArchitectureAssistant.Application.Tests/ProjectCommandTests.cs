using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Analysis;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Application.Projects;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Application.Tests;

public sealed class ProjectCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateProjectHandler_ValidatesAndPersistsProject()
    {
        var repository = new InMemoryProjectRepository();
        var handler = new CreateProjectHandler(repository, new CreateProjectValidator(), new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateProjectCommand("Orders", "Order processing platform"),
            CancellationToken.None);

        Assert.True(result.TryGetValue(out var project));
        Assert.Equal("Orders", project.Name);
        Assert.Equal(1, project.Revision);
        Assert.Single(repository.Projects);
    }

    [Fact]
    public async Task CreateProjectHandler_InvalidInputDoesNotPersistProject()
    {
        var repository = new InMemoryProjectRepository();
        var handler = new CreateProjectHandler(repository, new CreateProjectValidator(), new FixedTimeProvider(Now));

        var result = await handler.Handle(new CreateProjectCommand("", ""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(repository.Projects);
    }

    [Fact]
    public async Task AddRequirementHandler_PersistsConfirmedRequirementAndNewRevision()
    {
        var repository = new InMemoryProjectRepository();
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing platform", Now);
        Assert.True(created.TryGetValue(out var project));
        Assert.True((await repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);

        var handler = new AddRequirementHandler(repository, new AddRequirementValidator(), new FixedTimeProvider(Now.AddMinutes(1)));
        var result = await handler.Handle(
            new AddRequirementCommand(
                project.Id,
                "The system must support concurrent users.",
                RequirementClassification.Quality,
                RequirementPriority.Must),
            CancellationToken.None);

        Assert.True(result.TryGetValue(out var view));
        Assert.Equal(2, view.Revision);
        Assert.Single(view.Requirements);
        Assert.Equal(RequirementStatus.Confirmed, view.Requirements[0].Status);
        Assert.Equal(2, repository.Projects.Single().Revision);
    }

    [Fact]
    public async Task DecisionHandlers_RecordReviseAndTransitionExplicitly()
    {
        var repository = new InMemoryProjectRepository();
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing platform", Now);
        Assert.True(created.TryGetValue(out var project));
        Assert.True((await repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);
        var analyses = new InMemoryAnalysisRepository();
        var catalog = CreateCatalog();
        var timeProvider = new FixedTimeProvider(Now);

        var recordHandler = new RecordArchitecturalDecisionHandler(
            repository,
            analyses,
            catalog,
            new RecordArchitecturalDecisionValidator(),
            timeProvider);
        var recorded = await recordHandler.Handle(
            new RecordArchitecturalDecisionCommand(
                project.Id,
                "architecture-a",
                "Architecture",
                ["architecture-b"],
                "Chosen after review.",
                ["Requires an operational team."],
                "architect"),
            CancellationToken.None);

        Assert.True(recorded.TryGetValue(out var proposed));
        var originalDecision = Assert.Single(proposed.Decisions);
        Assert.Equal(DecisionStatus.Proposed, originalDecision.Status);
        Assert.Null(originalDecision.AnalysisId);

        var revised = await recordHandler.Handle(
            new RecordArchitecturalDecisionCommand(
                project.Id,
                "architecture-b",
                "Architecture",
                ["architecture-a"],
                "Revised after further review.",
                [],
                "architect",
                RevisesDecisionId: originalDecision.Id),
            CancellationToken.None);

        Assert.True(revised.TryGetValue(out var revisedProject));
        Assert.Equal(DecisionStatus.Superseded, revisedProject.Decisions[0].Status);
        Assert.Equal(revisedProject.Decisions[1].Id, revisedProject.Decisions[0].SupersededByDecisionId);
        Assert.Equal(originalDecision.Id, revisedProject.Decisions[1].SupersedesDecisionId);

        var transitionHandler = new TransitionArchitecturalDecisionHandler(
            repository,
            new TransitionArchitecturalDecisionValidator(),
            timeProvider);
        var accepted = await transitionHandler.Handle(
            new TransitionArchitecturalDecisionCommand(
                project.Id,
                revisedProject.Decisions[1].Id,
                DecisionStatus.Accepted,
                "reviewer"),
            CancellationToken.None);

        Assert.True(accepted.TryGetValue(out var acceptedProject));
        Assert.Equal(DecisionStatus.Accepted, acceptedProject.Decisions[1].Status);
        Assert.Equal(DecisionStatus.Superseded, acceptedProject.Decisions[0].Status);
    }

    [Fact]
    public async Task RecordDecisionHandler_RejectsAlternativesOfDifferentKinds()
    {
        var repository = new InMemoryProjectRepository();
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing platform", Now);
        Assert.True(created.TryGetValue(out var project));
        Assert.True((await repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);
        var handler = new RecordArchitecturalDecisionHandler(
            repository,
            new InMemoryAnalysisRepository(),
            CreateCatalog(),
            new RecordArchitecturalDecisionValidator(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RecordArchitecturalDecisionCommand(
                project.Id,
                "architecture-a",
                "Architecture",
                ["technology-c"],
                "Chosen after review.",
                [],
                "architect"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "decision.alternatives.invalid");
        Assert.Empty(repository.Projects.Single().Decisions);
    }

    private static InMemoryKnowledgeCatalog CreateCatalog() =>
        new([
            CreateKnowledgeItem("architecture-a", "Architecture"),
            CreateKnowledgeItem("architecture-b", "Architecture"),
            CreateKnowledgeItem("technology-c", "Technology")
        ]);

    private static KnowledgeItemView CreateKnowledgeItem(string id, string kind) =>
        new(
            id,
            kind,
            "1.0",
            id,
            "Description",
            null,
            null,
            [],
            [],
            [],
            [],
            [],
            null,
            null,
            null,
            "catalog",
            []);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public List<Project> Projects { get; } = [];

        public Task<Result<IReadOnlyList<Project>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<Project>>.Success(Projects.ToArray()));

        public Task<Result<Project>> LoadAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var project = Projects.SingleOrDefault(candidate => candidate.Id == projectId);
            return Task.FromResult(project is null
                ? Result<Project>.Failure(new DomainError("project.not_found", "Not found."))
                : Result<Project>.Success(project));
        }

        public Task<Result<Project>> SaveAsync(
            Project project,
            long? expectedRevision,
            CancellationToken cancellationToken)
        {
            var index = Projects.FindIndex(candidate => candidate.Id == project.Id);
            if ((expectedRevision is null && index >= 0) ||
                (expectedRevision is not null &&
                 (index < 0 || Projects[index].Revision != expectedRevision.Value)))
            {
                return Task.FromResult(Result<Project>.Failure(
                    new DomainError("project.conflict.revision", "Conflict.")));
            }

            if (index >= 0)
            {
                Projects[index] = project;
            }
            else
            {
                Projects.Add(project);
            }

            return Task.FromResult(Result<Project>.Success(project));
        }

    }

    private sealed class InMemoryKnowledgeCatalog(IReadOnlyList<KnowledgeItemView> items) : IKnowledgeCatalog
    {
        public Task<Result<KnowledgeCatalogSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<KnowledgeCatalogSnapshot>.Success(
                new KnowledgeCatalogSnapshot("1.0", "catalog-hash", items, [])));
    }

    private sealed class InMemoryAnalysisRepository : IAnalysisRepository
    {
        public Task<Result<IReadOnlyList<AnalysisSnapshot>>> ListByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<AnalysisSnapshot>>.Success([]));

        public Task<Result<AnalysisSnapshot>> SaveAsync(
            AnalysisSnapshot snapshot,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<AnalysisSnapshot>.Success(snapshot));
    }
}
