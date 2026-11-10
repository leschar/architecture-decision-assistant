using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Application.Projects;

public sealed record ProjectListItem(
    Guid Id,
    string Name,
    string Purpose,
    long Revision,
    int RequirementCount,
    DateTimeOffset UpdatedAt);

public sealed record RequirementView(
    Guid Id,
    string Description,
    RequirementClassification Classification,
    RequirementPriority? Priority,
    RequirementStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ArchitecturalDecisionView(
    Guid Id,
    string SelectedAlternativeId,
    string SelectedAlternativeKind,
    string? SelectedAlternativeVersion,
    IReadOnlyList<string> ConsideredAlternatives,
    DecisionStatus Status,
    string Justification,
    IReadOnlyList<string> Consequences,
    string RecordedBy,
    DateTimeOffset RecordedAt,
    long Revision,
    Guid? AnalysisId,
    string? StatusChangedBy,
    DateTimeOffset? StatusChangedAt,
    Guid? SupersedesDecisionId,
    Guid? SupersededByDecisionId,
    IReadOnlyList<DecisionStatusChange> StatusHistory,
    HistoricalDecisionSource? HistoricalSource);

public sealed record StructuralElementView(
    string Id,
    string Name,
    StructuralElementType Type,
    string? ParentId,
    string? Description,
    string? Responsibility,
    string? Technology,
    StructuralElementStatus Status,
    StructuralProvenance Provenance);

public sealed record StructuralConnectionView(
    string Id,
    string SourceId,
    string TargetId,
    string Description,
    StructuralInteractionType InteractionType,
    string? Protocol,
    string? MessageName,
    StructuralElementStatus Status,
    StructuralProvenance Provenance);

public sealed record ProjectView(
    Guid Id,
    string Name,
    string Purpose,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? BusinessDomain,
    int? TeamSize,
    ProjectCriticality? Criticality,
    ScalabilityTarget? ScalabilityTarget,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<RequirementView> Requirements,
    IReadOnlyList<ArchitecturalDecisionView> Decisions,
    IReadOnlyList<StructuralElementView> StructuralElements,
    IReadOnlyList<StructuralConnectionView> StructuralConnections)
{
    public static ProjectView From(Project project) =>
        new(
            project.Id,
            project.Name,
            project.Purpose,
            project.Revision,
            project.CreatedAt,
            project.UpdatedAt,
            project.Context.BusinessDomain,
            project.Context.TeamSize,
            project.Context.Criticality,
            project.Context.ScalabilityTarget,
            project.Context.Constraints,
            project.Requirements.Select(requirement => new RequirementView(
                requirement.Id,
                requirement.Description,
                requirement.Classification,
                requirement.Priority,
                requirement.Status,
                requirement.CreatedAt)).ToArray(),
            project.Decisions.Select(decision => new ArchitecturalDecisionView(
                decision.Id,
                decision.SelectedAlternativeId,
                decision.SelectedAlternativeKind,
                decision.SelectedAlternativeVersion,
                decision.ConsideredAlternatives,
                decision.Status,
                decision.Justification,
                decision.Consequences,
                decision.RecordedBy,
                decision.RecordedAt,
                decision.Revision,
                decision.AnalysisId,
                decision.StatusChangedBy,
                decision.StatusChangedAt,
                decision.SupersedesDecisionId,
                decision.SupersededByDecisionId,
                decision.StatusHistory,
                decision.HistoricalSource)).ToArray(),
            project.Structure.Elements.Select(element => new StructuralElementView(
                element.Id,
                element.Name,
                element.Type,
                element.ParentId,
                element.Description,
                element.Responsibility,
                element.Technology,
                element.Status,
                element.Provenance)).ToArray(),
            project.Structure.Connections.Select(connection => new StructuralConnectionView(
                connection.Id,
                connection.SourceId,
                connection.TargetId,
                connection.Description,
                connection.InteractionType,
                connection.Protocol,
                connection.MessageName,
                connection.Status,
                connection.Provenance)).ToArray());
}

public static class ProjectListItemMapper
{
    public static ProjectListItem From(Project project) =>
        new(
            project.Id,
            project.Name,
            project.Purpose,
            project.Revision,
            project.Requirements.Count,
            project.UpdatedAt);
}
