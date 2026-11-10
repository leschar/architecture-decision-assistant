using ArchitectureAssistant.Domain.Diagrams;
using ArchitectureAssistant.Domain.Projects;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArchitectureAssistant.Infrastructure.Tests;

public sealed class JsonDiagramRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(),
        "ArchitectureDecisionAssistant.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly JsonDiagramRepository _repository;

    public JsonDiagramRepositoryTests()
    {
        _repository = new JsonDiagramRepository(_workspace, NullLogger<JsonDiagramRepository>.Instance);
    }

    [Fact]
    public async Task SaveAsync_ValidatesAndPersistsAnIdempotentJsonSnapshot()
    {
        var created = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing",
            DateTimeOffset.UtcNow);
        Assert.True(created.TryGetValue(out var project));
        var element = new StructuralElement(
            "orders",
            "Orders",
            StructuralElementType.SoftwareSystem,
            null,
            "Order processing system.",
            null,
            null,
            StructuralElementStatus.Confirmed,
            new StructuralProvenance(StructuralSourceKind.Manual, null, null));
        var structure = ArchitecturalStructure.Create([element], []);
        Assert.True(structure.TryGetValue(out var validStructure));
        var updated = project.UpdateStructure(validStructure, DateTimeOffset.UtcNow);
        Assert.True(updated.TryGetValue(out var withStructure));
        var projection = new ArchitecturalDiagramProjector().Project(
            withStructure,
            ArchitecturalDiagramType.Context,
            element.Id,
            DateTimeOffset.UtcNow);
        Assert.True(projection.TryGetValue(out var diagram));

        var firstSave = await _repository.SaveAsync(diagram, CancellationToken.None);
        var secondSave = await _repository.SaveAsync(diagram with { GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(1) }, CancellationToken.None);

        Assert.True(firstSave.TryGetValue(out var saved));
        Assert.True(secondSave.TryGetValue(out var existing));
        Assert.Equal(diagram.SourceFingerprint, saved.SourceFingerprint);
        Assert.Equal(saved.GeneratedAt, existing.GeneratedAt);
        Assert.Single(Directory.GetFiles(_workspace, "*.json", SearchOption.AllDirectories));

        var listed = await _repository.ListAsync(diagram.ProjectId, CancellationToken.None);
        Assert.True(listed.TryGetValue(out var snapshots));
        var loaded = await _repository.LoadSnapshotAsync(
            diagram.ProjectId,
            diagram.DiagramId,
            CancellationToken.None);
        Assert.True(loaded.TryGetValue(out var loadedSnapshot));
        Assert.Equal(diagram.SourceFingerprint, Assert.Single(snapshots).SourceFingerprint);
        Assert.Equal(diagram.SourceFingerprint, loadedSnapshot.SourceFingerprint);
    }

    [Fact]
    public async Task Layout_IsPersistedSeparatelyAndReportsOrphanedOverrides()
    {
        var project = CreateProjectWithStructure();
        var projector = new ArchitecturalDiagramProjector();
        var projected = projector.Project(project, ArchitecturalDiagramType.Context, "orders", Now);
        Assert.True(projected.TryGetValue(out var diagram));
        var originalLayout = new ArchitecturalDiagramLayout(
            "1.0",
            diagram.DiagramId,
            diagram.SourceFingerprint,
            [new DiagramElementPosition("customer", 12, 24)],
            ["orders"]);

        var savedLayout = await _repository.SaveLayoutAsync(diagram, originalLayout, CancellationToken.None);
        Assert.True(savedLayout.IsSuccess);
        Assert.Single(Directory.GetFiles(
            Path.Combine(_workspace, "diagrams", project.Id.ToString("N"), "layouts"),
            "*.json"));

        var remainingElements = project.Structure.Elements.Where(element => element.Id != "customer");
        var remainingConnections = project.Structure.Connections.Where(connection =>
            connection.SourceId != "customer" && connection.TargetId != "customer");
        var structure = ArchitecturalStructure.Create(remainingElements, remainingConnections);
        Assert.True(structure.TryGetValue(out var revisedStructure));
        var revisedProjectResult = project.UpdateStructure(revisedStructure, Now.AddMinutes(1));
        Assert.True(revisedProjectResult.TryGetValue(out var revisedProject));
        var revisedDiagramResult = projector.Project(
            revisedProject,
            ArchitecturalDiagramType.Context,
            "orders",
            Now.AddMinutes(1));
        Assert.True(revisedDiagramResult.TryGetValue(out var revisedDiagram));

        var loadedLayout = await _repository.LoadLayoutAsync(revisedDiagram, CancellationToken.None);
        Assert.True(loadedLayout.TryGetValue(out var layoutResult));
        Assert.Contains("customer", layoutResult.OrphanedElementIds);
        Assert.Equal(["orders"], layoutResult.Layout.CollapsedBoundaryIds);
        Assert.Empty(layoutResult.Layout.Positions);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private static Project CreateProjectWithStructure()
    {
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing", Now);
        Assert.True(created.TryGetValue(out var project));
        var manual = new StructuralProvenance(StructuralSourceKind.Manual, null, null);
        var elements = new[]
        {
            new StructuralElement("orders", "Orders", StructuralElementType.SoftwareSystem, null,
                "Order processing system.", null, null, StructuralElementStatus.Confirmed, manual),
            new StructuralElement("orders-api", "Orders API", StructuralElementType.Container, "orders",
                "Handles order requests.", null, null, StructuralElementStatus.Confirmed, manual),
            new StructuralElement("customer", "Customer", StructuralElementType.Person, null,
                "Places orders.", null, null, StructuralElementStatus.Confirmed, manual)
        };
        var connections = new[]
        {
            new StructuralConnection("customer-orders", "customer", "orders", "Places orders",
                StructuralInteractionType.Synchronous, null, null, StructuralElementStatus.Confirmed, manual)
        };
        var structure = ArchitecturalStructure.Create(elements, connections);
        Assert.True(structure.TryGetValue(out var validStructure));
        var updated = project.UpdateStructure(validStructure, Now.AddMinutes(1));
        Assert.True(updated.TryGetValue(out var withStructure));
        return withStructure;
    }
}
