using ArchitectureAssistant.Domain.Analysis;
using System.Text.Json.Serialization;

namespace ArchitectureAssistant.Domain.KnowledgeGraph;

public sealed record KnowledgeGraphItem(
    string Id,
    string Kind,
    string Version,
    string Name,
    string? Source,
    string KnowledgeSource,
    IReadOnlyList<KnowledgeGraphRelationDeclaration> Relations);

public sealed record KnowledgeGraphRelationDeclaration(
    string Type,
    string TargetId,
    string Rationale,
    string? Source,
    RuleCondition? AppliesWhen,
    string KnowledgeSource);

public sealed record KnowledgeGraphProjection(
    string SchemaVersion,
    string CatalogHash,
    IReadOnlyList<KnowledgeGraphNode> Nodes,
    IReadOnlyList<KnowledgeGraphRelation> Relations,
    IReadOnlyList<string> Warnings);

public sealed record KnowledgeGraphNode(
    string ItemId,
    string Kind,
    string Name,
    string Version,
    string? Source,
    string KnowledgeSource);

public sealed record KnowledgeGraphRelation(
    string RelationKey,
    string SourceId,
    string TargetId,
    string Type,
    string ConditionKey,
    string SourceVersion,
    string TargetVersion,
    IReadOnlyList<KnowledgeGraphRelationDeclarationView> Declarations);

public sealed record KnowledgeGraphRelationDeclarationView(
    string RelationKey,
    string Type,
    string SourceId,
    string TargetId,
    string ConditionKey,
    [property: JsonIgnore]
    RuleCondition? AppliesWhen,
    string Rationale,
    string? Source,
    string KnowledgeSource);

public sealed record KnowledgeGraphExpansion(
    string SchemaVersion,
    string CatalogHash,
    IReadOnlyList<KnowledgeGraphExpansionRoot> Roots,
    IReadOnlyList<KnowledgeGraphExpansionNode> Nodes,
    IReadOnlyList<KnowledgeGraphDependencyFinding> Dependencies,
    bool Truncated,
    int MaximumDepth,
    int MaximumPathCount,
    int EvaluatedPathCount,
    IReadOnlyList<string> Warnings);

public sealed record KnowledgeGraphExpansionRoot(
    string ItemId,
    string Role);

public sealed record KnowledgeGraphExpansionNode(
    string ItemId,
    IReadOnlyList<string> Roles,
    string SelectionStatus);

public sealed record KnowledgeGraphDependencyFinding(
    string RootId,
    string RelationKey,
    string SourceId,
    string TargetId,
    string Applicability,
    string Finding,
    string Rationale,
    string SourceVersion,
    string TargetVersion,
    string KnowledgeSource,
    string? RelationSource,
    IReadOnlyList<string> UnknownFacts,
    IReadOnlyList<IReadOnlyList<string>> PathsFromRoot);

public sealed record AnalysisCandidateComparison(
    string SchemaVersion,
    Guid AnalysisId,
    Guid ProjectId,
    string CatalogHash,
    CandidateComparisonSide FirstCandidate,
    CandidateComparisonSide SecondCandidate,
    IReadOnlyList<AnalysisRelationFinding> RelationsBetweenCandidates,
    IReadOnlyList<KnowledgeGraphDependencyFinding> FirstDependencies,
    IReadOnlyList<KnowledgeGraphDependencyFinding> SecondDependencies,
    IReadOnlyList<string> SharedRequiredDependencyIds,
    IReadOnlyList<string> FirstOnlyRequiredDependencyIds,
    IReadOnlyList<string> SecondOnlyRequiredDependencyIds,
    string DependencyDataStatus,
    IReadOnlyList<string> Limitations);

public sealed record CandidateComparisonSide(
    string CandidateId,
    string Kind,
    string Name,
    string Version,
    AnalysisEligibility Eligibility,
    decimal? FitScore,
    decimal? EvidenceCoverage);
