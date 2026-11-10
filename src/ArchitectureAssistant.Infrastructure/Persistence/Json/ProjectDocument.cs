using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

internal sealed class ProjectDocument
{
    public ProjectDocument()
    {
    }

    public string SchemaVersion { get; init; } = "1.0";

    public Guid Id { get; init; }

    public long Revision { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public ProjectContextDocument Context { get; init; } = new();

    public IReadOnlyList<RequirementDocument> Requirements { get; init; } = Array.Empty<RequirementDocument>();

    public IReadOnlyList<DecisionDocument> Decisions { get; init; } = Array.Empty<DecisionDocument>();

    public ProjectStructureDocument Structure { get; init; } = new();
}

internal sealed class ProjectStructureDocument
{
    public IReadOnlyList<StructuralElementDocument> Elements { get; init; } = Array.Empty<StructuralElementDocument>();

    public IReadOnlyList<StructuralConnectionDocument> Connections { get; init; } = Array.Empty<StructuralConnectionDocument>();
}

internal sealed class StructuralElementDocument
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public StructuralElementType Type { get; init; }

    public string? ParentId { get; init; }

    public string? Description { get; init; }

    public string? Responsibility { get; init; }

    public string? Technology { get; init; }

    public StructuralElementStatus Status { get; init; }

    public StructuralProvenanceDocument Provenance { get; init; } = new();
}

internal sealed class StructuralConnectionDocument
{
    public string Id { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public StructuralInteractionType InteractionType { get; init; }

    public string? Protocol { get; init; }

    public string? MessageName { get; init; }

    public StructuralElementStatus Status { get; init; }

    public StructuralProvenanceDocument Provenance { get; init; } = new();
}

internal sealed class StructuralProvenanceDocument
{
    public StructuralSourceKind SourceKind { get; init; }

    public string? SourceId { get; init; }

    public string? SourceVersion { get; init; }
}

internal sealed class ProjectContextDocument
{
    public ProjectContextDocument()
    {
    }

    public string? BusinessDomain { get; init; }

    public int? TeamSize { get; init; }

    public ProjectCriticality? Criticality { get; init; }

    public ScalabilityTarget? ScalabilityTarget { get; init; }

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();
}

internal sealed class RequirementDocument
{
    public RequirementDocument()
    {
    }

    public Guid Id { get; init; }

    public string Description { get; init; } = string.Empty;

    public RequirementClassification Classification { get; init; }

    public RequirementPriority? Priority { get; init; }

    public RequirementStatus Status { get; init; }

    public string Origin { get; init; } = "UserProvided";

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

internal sealed class DecisionDocument
{
    public DecisionDocument()
    {
    }

    public Guid Id { get; init; }

    public string SelectedAlternativeId { get; init; } = string.Empty;

    public string SelectedAlternativeKind { get; init; } = string.Empty;

    public IReadOnlyList<string> ConsideredAlternatives { get; init; } = Array.Empty<string>();

    public DecisionStatus Status { get; init; }

    public string Justification { get; init; } = string.Empty;

    public IReadOnlyList<string> Consequences { get; init; } = Array.Empty<string>();

    public string RecordedBy { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; }

    public long Revision { get; init; }

    public Guid? AnalysisId { get; init; }

    public string? SelectedAlternativeVersion { get; init; }

    public string? StatusChangedBy { get; init; }

    public DateTimeOffset? StatusChangedAt { get; init; }

    public Guid? SupersededByDecisionId { get; init; }

    public Guid? SupersedesDecisionId { get; init; }

    public IReadOnlyList<DecisionStatusChange> StatusHistory { get; init; } = Array.Empty<DecisionStatusChange>();

    public HistoricalDecisionSource? HistoricalSource { get; init; }
}
