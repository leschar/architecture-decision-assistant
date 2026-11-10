using ArchitectureAssistant.Domain.Diagrams;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class ArchitecturalDiagramProjectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Project_ContextAndContainerViewsRespectScopeAndExplicitConnections()
    {
        var project = CreateProjectWithStructure();
        var projector = new ArchitecturalDiagramProjector();

        var context = projector.Project(project, ArchitecturalDiagramType.Context, "orders", Now);
        var containers = projector.Project(project, ArchitecturalDiagramType.Container, "orders", Now);

        Assert.True(context.TryGetValue(out var contextDiagram));
        Assert.Equal(["customer", "orders"], contextDiagram.Elements.Select(element => element.Id).Order().ToArray());
        Assert.Single(contextDiagram.Connections);
        Assert.True(containers.TryGetValue(out var containerDiagram));
        Assert.Equal(["customer", "orders", "orders-api", "erp"], containerDiagram.Elements.Select(element => element.Id).Order().ToArray());
        Assert.Equal(2, containerDiagram.Connections.Count);
    }

    [Fact]
    public void Project_ComponentWithoutExplicitComponentsReturnsIncompleteDiagram()
    {
        var project = CreateProjectWithStructure();
        var projector = new ArchitecturalDiagramProjector();

        var result = projector.Project(project, ArchitecturalDiagramType.Component, "orders-api", Now);

        Assert.True(result.TryGetValue(out var diagram));
        Assert.Equal(ArchitecturalDiagramStatus.Incomplete, diagram.Status);
        Assert.Contains(diagram.Warnings, warning => warning.Contains("Nenhum componente", StringComparison.Ordinal));
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
                "Places orders.", null, null, StructuralElementStatus.Confirmed, manual),
            new StructuralElement("erp", "ERP", StructuralElementType.SoftwareSystem, null,
                "External enterprise system.", null, null, StructuralElementStatus.Confirmed, manual)
        };
        var connections = new[]
        {
            new StructuralConnection("customer-orders", "customer", "orders", "Places orders",
                StructuralInteractionType.Synchronous, null, null, StructuralElementStatus.Confirmed, manual),
            new StructuralConnection("orders-erp", "orders-api", "erp", "Sends order data",
                StructuralInteractionType.BatchTransfer, null, null, StructuralElementStatus.Proposed, manual)
        };
        var structure = ArchitecturalStructure.Create(elements, connections);
        Assert.True(structure.TryGetValue(out var validStructure));
        var updated = project.UpdateStructure(validStructure, Now.AddMinutes(1));
        Assert.True(updated.TryGetValue(out var withStructure));
        return withStructure;
    }
}
