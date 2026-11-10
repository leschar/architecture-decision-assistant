using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Analysis;

public sealed class BasicAnalysisEngine
{
    public const string CurrentVersion = "2.0.0";

    private static readonly string[] CandidateKinds = ["Architecture", "Pattern", "Technology"];
    private static readonly JsonSerializerOptions FingerprintOptions = CreateFingerprintOptions();
    private readonly RuleConditionEvaluator _conditionEvaluator = new();
    private readonly KnowledgeGraphExpander _graphExpander = new();

    public static string ComputeProjectHash(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var content = new
        {
            project.Id,
            project.Revision,
            project.Name,
            project.Purpose,
            project.CreatedAt,
            project.UpdatedAt,
            Context = new
            {
                project.Context.BusinessDomain,
                project.Context.TeamSize,
                project.Context.Criticality,
                project.Context.ScalabilityTarget,
                Constraints = project.Context.Constraints.Order(StringComparer.Ordinal)
            },
            Requirements = project.Requirements
                .OrderBy(requirement => requirement.Id)
                .Select(requirement => new
                {
                    requirement.Id,
                    requirement.Description,
                    requirement.Classification,
                    requirement.Priority,
                    requirement.Status,
                    requirement.Origin,
                    requirement.CreatedAt,
                    requirement.UpdatedAt
                }),
            Decisions = project.Decisions
                .OrderBy(decision => decision.Id)
                .Select(decision => new
                {
                    decision.Id,
                    decision.SelectedAlternativeId,
                    decision.SelectedAlternativeKind,
                    ConsideredAlternatives = decision.ConsideredAlternatives.Order(StringComparer.Ordinal),
                    decision.Status,
                    decision.Justification,
                    Consequences = decision.Consequences.Order(StringComparer.Ordinal),
                    decision.RecordedBy,
                    decision.RecordedAt,
                    decision.Revision
                })
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(content, FingerprintOptions);
        return $"sha256:{Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant()}";
    }

    public Result<AnalysisSnapshot> Analyze(
        Guid analysisId,
        Project project,
        string catalogHash,
        string ruleSetHash,
        IReadOnlyList<BasicAnalysisCandidate> candidates,
        IReadOnlyList<BasicAnalysisRule> rules,
        DateTimeOffset createdAt,
        KnowledgeGraphProjection? graphProjection = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rules);

        if (analysisId == Guid.Empty)
        {
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.id.invalid", "Analysis ID must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(catalogHash) || string.IsNullOrWhiteSpace(ruleSetHash))
        {
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.fingerprint.required", "Catalog and rule-set fingerprints are required."));
        }

        if (candidates.Select(candidate => candidate.Id).Distinct(StringComparer.Ordinal).Count() != candidates.Count ||
            rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count() != rules.Count)
        {
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.input.duplicate_id", "Analysis inputs contain duplicate candidate or rule IDs."));
        }

        if (graphProjection is not null)
        {
            var graphErrors = ValidateGraphProjection(graphProjection, catalogHash, candidates);
            if (graphErrors.Count > 0)
            {
                return Result<AnalysisSnapshot>.Failure(graphErrors);
            }
        }

        if (candidates.Any(candidate => !CandidateKinds.Contains(candidate.Kind, StringComparer.Ordinal)))
        {
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.candidate.kind.invalid", "Analysis candidates must be Architecture, Pattern, or Technology items."));
        }

        var invalidRules = rules.SelectMany(ValidateRule).ToArray();
        if (invalidRules.Length > 0)
        {
            return Result<AnalysisSnapshot>.Failure(invalidRules);
        }

        var relationFindings = graphProjection is null
            ? new Dictionary<string, IReadOnlyList<AnalysisRelationFinding>>(StringComparer.Ordinal)
            : EvaluateRelations(project, candidates, graphProjection);
        KnowledgeGraphExpansion? graphExpansion = null;
        if (graphProjection is not null)
        {
            var expansionResult = _graphExpander.Expand(project, candidates, graphProjection);
            if (!expansionResult.TryGetValue(out graphExpansion))
            {
                return Result<AnalysisSnapshot>.Failure(expansionResult.Errors);
            }
        }

        var groups = CandidateKinds
            .Select(kind => new AnalysisCandidateGroup(
                kind,
                Array.AsReadOnly(candidates
                    .Where(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal))
                    .Select(candidate => EvaluateCandidate(
                        analysisId,
                        project,
                        candidate,
                        rules,
                        relationFindings.GetValueOrDefault(candidate.Id) ?? Array.Empty<AnalysisRelationFinding>()))
                    .OrderBy(candidate => candidate.Eligibility switch
                    {
                        AnalysisEligibility.Eligible => 0,
                        AnalysisEligibility.NotEvaluated => 1,
                        AnalysisEligibility.Excluded => 2,
                        _ => 3
                    })
                    .ThenByDescending(candidate => candidate.FitScore.HasValue)
                    .ThenByDescending(candidate => candidate.FitScore)
                    .ThenByDescending(candidate => candidate.EvidenceCoverage)
                    .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                    .ToArray())))
            .ToArray();

        return Result<AnalysisSnapshot>.Success(new AnalysisSnapshot(
            "2.0",
            analysisId,
            project.Id,
            project.Revision,
            ComputeProjectHash(project),
            catalogHash,
            ruleSetHash,
            CurrentVersion,
            createdAt,
            Array.AsReadOnly(groups),
            Array.AsReadOnly(new[]
            {
                "Only the documented facts and condition operators in the deterministic rule registry are evaluated.",
                "Only explicit direct catalog relations are evaluated; they do not select candidates or add score.",
                "Analysis results are recommendations; human decisions remain explicit project data."
            }),
            graphProjection,
            graphExpansion));
    }

    private AnalysisCandidateResult EvaluateCandidate(
        Guid analysisId,
        Project project,
        BasicAnalysisCandidate candidate,
        IReadOnlyList<BasicAnalysisRule> rules,
        IReadOnlyList<AnalysisRelationFinding> relationFindings)
    {
        var relevantRules = rules
            .Where(rule => rule.Enabled &&
                           rule.Kinds.Contains(candidate.Kind, StringComparer.Ordinal) &&
                           (rule.CandidateIds.Count == 0 ||
                            rule.CandidateIds.Contains(candidate.Id, StringComparer.Ordinal)) &&
                           (rule.CandidateTags.Count == 0 ||
                            rule.CandidateTags.Intersect(candidate.Tags, StringComparer.Ordinal).Any()))
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();

        var ruleEvaluations = new List<AnalysisRuleEvaluation>();
        var warnings = new SortedSet<string>(StringComparer.Ordinal);
        var scoreWeight = 0;
        var weightedPoints = 0;
        var evidenceWeight = 0;
        var potentialEvidenceWeight = 0;
        var hasEvaluatedHardConstraint = false;
        var unknownHardConstraint = false;
        var excludedByHardConstraint = false;

        foreach (var rule in relevantRules)
        {
            var factEvidence = new List<AnalysisFactEvidence>();
            var applicability = rule.ApplicabilityCondition is null
                ? RuleApplicability.Applicable
                : ToApplicability(_conditionEvaluator.Evaluate(
                    rule.ApplicabilityCondition,
                    project,
                    candidate,
                    "Applicability",
                    factEvidence));
            var conditionResult = applicability == RuleApplicability.Applicable
                ? _conditionEvaluator.Evaluate(rule.Conditions, project, candidate, "Condition", factEvidence)
                : RuleConditionResult.NotEvaluated;

            var effectsApplied = applicability == RuleApplicability.Applicable &&
                                 conditionResult == RuleConditionResult.Matched;
            var scoreEffect = rule.Effects.SingleOrDefault(effect => effect.Type == "Score");
            if (scoreEffect is not null && applicability != RuleApplicability.NotApplicable)
            {
                potentialEvidenceWeight += rule.Weight;
                if (applicability == RuleApplicability.Applicable &&
                    conditionResult is RuleConditionResult.Matched or RuleConditionResult.NotMatched)
                {
                    evidenceWeight += rule.Weight;
                    scoreWeight += rule.Weight;
                    if (conditionResult == RuleConditionResult.Matched)
                    {
                        weightedPoints += rule.Weight * scoreEffect.Points!.Value;
                    }
                }
            }

            if (applicability == RuleApplicability.Unknown)
            {
                warnings.Add($"Rule '{rule.Id}' has Unknown applicability because one or more project facts are unavailable.");
            }

            foreach (var fact in factEvidence.Where(item => item.Status == "Unknown")
                         .Select(item => item.Fact)
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                warnings.Add($"Rule '{rule.Id}' requires confirmed fact '{fact}', which is Unknown.");
            }

            foreach (var effect in rule.Effects.Where(effect =>
                         effect.Type == "Exclude" && effect.HardConstraint))
            {
                if (applicability == RuleApplicability.Unknown ||
                    (applicability == RuleApplicability.Applicable &&
                     conditionResult is RuleConditionResult.Unknown or RuleConditionResult.NotEvaluated))
                {
                    unknownHardConstraint = true;
                }
                else if (applicability == RuleApplicability.Applicable)
                {
                    hasEvaluatedHardConstraint = true;
                    if (conditionResult == RuleConditionResult.Matched)
                    {
                        excludedByHardConstraint = true;
                        warnings.Add(effect.Rationale);
                    }
                }
            }

            foreach (var flag in rule.Effects.Where(effect =>
                         effect.Type == "Flag" && effectsApplied))
            {
                warnings.Add(flag.Rationale);
            }

            ruleEvaluations.Add(new AnalysisRuleEvaluation(
                rule.Id,
                rule.Version,
                rule.ContentHash,
                rule.Name,
                rule.Source,
                rule.KnowledgeSource,
                applicability,
                conditionResult,
                effectsApplied,
                Array.AsReadOnly(rule.Effects
                    .Select(effect => new AnalysisEffect(
                        effect.Type,
                        effectsApplied,
                        effect.HardConstraint,
                        effect.Type == "Score" ? "fit" : null,
                        effect.Points,
                        rule.Weight,
                        effect.Severity,
                        effect.Rationale))
                    .ToArray()),
                Array.AsReadOnly(factEvidence
                    .Where(item => item.Status == "Unknown")
                    .Select(item => item.Fact)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()),
                Array.AsReadOnly(factEvidence.ToArray())));
        }

        var eligibility = excludedByHardConstraint
            ? AnalysisEligibility.Excluded
            : hasEvaluatedHardConstraint && !unknownHardConstraint
                ? AnalysisEligibility.Eligible
                : AnalysisEligibility.NotEvaluated;

        decimal? score = scoreWeight == 0
            ? null
            : Math.Clamp(50m + (50m * weightedPoints / (100m * scoreWeight)), 0m, 100m);
        decimal? coverage = potentialEvidenceWeight == 0
            ? null
            : (decimal)evidenceWeight / potentialEvidenceWeight;

        return new AnalysisCandidateResult(
            "2.0",
            analysisId,
            candidate.Id,
            candidate.Kind,
            candidate.Name,
            candidate.Version,
            candidate.Description,
            candidate.Source,
            candidate.Category,
            candidate.Complexity,
            candidate.Scalability,
            candidate.OperationalCost,
            eligibility,
            score,
            coverage,
            Array.AsReadOnly(candidate.Benefits.ToArray()),
            Array.AsReadOnly(candidate.Limitations.ToArray()),
            Array.AsReadOnly(ruleEvaluations.ToArray()),
            relationFindings,
            Array.AsReadOnly(warnings.ToArray()));
    }

    private IReadOnlyDictionary<string, IReadOnlyList<AnalysisRelationFinding>> EvaluateRelations(
        Project project,
        IReadOnlyList<BasicAnalysisCandidate> candidates,
        KnowledgeGraphProjection graphProjection)
    {
        var candidatesById = candidates.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var selectedIds = project.Decisions
            .Where(decision => decision.Status == DecisionStatus.Accepted)
            .Select(decision => decision.SelectedAlternativeId)
            .ToHashSet(StringComparer.Ordinal);
        var findings = candidates.ToDictionary(
            candidate => candidate.Id,
            _ => new List<AnalysisRelationFinding>(),
            StringComparer.Ordinal);

        foreach (var relation in graphProjection.Relations)
        {
            var declarations = relation.Declarations
                .OrderBy(item => item.RelationKey, StringComparer.Ordinal)
                .ToArray();
            var declaration = declarations[0];
            var sourceCandidate = candidatesById[declaration.SourceId];
            var targetCandidate = candidatesById[declaration.TargetId];

            var declarationEvaluations = declarations
                .Select(current =>
                {
                    var evidence = new List<AnalysisFactEvidence>();
                    var conditionResult = current.AppliesWhen is null
                        ? RuleConditionResult.Matched
                        : _conditionEvaluator.Evaluate(
                            current.AppliesWhen,
                            project,
                            candidatesById[current.SourceId],
                            "Applicability",
                            evidence);
                    return new RelationConditionEvaluation(conditionResult, evidence);
                })
                .ToArray();
            var conditionResult = relation.Type is "ConflictsWith" or "AlternativeTo"
                ? CombineSymmetricApplicability(declarationEvaluations)
                : declarationEvaluations[0].Result;
            var applicability = ToApplicability(conditionResult);
            var status = conditionResult switch
            {
                RuleConditionResult.Unknown => "Unknown",
                RuleConditionResult.NotMatched => "NotEvaluated",
                _ => relation.Type switch
                {
                    "DependsOn" => selectedIds.Contains(targetCandidate.Id) ? "Satisfied" : "Missing",
                    "Recommends" => "Suggested",
                    "ConflictsWith" => selectedIds.Contains(sourceCandidate.Id) &&
                                       selectedIds.Contains(targetCandidate.Id)
                        ? "Conflict"
                        : "Relevant",
                    "AlternativeTo" => "Alternative",
                    _ => "NotEvaluated"
                }
            };
            var unknownFacts = declarationEvaluations
                .Where(evaluation => evaluation.Result == RuleConditionResult.Unknown)
                .SelectMany(evaluation => evaluation.Evidence)
                .Where(item => item.Status == "Unknown")
                .Select(item => item.Fact)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var finding = new AnalysisRelationFinding(
                relation.Type,
                declaration.SourceId,
                declaration.TargetId,
                status,
                declaration.Rationale,
                declaration.RelationKey,
                applicability.ToString(),
                sourceCandidate.Version,
                targetCandidate.Version,
                declaration.KnowledgeSource,
                declaration.Source,
                Array.AsReadOnly(unknownFacts));

            findings[sourceCandidate.Id].Add(finding);
            if (relation.Type is "ConflictsWith" or "AlternativeTo" &&
                sourceCandidate.Id != targetCandidate.Id)
            {
                findings[targetCandidate.Id].Add(finding);
            }
        }

        return findings.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<AnalysisRelationFinding>)Array.AsReadOnly(pair.Value
                .OrderBy(finding => finding.Type, StringComparer.Ordinal)
                .ThenBy(finding => finding.SourceId, StringComparer.Ordinal)
                .ThenBy(finding => finding.TargetId, StringComparer.Ordinal)
                .ThenBy(finding => finding.RelationKey, StringComparer.Ordinal)
                .ToArray()),
            StringComparer.Ordinal);
    }

    private static RuleConditionResult CombineSymmetricApplicability(
        IReadOnlyList<RelationConditionEvaluation> evaluations) =>
        evaluations.Any(evaluation => evaluation.Result == RuleConditionResult.Matched)
            ? RuleConditionResult.Matched
            : evaluations.All(evaluation => evaluation.Result == RuleConditionResult.NotMatched)
                ? RuleConditionResult.NotMatched
                : RuleConditionResult.Unknown;

    private static IReadOnlyList<DomainError> ValidateGraphProjection(
        KnowledgeGraphProjection projection,
        string catalogHash,
        IReadOnlyList<BasicAnalysisCandidate> candidates)
    {
        var errors = new List<DomainError>();
        if (projection.SchemaVersion != "1.0" ||
            !string.Equals(projection.CatalogHash, catalogHash, StringComparison.Ordinal))
        {
            errors.Add(new DomainError(
                "analysis.graph.catalog_mismatch",
                "The knowledge graph projection does not match the analysis catalog or schema."));
        }

        var candidatesById = candidates.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        if (projection.Nodes.Select(node => node.ItemId)
            .Distinct(StringComparer.Ordinal).Count() != projection.Nodes.Count)
        {
            errors.Add(new DomainError(
                "analysis.graph.duplicate_node",
                "The knowledge graph projection contains duplicate node IDs."));
            return errors;
        }

        var nodesById = projection.Nodes.ToDictionary(node => node.ItemId, StringComparer.Ordinal);
        foreach (var node in projection.Nodes)
        {
            if (!candidatesById.TryGetValue(node.ItemId, out var candidate) ||
                candidate.Kind != node.Kind ||
                candidate.Version != node.Version)
            {
                errors.Add(new DomainError(
                    "analysis.graph.node_mismatch",
                    $"Graph node '{node.ItemId}' does not match an analysis candidate."));
            }
        }

        foreach (var relation in projection.Relations)
        {
            if (relation.Declarations.Count == 0 ||
                !nodesById.ContainsKey(relation.SourceId) ||
                !nodesById.ContainsKey(relation.TargetId) ||
                !candidatesById.ContainsKey(relation.SourceId) ||
                !candidatesById.ContainsKey(relation.TargetId) ||
                relation.Declarations.Any(declaration =>
                    declaration.Type != relation.Type ||
                    !nodesById.ContainsKey(declaration.SourceId) ||
                    !nodesById.ContainsKey(declaration.TargetId) ||
                    !candidatesById.ContainsKey(declaration.SourceId) ||
                    !candidatesById.ContainsKey(declaration.TargetId)))
            {
                errors.Add(new DomainError(
                    "analysis.graph.relation_invalid",
                    $"Graph relation '{relation.RelationKey}' has missing endpoints or provenance."));
            }
        }

        return errors;
    }

    private static IEnumerable<DomainError> ValidateRule(BasicAnalysisRule rule)
    {
        foreach (var error in RuleConditionEvaluator.Validate(rule.Conditions))
        {
            yield return new DomainError(
                "analysis.rule.invalid",
                $"Rule '{rule.Id}' has an invalid conditions tree: {error}");
        }

        if (rule.ApplicabilityCondition is not null)
        {
            foreach (var error in RuleConditionEvaluator.Validate(rule.ApplicabilityCondition))
            {
                yield return new DomainError(
                    "analysis.rule.invalid",
                    $"Rule '{rule.Id}' has an invalid applicability tree: {error}");
            }

            if (EnumeratePredicates(rule.ApplicabilityCondition)
                .Any(condition => condition.Fact is null ||
                                  !condition.Fact.StartsWith("project.", StringComparison.Ordinal)))
            {
                yield return new DomainError(
                    "analysis.rule.invalid",
                    $"Rule '{rule.Id}' applicability conditions may only reference project facts.");
            }
        }

        if (rule.Weight is < 1 or > 100 ||
            rule.Effects.Count == 0 ||
            rule.Effects.Count(effect => effect.Type == "Score") > 1 ||
            rule.Effects.Any(effect =>
                effect.Type == "Score" && effect.Points is not (>= -100 and <= 100) ||
                effect.Type == "Exclude" && !effect.HardConstraint ||
                effect.Type == "Flag" && effect.HardConstraint ||
                effect.Type is not ("Score" or "Exclude" or "Flag")))
        {
            yield return new DomainError(
                "analysis.rule.invalid",
                $"Rule '{rule.Id}' has invalid weight or effects.");
        }
    }

    private static RuleApplicability ToApplicability(RuleConditionResult result) =>
        result switch
        {
            RuleConditionResult.Matched => RuleApplicability.Applicable,
            RuleConditionResult.NotMatched => RuleApplicability.NotApplicable,
            _ => RuleApplicability.Unknown
        };

    private static IEnumerable<RuleCondition> EnumeratePredicates(RuleCondition? condition)
    {
        if (condition is null || condition.Children is null)
        {
            yield break;
        }

        if (condition.Kind == RuleConditionKind.Predicate)
        {
            yield return condition;
        }

        foreach (var child in condition.Children)
        {
            foreach (var predicate in EnumeratePredicates(child))
            {
                yield return predicate;
            }
        }
    }

    private sealed record RelationConditionEvaluation(
        RuleConditionResult Result,
        IReadOnlyList<AnalysisFactEvidence> Evidence);

    private static JsonSerializerOptions CreateFingerprintOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
