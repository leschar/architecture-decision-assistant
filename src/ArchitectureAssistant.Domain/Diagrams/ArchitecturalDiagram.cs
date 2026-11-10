namespace ArchitectureAssistant.Domain.Diagrams;

public enum ArchitecturalDiagramType
{
    Context,
    Container,
    Component
}

public enum ArchitecturalDiagramStatus
{
    Complete,
    Incomplete,
    Stale
}

public sealed record DiagramSourceReference(
    string SourceType,
    string SourceId,
    string? SourceVersion);

public sealed record DiagramElement(
    string Id,
    string ElementType,
    string Name,
    string Description,
    string? Technology,
    string? ParentId,
    string Status,
    IReadOnlyList<DiagramSourceReference> Provenance);

public sealed record DiagramConnection(
    string Id,
    string SourceId,
    string TargetId,
    string Description,
    string InteractionType,
    string? Protocol,
    string? MessageName,
    string Status,
    IReadOnlyList<DiagramSourceReference> Provenance);

public sealed record ArchitecturalDiagram(
    string SchemaVersion,
    string DiagramId,
    ArchitecturalDiagramType DiagramType,
    Guid ProjectId,
    string ProjectRevision,
    string SourceFingerprint,
    string GeneratorVersion,
    DateTimeOffset GeneratedAt,
    string ScopeId,
    string Title,
    ArchitecturalDiagramStatus Status,
    IReadOnlyList<DiagramElement> Elements,
    IReadOnlyList<DiagramConnection> Connections,
    IReadOnlyList<string> Warnings);

public sealed record DiagramElementPosition(string ElementId, decimal X, decimal Y);

public sealed record ArchitecturalDiagramLayout(
    string SchemaVersion,
    string DiagramId,
    string SourceFingerprint,
    IReadOnlyList<DiagramElementPosition> Positions,
    IReadOnlyList<string> CollapsedBoundaryIds);

public sealed record DiagramLayoutLoadResult(
    ArchitecturalDiagramLayout Layout,
    IReadOnlyList<string> OrphanedElementIds);
