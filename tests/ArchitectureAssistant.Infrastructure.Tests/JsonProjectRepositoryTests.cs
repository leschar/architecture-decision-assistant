using ArchitectureAssistant.Domain.Projects;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArchitectureAssistant.Infrastructure.Tests;

public sealed class JsonProjectRepositoryTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(),
        "ArchitectureDecisionAssistant.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly JsonProjectRepository _repository;

    public JsonProjectRepositoryTests()
    {
        _repository = new JsonProjectRepository(_workspace, NullLogger<JsonProjectRepository>.Instance);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsProjectAsValidatedJson()
    {
        var created = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing platform",
            DateTimeOffset.UtcNow);
        Assert.True(created.TryGetValue(out var project));

        Assert.True((await _repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);

        var context = project.Context.Update(
            "Retail",
            8,
            ProjectCriticality.High,
            ["No external database"],
            ScalabilityTarget.High);
        Assert.True(context.TryGetValue(out var updatedContext));
        var projectWithContext = project.UpdateContext(updatedContext, DateTimeOffset.UtcNow);
        Assert.True(projectWithContext.TryGetValue(out var revisedProject));
        Assert.True((await _repository.SaveAsync(revisedProject, project.Revision, CancellationToken.None)).IsSuccess);

        var requirement = Requirement.Create(
            Guid.NewGuid(),
            "The application must be available during business hours.",
            RequirementClassification.Quality,
            RequirementPriority.Must,
            RequirementStatus.Confirmed,
            DateTimeOffset.UtcNow);
        Assert.True(requirement.TryGetValue(out var value));
        var projectWithRequirement = revisedProject.AddRequirement(value, DateTimeOffset.UtcNow);
        Assert.True(projectWithRequirement.TryGetValue(out var finalProject));
        Assert.True((await _repository.SaveAsync(finalProject, revisedProject.Revision, CancellationToken.None)).IsSuccess);

        var loaded = await _repository.LoadAsync(project.Id, CancellationToken.None);
        Assert.True(loaded.TryGetValue(out var result));
        Assert.Equal(project.Id, result.Id);
        Assert.Equal(3, result.Revision);
        Assert.Equal(project.Name, result.Name);
        Assert.Equal(8, result.Context.TeamSize);
        Assert.Equal(ProjectCriticality.High, result.Context.Criticality);
        Assert.Equal(ScalabilityTarget.High, result.Context.ScalabilityTarget);
        Assert.Equal("No external database", Assert.Single(result.Context.Constraints));
        Assert.Equal("The application must be available during business hours.", Assert.Single(result.Requirements).Description);
    }

    [Fact]
    public async Task Save_RejectsStaleRevisionWithoutOverwritingNewerProject()
    {
        var created = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing platform",
            DateTimeOffset.UtcNow);
        Assert.True(created.TryGetValue(out var original));
        Assert.True((await _repository.SaveAsync(original, null, CancellationToken.None)).IsSuccess);

        var firstRequirement = Requirement.Create(
            Guid.NewGuid(),
            "Keep an audit trail.",
            RequirementClassification.Quality,
            null,
            RequirementStatus.Confirmed,
            DateTimeOffset.UtcNow);
        Assert.True(firstRequirement.TryGetValue(out var first));
        var firstRevision = original.AddRequirement(first, DateTimeOffset.UtcNow);
        Assert.True(firstRevision.TryGetValue(out var revised));
        Assert.True((await _repository.SaveAsync(revised, original.Revision, CancellationToken.None)).IsSuccess);

        var staleRequirement = Requirement.Create(
            Guid.NewGuid(),
            "Support accessibility.",
            RequirementClassification.Quality,
            null,
            RequirementStatus.Confirmed,
            DateTimeOffset.UtcNow);
        Assert.True(staleRequirement.TryGetValue(out var stale));
        var staleRevision = original.AddRequirement(stale, DateTimeOffset.UtcNow);
        Assert.True(staleRevision.TryGetValue(out var staleProject));

        var result = await _repository.SaveAsync(staleProject, original.Revision, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "project.conflict.revision");
        var persisted = await _repository.LoadAsync(original.Id, CancellationToken.None);
        Assert.True(persisted.TryGetValue(out var value));
        Assert.Equal("Keep an audit trail.", Assert.Single(value.Requirements).Description);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesDecisionTransitionHistory()
    {
        var created = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing platform",
            DateTimeOffset.UtcNow);
        Assert.True(created.TryGetValue(out var project));
        var decisionResult = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            "architecture-a",
            "Architecture",
            ["architecture-b"],
            DecisionStatus.Proposed,
            "Selected after review.",
            [],
            "architect",
            DateTimeOffset.UtcNow);
        Assert.True(decisionResult.TryGetValue(out var decision));
        var added = project.AddDecision(decision, DateTimeOffset.UtcNow);
        Assert.True(added.TryGetValue(out var withDecision));
        var accepted = withDecision.TransitionDecision(
            decision.Id,
            DecisionStatus.Accepted,
            "reviewer",
            DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(accepted.TryGetValue(out var revised));
        Assert.True((await _repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);
        Assert.True((await _repository.SaveAsync(withDecision, project.Revision, CancellationToken.None)).IsSuccess);
        Assert.True((await _repository.SaveAsync(revised, withDecision.Revision, CancellationToken.None)).IsSuccess);

        var loaded = await _repository.LoadAsync(project.Id, CancellationToken.None);

        Assert.True(loaded.TryGetValue(out var restored));
        var restoredDecision = Assert.Single(restored.Decisions);
        Assert.Equal(DecisionStatus.Accepted, restoredDecision.Status);
        Assert.Equal(2, restoredDecision.Revision);
        Assert.Equal("reviewer", restoredDecision.StatusHistory[0].ChangedBy);
        Assert.Equal(DecisionStatus.Accepted, restoredDecision.StatusHistory[0].Status);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesArchitecturalStructure()
    {
        var created = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing platform",
            DateTimeOffset.UtcNow);
        Assert.True(created.TryGetValue(out var project));
        var system = new StructuralElement(
            "orders-system", "Orders", StructuralElementType.SoftwareSystem, null,
            null, null, null, StructuralElementStatus.Confirmed,
            new StructuralProvenance(StructuralSourceKind.Manual, null, null));
        var container = new StructuralElement(
            "orders-api", "Orders API", StructuralElementType.Container, system.Id,
            null, null, "ASP.NET Core", StructuralElementStatus.Proposed,
            new StructuralProvenance(StructuralSourceKind.Manual, null, null));
        var structure = ArchitecturalStructure.Create([system, container], []);
        Assert.True(structure.TryGetValue(out var validStructure));
        var updated = project.UpdateStructure(validStructure, DateTimeOffset.UtcNow);
        Assert.True(updated.TryGetValue(out var revisedProject));
        Assert.True((await _repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);
        Assert.True((await _repository.SaveAsync(revisedProject, project.Revision, CancellationToken.None)).IsSuccess);

        var loaded = await _repository.LoadAsync(project.Id, CancellationToken.None);

        Assert.True(loaded.TryGetValue(out var restored));
        Assert.Equal(2, restored.Revision);
        Assert.Equal("ASP.NET Core", Assert.Single(restored.Structure.Elements, element => element.Type == StructuralElementType.Container).Technology);
    }

    [Fact]
    public async Task List_ReportsInvalidProjectInsteadOfReturningPartialResults()
    {
        var created = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing platform",
            DateTimeOffset.UtcNow);
        Assert.True(created.TryGetValue(out var project));
        Assert.True((await _repository.SaveAsync(project, null, CancellationToken.None)).IsSuccess);

        var invalidId = Guid.NewGuid();
        var projectDirectory = Path.Combine(_workspace, "projects");
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, $"{invalidId:N}.json"),
            """{"schemaVersion":"99.0"}""");

        var result = await _repository.ListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "project.schema.invalid");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }
}
