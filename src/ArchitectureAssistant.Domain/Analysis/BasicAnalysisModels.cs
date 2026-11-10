using System.Text.Json.Serialization;
using ArchitectureAssistant.Domain.KnowledgeGraph;

namespace ArchitectureAssistant.Domain.Analysis;

public sealed record AnalysisSnapshot(
    string SchemaVersion,
    Guid AnalysisId,
    Guid ProjectId,
    long ProjectRevision,
    string ProjectHash,
    string CatalogHash,
    string RuleSetHash,
    string EvaluatorVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AnalysisCandidateGroup> CandidateGroups,
    IReadOnlyList<string> Limitations,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    KnowledgeGraphProjection? GraphProjection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    KnowledgeGraphExpansion? GraphExpansion = null);

public sealed record AnalysisCandidateGroup(
    string Kind,
    IReadOnlyList<AnalysisCandidateResult> Candidates);

public sealed record AnalysisCandidateResult(
    string SchemaVersion,
    Guid AnalysisId,
    string CandidateId,
    string CandidateKind,
    string CandidateName,
    string CandidateVersion,
    string CandidateDescription,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CandidateSource,
    string? CandidateCategory,
    int? CandidateComplexity,
    int? CandidateScalability,
    int? CandidateOperationalCost,
    AnalysisEligibility Eligibility,
    decimal? FitScore,
    decimal? EvidenceCoverage,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<AnalysisRuleEvaluation> RuleEvaluations,
    IReadOnlyList<AnalysisRelationFinding> RelationFindings,
    IReadOnlyList<string> Warnings);

public sealed record AnalysisRuleEvaluation(
    string RuleId,
    string RuleVersion,
    string RuleContentHash,
    string RuleName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Source,
    string KnowledgeSource,
    RuleApplicability Applicability,
    RuleConditionResult ConditionResult,
    bool EffectsApplied,
    IReadOnlyList<AnalysisEffect> Effects,
    IReadOnlyList<string> UnknownFacts,
    IReadOnlyList<AnalysisFactEvidence> FactEvidence);

public sealed record AnalysisEffect(
    string Type,
    bool Applied,
    bool HardConstraint,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Dimension,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Points,
    int Weight,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Severity,
    string Rationale);

public sealed record AnalysisFactEvidence(
    string Fact,
    string Phase,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Operator,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ExpectedValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Value,
    string Status);

public sealed record AnalysisRelationFinding(
    string Type,
    string SourceId,
    string TargetId,
    string Status,
    string Rationale,
    string? RelationKey = null,
    string? Applicability = null,
    string? SourceVersion = null,
    string? TargetVersion = null,
    string? KnowledgeSource = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RelationSource = null,
    IReadOnlyList<string>? UnknownFacts = null);

public sealed record BasicAnalysisCandidate(
    string Id,
    string Kind,
    string Name,
    string Version,
    string Description,
    string? Source,
    IReadOnlyList<string> Tags,
    string? Category,
    int? Complexity,
    int? Scalability,
    int? OperationalCost,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> Limitations);

public sealed record BasicAnalysisRule(
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
    IReadOnlyList<BasicAnalysisEffect> Effects);

public sealed record BasicAnalysisEffect(
    string Type,
    bool HardConstraint,
    int? Points,
    string? Severity,
    string Rationale);

public enum AnalysisEligibility
{
    Eligible,
    Excluded,
    NotEvaluated
}

public enum RuleApplicability
{
    Applicable,
    NotApplicable,
    Unknown
}

public enum RuleConditionResult
{
    Matched,
    NotMatched,
    Unknown,
    NotEvaluated
}
