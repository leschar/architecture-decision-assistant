using ArchitectureAssistant.Domain.Analysis;

namespace ArchitectureAssistant.Application.Knowledge;

public sealed record KnowledgeCatalogSnapshot(
    string SchemaVersion,
    string ContentHash,
    IReadOnlyList<KnowledgeItemView> Items,
    IReadOnlyList<KnowledgeRuleView> Rules);

public sealed record KnowledgeRuleView(
    string Id,
    string Version,
    string Name,
    string? Source,
    string KnowledgeSource,
    string ContentHash,
    bool Enabled,
    int Weight,
    IReadOnlyList<string> Kinds,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> CandidateTags,
    RuleCondition? ApplicabilityCondition,
    RuleCondition Conditions,
    IReadOnlyList<KnowledgeRuleEffectView> Effects);

public sealed record KnowledgeRuleEffectView(
    string Type,
    bool HardConstraint,
    int? Points,
    string? Severity,
    string Rationale);

public sealed record KnowledgeItemView(
    string Id,
    string Kind,
    string Version,
    string Name,
    string Description,
    string? Source,
    string? Category,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> UseWhen,
    IReadOnlyList<string> AvoidWhen,
    int? Complexity,
    int? Scalability,
    int? OperationalCost,
    string KnowledgeSource,
    IReadOnlyList<KnowledgeRelationView> Relations);

public sealed record KnowledgeRelationView(
    string Type,
    string TargetId,
    string Rationale,
    string? Source,
    RuleCondition? AppliesWhen,
    string KnowledgeSource);
