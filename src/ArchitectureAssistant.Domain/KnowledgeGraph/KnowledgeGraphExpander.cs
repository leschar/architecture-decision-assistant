using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.KnowledgeGraph;

public sealed class KnowledgeGraphExpander
{
    public const int MaximumDependencyDepth = 32;
    public const int MaximumPathCount = 5_000;

    private readonly RuleConditionEvaluator _conditionEvaluator = new();

    public Result<KnowledgeGraphExpansion> Expand(
        Project project,
        IReadOnlyList<BasicAnalysisCandidate> candidates,
        KnowledgeGraphProjection projection)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(projection);

        var errors = new List<DomainError>();
        if (candidates.Select(candidate => candidate.Id)
            .Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            errors.Add(new DomainError(
                "knowledge.graph.candidate_duplicate",
                "Dependency expansion candidates contain duplicate IDs."));
        }

        if (projection.Nodes.Select(node => node.ItemId)
            .Distinct(StringComparer.Ordinal).Count() != projection.Nodes.Count)
        {
            errors.Add(new DomainError(
                "knowledge.graph.node_duplicate",
                "Dependency expansion projection contains duplicate node IDs."));
        }

        if (errors.Count > 0)
        {
            return Result<KnowledgeGraphExpansion>.Failure(errors);
        }

        var candidatesById = candidates.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var nodesById = projection.Nodes.ToDictionary(node => node.ItemId, StringComparer.Ordinal);
        var acceptedIds = project.Decisions
            .Where(decision => decision.Status == DecisionStatus.Accepted)
            .Select(decision => decision.SelectedAlternativeId)
            .ToHashSet(StringComparer.Ordinal);
        if (projection.Nodes.Count > 200)
        {
            errors.Add(new DomainError(
                "knowledge.graph.node_limit_exceeded",
                "The knowledge graph exceeds the supported limit of 200 nodes."));
        }

        foreach (var node in projection.Nodes)
        {
            if (!candidatesById.TryGetValue(node.ItemId, out var candidate) ||
                candidate.Kind != node.Kind ||
                candidate.Version != node.Version)
            {
                errors.Add(new DomainError(
                    "knowledge.graph.candidate_mismatch",
                    $"Graph node '{node.ItemId}' does not match an analysis candidate."));
            }
        }

        foreach (var relation in projection.Relations)
        {
            if (relation.Type == "DependsOn" &&
                (relation.Declarations.Count == 0 ||
                 !nodesById.ContainsKey(relation.SourceId) ||
                 !nodesById.ContainsKey(relation.TargetId) ||
                 !candidatesById.ContainsKey(relation.SourceId) ||
                 !candidatesById.ContainsKey(relation.TargetId) ||
                 relation.Declarations.Any(declaration =>
                     declaration.Type != "DependsOn" ||
                     !nodesById.ContainsKey(declaration.SourceId) ||
                     !nodesById.ContainsKey(declaration.TargetId) ||
                     !candidatesById.ContainsKey(declaration.SourceId) ||
                     !candidatesById.ContainsKey(declaration.TargetId))))
            {
                errors.Add(new DomainError(
                    "knowledge.graph.dependency_invalid",
                    $"Dependency relation '{relation.RelationKey}' has missing or inconsistent endpoints."));
            }
        }

        if (errors.Count > 0)
        {
            return Result<KnowledgeGraphExpansion>.Failure(errors);
        }

        var roots = candidates
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(candidate => new KnowledgeGraphExpansionRoot(
                candidate.Id,
                acceptedIds.Contains(candidate.Id) ? "ConfirmedSelection" : "RecommendationCandidate"))
            .ToArray();
        var nodeRoles = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var findings = new Dictionary<(string RootId, string RelationKey), MutableFinding>();
        var warnings = new SortedSet<string>(StringComparer.Ordinal);
        var adjacency = projection.Relations
            .Where(relation => relation.Type == "DependsOn")
            .GroupBy(relation => relation.SourceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(relation => relation.TargetId, StringComparer.Ordinal)
                    .ThenBy(relation => relation.RelationKey, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        foreach (var root in roots)
        {
            AddRole(root.ItemId, "Root");
        }

        var evaluatedPathCount = 0;
        var truncated = false;
        foreach (var root in roots)
        {
            var queue = new Queue<DependencyPath>();
            queue.Enqueue(new DependencyPath(root.ItemId, [], [root.ItemId]));

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                if (!adjacency.TryGetValue(path.NodeId, out var relations))
                {
                    continue;
                }

                if (path.RelationKeys.Count >= MaximumDependencyDepth)
                {
                    truncated = true;
                    warnings.Add(
                        $"Dependency expansion from root '{root.ItemId}' reached the maximum depth of {MaximumDependencyDepth}.");
                    continue;
                }

                foreach (var relation in relations)
                {
                    if (evaluatedPathCount >= MaximumPathCount)
                    {
                        truncated = true;
                        warnings.Add(
                            $"Dependency expansion stopped at the global limit of {MaximumPathCount} evaluated relation paths.");
                        goto ExpansionComplete;
                    }

                    evaluatedPathCount++;
                    if (path.NodeIds.Contains(relation.TargetId, StringComparer.Ordinal))
                    {
                        truncated = true;
                        warnings.Add(
                            $"Runtime cycle guard stopped relation '{relation.RelationKey}' on path from root '{root.ItemId}'.");
                        continue;
                    }

                    var unknownFacts = new SortedSet<string>(StringComparer.Ordinal);
                    var applicable = RuleConditionResult.Matched;
                    foreach (var declaration in relation.Declarations
                                 .OrderBy(item => item.RelationKey, StringComparer.Ordinal))
                    {
                        if (declaration.AppliesWhen is null)
                        {
                            applicable = RuleConditionResult.Matched;
                            break;
                        }

                        var evidence = new List<AnalysisFactEvidence>();
                        var declarationResult = _conditionEvaluator.Evaluate(
                            declaration.AppliesWhen,
                            project,
                            candidatesById[declaration.SourceId],
                            "Applicability",
                            evidence);
                        if (declarationResult == RuleConditionResult.Matched)
                        {
                            applicable = RuleConditionResult.Matched;
                            break;
                        }

                        if (declarationResult == RuleConditionResult.Unknown)
                        {
                            applicable = RuleConditionResult.Unknown;
                            foreach (var fact in evidence.Where(item => item.Status == "Unknown")
                                         .Select(item => item.Fact))
                            {
                                unknownFacts.Add(fact);
                            }
                        }
                        else if (applicable != RuleConditionResult.Unknown)
                        {
                            applicable = RuleConditionResult.NotMatched;
                        }
                    }

                    var representative = relation.Declarations
                        .OrderBy(item => item.RelationKey, StringComparer.Ordinal)
                        .First();
                    var finding = applicable switch
                    {
                        RuleConditionResult.Matched =>
                            acceptedIds.Contains(relation.TargetId) ? "Satisfied" : "Missing",
                        RuleConditionResult.NotMatched => "NotEvaluated",
                        _ => "Unknown"
                    };
                    var applicability = applicable switch
                    {
                        RuleConditionResult.Matched => "Applicable",
                        RuleConditionResult.NotMatched => "NotApplicable",
                        _ => "Unknown"
                    };
                    var relationPath = path.RelationKeys.Append(relation.RelationKey).ToArray();
                    var key = (root.ItemId, relation.RelationKey);
                    if (!findings.TryGetValue(key, out var mutableFinding))
                    {
                        mutableFinding = new MutableFinding(
                            root.ItemId,
                            relation,
                            finding,
                            applicability,
                            representative.Rationale,
                            representative.Source,
                            representative.KnowledgeSource,
                            Array.AsReadOnly(unknownFacts.ToArray()));
                        findings.Add(key, mutableFinding);
                    }

                    mutableFinding.AddPath(relationPath);
                    if (applicable == RuleConditionResult.Matched)
                    {
                        AddRole(relation.TargetId, "Required");
                        queue.Enqueue(new DependencyPath(
                            relation.TargetId,
                            relationPath,
                            path.NodeIds.Append(relation.TargetId).ToArray()));
                    }
                    else if (applicable == RuleConditionResult.Unknown)
                    {
                        AddRole(relation.TargetId, "PotentialRequired");
                    }
                }
            }
        }

    ExpansionComplete:
        var expansionNodes = nodeRoles
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KnowledgeGraphExpansionNode(
                pair.Key,
                Array.AsReadOnly(pair.Value.ToArray()),
                acceptedIds.Contains(pair.Key) ? "Confirmed" : "NotSelected"))
            .ToArray();
        var dependencyFindings = findings.Values
            .OrderBy(finding => finding.RootId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Relation.TargetId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Relation.RelationKey, StringComparer.Ordinal)
            .Select(finding => new KnowledgeGraphDependencyFinding(
                finding.RootId,
                finding.Relation.RelationKey,
                finding.Relation.SourceId,
                finding.Relation.TargetId,
                finding.Applicability,
                finding.Status,
                finding.Rationale,
                finding.Relation.SourceVersion,
                finding.Relation.TargetVersion,
                finding.KnowledgeSource,
                finding.Source,
                finding.UnknownFacts,
                Array.AsReadOnly(finding.Paths
                    .OrderBy(path => string.Join("\0", path), StringComparer.Ordinal)
                    .Select(path => (IReadOnlyList<string>)Array.AsReadOnly(path.ToArray()))
                    .ToArray())))
            .ToArray();

        return Result<KnowledgeGraphExpansion>.Success(new KnowledgeGraphExpansion(
            "1.0",
            projection.CatalogHash,
            Array.AsReadOnly(roots),
            Array.AsReadOnly(expansionNodes),
            Array.AsReadOnly(dependencyFindings),
            truncated,
            MaximumDependencyDepth,
            MaximumPathCount,
            evaluatedPathCount,
            Array.AsReadOnly(warnings.ToArray())));

        void AddRole(string itemId, string role)
        {
            if (!nodeRoles.TryGetValue(itemId, out var roles))
            {
                roles = new SortedSet<string>(StringComparer.Ordinal);
                nodeRoles.Add(itemId, roles);
            }

            roles.Add(role);
        }
    }

    private sealed record DependencyPath(
        string NodeId,
        IReadOnlyList<string> RelationKeys,
        IReadOnlyList<string> NodeIds);

    private sealed class MutableFinding(
        string rootId,
        KnowledgeGraphRelation relation,
        string status,
        string applicability,
        string rationale,
        string? source,
        string knowledgeSource,
        IReadOnlyList<string> unknownFacts)
    {
        private readonly List<IReadOnlyList<string>> _paths = [];
        private readonly HashSet<string> _pathKeys = new(StringComparer.Ordinal);

        public string RootId { get; } = rootId;

        public KnowledgeGraphRelation Relation { get; } = relation;

        public string Status { get; } = status;

        public string Applicability { get; } = applicability;

        public string Rationale { get; } = rationale;

        public string? Source { get; } = source;

        public string KnowledgeSource { get; } = knowledgeSource;

        public IReadOnlyList<string> UnknownFacts { get; } = unknownFacts;

        public IReadOnlyList<IReadOnlyList<string>> Paths => _paths;

        public void AddPath(IReadOnlyList<string> path)
        {
            var key = string.Join("\0", path);
            if (_pathKeys.Add(key))
            {
                _paths.Add(path);
            }
        }
    }
}
