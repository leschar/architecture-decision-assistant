using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInputStartsAtRevisionOne()
    {
        var result = Project.Create(Guid.NewGuid(), "Orders", "Order processing platform", Now);

        Assert.True(result.TryGetValue(out var project));
        Assert.Equal(1, project.Revision);
        Assert.Empty(project.Requirements);
        Assert.Empty(project.Decisions);
    }

    [Fact]
    public void Create_WhitespaceNameReturnsDomainError()
    {
        var result = Project.Create(Guid.NewGuid(), "   ", "Order processing platform", Now);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "project.name.required");
    }

    [Fact]
    public void AddRequirement_IncrementsRevisionAndPreservesPreviousAggregate()
    {
        var project = CreateProject();
        var requirement = Requirement.Create(
            Guid.NewGuid(),
            "The system must support concurrent users.",
            RequirementClassification.Quality,
            RequirementPriority.Must,
            RequirementStatus.Confirmed,
            Now);

        Assert.True(requirement.TryGetValue(out var value));
        var updated = project.AddRequirement(value, Now.AddMinutes(1));

        Assert.True(updated.TryGetValue(out var revised));
        Assert.Equal(1, project.Revision);
        Assert.Empty(project.Requirements);
        Assert.Equal(2, revised.Revision);
        Assert.Single(revised.Requirements);
    }

    [Fact]
    public void AddRequirement_DuplicateIdReturnsDomainError()
    {
        var requirementId = Guid.NewGuid();
        var requirement = Requirement.Create(
            requirementId,
            "Keep an audit trail.",
            RequirementClassification.Quality,
            null,
            RequirementStatus.Confirmed,
            Now);
        Assert.True(requirement.TryGetValue(out var value));

        var project = Project.Restore(
            Guid.NewGuid(),
            2,
            "Orders",
            "Order processing platform",
            Now,
            Now,
            ProjectContext.Empty,
            [value]);
        Assert.True(project.TryGetValue(out var restored));

        var duplicate = Requirement.Create(
            requirementId,
            "Use a specific storage technology.",
            RequirementClassification.Constraint,
            null,
            RequirementStatus.Confirmed,
            Now);
        Assert.True(duplicate.TryGetValue(out var duplicateValue));

        var result = restored.AddRequirement(duplicateValue, Now.AddMinutes(1));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "requirement.id.duplicate");
    }

    [Fact]
    public void UpdateStructure_ValidatesHierarchyAndDirectionalEventDetails()
    {
        var project = CreateProject();
        var system = new StructuralElement(
            "orders-system", "Orders", StructuralElementType.SoftwareSystem, null,
            null, null, null, StructuralElementStatus.Confirmed,
            new StructuralProvenance(StructuralSourceKind.Manual, null, null));
        var container = new StructuralElement(
            "orders-api", "Orders API", StructuralElementType.Container, system.Id,
            null, null, null, StructuralElementStatus.Proposed,
            new StructuralProvenance(StructuralSourceKind.Manual, null, null));
        var elements = new[] { system, container };
        var connection = new StructuralConnection(
            "orders-event",
            container.Id,
            system.Id,
            "Publishes an order event",
            StructuralInteractionType.AsynchronousEvent,
            null,
            "OrderCreated",
            StructuralElementStatus.Proposed,
            new StructuralProvenance(StructuralSourceKind.Manual, null, null));

        var structure = ArchitecturalStructure.Create(elements, [connection]);
        Assert.True(structure.TryGetValue(out var validatedStructure));
        var updated = project.UpdateStructure(validatedStructure, Now.AddMinutes(1));

        Assert.True(updated.TryGetValue(out var revised));
        Assert.Equal(2, revised.Revision);
        Assert.Equal("orders-api", Assert.Single(revised.Structure.Elements, element => element.Type == StructuralElementType.Container).Id);
        Assert.Equal("OrderCreated", Assert.Single(revised.Structure.Connections).MessageName);

        var invalidEvent = ArchitecturalStructure.Create(elements, [connection with { MessageName = null }]);
        Assert.False(invalidEvent.IsSuccess);
        Assert.Contains(invalidEvent.Errors, error => error.Code == "structure.connection.event_name.required");
    }

    private static Project CreateProject()
    {
        var result = Project.Create(Guid.NewGuid(), "Orders", "Order processing platform", Now);
        Assert.True(result.TryGetValue(out var project));
        return project;
    }
}
