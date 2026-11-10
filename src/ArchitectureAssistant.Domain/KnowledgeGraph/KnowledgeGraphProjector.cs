using System.Text.Json;
using System.Text.Json.Serialization;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Domain.KnowledgeGraph;

public sealed class KnowledgeGraphProjector
{
    private static readonly HashSet<string> NodeKinds =
        new(["Architecture", "Pattern", "Technology"], StringComparer.Ordinal);

    private static readonly HashSet<string> RelationTypes =
        new(["DependsOn", "Recommends", "ConflictsWith", "AlternativeTo"], StringComparer.Ordinal);

    private static readonly JsonSerializerOptions ConditionOptions = CreateConditionOptions();

    public Result<KnowledgeGraphProjection> Project(
        string catalogHash,
        IReadOnlyList<KnowledgeGraphItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogHash);
        ArgumentNullException.ThrowIfNull(items);

        var graphItems = items
            .Where(item => NodeKinds.Contains(item.Kind))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var errors = new List<DomainError>();
        if (items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != items.Count)
        {
            errors.Add(new DomainError(
                "knowledge.graph.duplicate_item",
                "The knowledge catalog contains duplicate item IDs."));
            return Result<KnowledgeGraphProjection>.Failure(errors);
        }

        var itemsById = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var graphItemsById = graphItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var declarations = new List<KnowledgeGraphRelationDeclarationView>();
        foreach (var item in graphItems)
        {
            for (var index = 0; index < item.Relations.Count; index++)
            {
                var relation = item.Relations[index];
                var path = $"{item.KnowledgeSource} ({item.Id}) /relations/{index}";
                if (!RelationTypes.Contains(relation.Type))
                {
                    errors.Add(new DomainError(
                        "knowledge.graph.relation_type_invalid",
                        $"{path}/type: relation type '{relation.Type}' is not supported."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(relation.Rationale))
                {
                    errors.Add(new DomainError(
                        "knowledge.graph.rationale_required",
                        $"{path}/rationale: relation rationale is required."));
                }

                if (!itemsById.TryGetValue(relation.TargetId, out var target))
                {
                    errors.Add(new DomainError(
                        "knowledge.graph.reference_missing",
                        $"{path}/targetId: target '{relation.TargetId}' does not exist."));
                    continue;
                }

                if (item.Id == target.Id)
                {
                    errors.Add(new DomainError(
                        "knowledge.graph.self_relation",
                        $"{path}/targetId: a relation cannot target its own item."));
                    continue;
                }

                if (!graphItemsById.ContainsKey(target.Id))
                {
                    errors.Add(new DomainError(
                        "knowledge.graph.endpoint_kind_invalid",
                        $"{path}/targetId: target '{target.Id}' has kind '{target.Kind}', outside this graph."));
                    continue;
                }

                var conditionErrors = RuleConditionEvaluator.Validate(
                    relation.AppliesWhen,
                    $"{path}/appliesWhen");
                if (relation.AppliesWhen is not null && conditionErrors.Count > 0)
                {
                    errors.AddRange(conditionErrors.Select(error =>
                        new DomainError("knowledge.graph.condition_invalid", error)));
                    continue;
                }

                var conditionKey = GetConditionKey(relation.AppliesWhen);
                var relationKey = GetRelationKey(item.Id, relation.Type, target.Id, conditionKey);
                declarations.Add(new KnowledgeGraphRelationDeclarationView(
                    relationKey,
                    relation.Type,
                    item.Id,
                    target.Id,
                    conditionKey,
                    relation.AppliesWhen,
                    relation.Rationale,
                    relation.Source ?? item.Source,
                    relation.KnowledgeSource));
            }
        }

        foreach (var duplicate in declarations
                     .GroupBy(declaration => declaration.RelationKey, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add(new DomainError(
                "knowledge.graph.relation_duplicate",
                $"Relation '{duplicate.Key}' is declared more than once."));
        }

        var normalizedRelations = NormalizeSymmetricRelations(declarations, errors, out var warnings);
        ValidateDependencyCycles(graphItems.Select(item => item.Id), declarations, errors);
        if (errors.Count > 0)
        {
            return Result<KnowledgeGraphProjection>.Failure(errors);
        }

        var nodes = graphItems
            .Select(item => new KnowledgeGraphNode(
                item.Id,
                item.Kind,
                item.Name,
                item.Version,
                item.Source,
                item.KnowledgeSource))
            .ToArray();
        var relations = normalizedRelations
            .Select(relation => new KnowledgeGraphRelation(
                relation.Primary.RelationKey,
                relation.Primary.SourceId,
                relation.Primary.TargetId,
                relation.Primary.Type,
                relation.Primary.ConditionKey,
                graphItemsById[relation.Primary.SourceId].Version,
                graphItemsById[relation.Primary.TargetId].Version,
                relation.Declarations))
            .OrderBy(relation => relation.Type, StringComparer.Ordinal)
            .ThenBy(relation => relation.TargetId, StringComparer.Ordinal)
            .ThenBy(relation => relation.RelationKey, StringComparer.Ordinal)
            .ToArray();

        return Result<KnowledgeGraphProjection>.Success(new KnowledgeGraphProjection(
            "1.0",
            catalogHash,
            Array.AsReadOnly(nodes),
            Array.AsReadOnly(relations),
            Array.AsReadOnly(warnings.Order(StringComparer.Ordinal).ToArray())));
    }

    private static IReadOnlyList<NormalizedRelation> NormalizeSymmetricRelations(
        IReadOnlyList<KnowledgeGraphRelationDeclarationView> declarations,
        ICollection<DomainError> errors,
        out IReadOnlyList<string> warnings)
    {
        var warningMessages = new SortedSet<string>(StringComparer.Ordinal);
        var normalized = new List<NormalizedRelation>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in declarations
                     .GroupBy(declaration => (
                         Type: declaration.Type,
                         First: StringComparer.Ordinal.Compare(declaration.SourceId, declaration.TargetId) < 0
                             ? declaration.SourceId
                             : declaration.TargetId,
                         Second: StringComparer.Ordinal.Compare(declaration.SourceId, declaration.TargetId) < 0
                             ? declaration.TargetId
                             : declaration.SourceId))
                     .OrderBy(group => group.Key.Type, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.First, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.Second, StringComparer.Ordinal))
        {
            var groupItems = group.OrderBy(item => item.RelationKey, StringComparer.Ordinal).ToArray();
            if (group.Key.Type is not ("ConflictsWith" or "AlternativeTo"))
            {
                foreach (var declaration in groupItems)
                {
                    normalized.Add(new NormalizedRelation(declaration, [declaration]));
                    consumed.Add(declaration.RelationKey);
                }

                continue;
            }

            var forward = groupItems.Where(item =>
                item.SourceId == group.Key.First && item.TargetId == group.Key.Second).ToArray();
            var reverse = groupItems.Where(item =>
                item.SourceId == group.Key.Second && item.TargetId == group.Key.First).ToArray();
            if (forward.Length > 0 && reverse.Length > 0)
            {
                var forwardConditions = forward.Select(item => item.ConditionKey).ToHashSet(StringComparer.Ordinal);
                var reverseConditions = reverse.Select(item => item.ConditionKey).ToHashSet(StringComparer.Ordinal);
                if (!forwardConditions.SetEquals(reverseConditions))
                {
                    errors.Add(new DomainError(
                        "knowledge.graph.symmetric_relation_mismatch",
                        $"{group.Key.Type} between '{group.Key.First}' and '{group.Key.Second}' has reciprocal declarations with different conditions."));
                    continue;
                }

                foreach (var conditionKey in forwardConditions.Order(StringComparer.Ordinal))
                {
                    var matching = forward.Concat(reverse)
                        .Where(item => item.ConditionKey == conditionKey)
                        .OrderBy(item => item.RelationKey, StringComparer.Ordinal)
                        .ToArray();
                    var primary = matching[0];
                    normalized.Add(new NormalizedRelation(primary, matching));
                    foreach (var declaration in matching)
                    {
                        consumed.Add(declaration.RelationKey);
                    }

                    if (matching.Length > 1)
                    {
                        warningMessages.Add(
                            $"Reciprocal {group.Key.Type} declarations for '{group.Key.First}' and '{group.Key.Second}' were canonicalized.");
                    }
                }
            }

            foreach (var declaration in groupItems.Where(item => !consumed.Contains(item.RelationKey)))
            {
                normalized.Add(new NormalizedRelation(declaration, [declaration]));
                consumed.Add(declaration.RelationKey);
            }
        }

        warnings = warningMessages.ToArray();
        return normalized;
    }

    private static void ValidateDependencyCycles(
        IEnumerable<string> nodeIds,
        IEnumerable<KnowledgeGraphRelationDeclarationView> declarations,
        ICollection<DomainError> errors)
    {
        var adjacency = declarations
            .Where(declaration => declaration.Type == "DependsOn")
            .GroupBy(declaration => declaration.SourceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.TargetId, StringComparer.Ordinal)
                    .ThenBy(item => item.RelationKey, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var reportedCycles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nodeId in nodeIds.Order(StringComparer.Ordinal))
        {
            Visit(nodeId);
        }

        void Visit(string nodeId)
        {
            if (states.TryGetValue(nodeId, out var state))
            {
                if (state == 2)
                {
                    return;
                }

                if (state == 1)
                {
                    var start = stack.IndexOf(nodeId);
                    var cycle = stack.Skip(start).Append(nodeId).ToArray();
                    var canonicalCycle = string.Join(" -> ", cycle);
                    if (reportedCycles.Add(canonicalCycle))
                    {
                        var declarationsInCycle = Enumerable.Range(0, cycle.Length - 1)
                            .Select(index => adjacency[cycle[index]].First(edge =>
                                edge.TargetId == cycle[index + 1]))
                            .Select(edge =>
                                $"{edge.KnowledgeSource} ({edge.SourceId})/relations [{edge.RelationKey}]");
                        errors.Add(new DomainError(
                            "knowledge.graph.dependency_cycle",
                            $"DependsOn cycle detected: {canonicalCycle}. Declarations: {string.Join("; ", declarationsInCycle)}."));
                    }

                    return;
                }
            }

            states[nodeId] = 1;
            stack.Add(nodeId);
            if (adjacency.TryGetValue(nodeId, out var targets))
            {
                foreach (var target in targets)
                {
                    Visit(target.TargetId);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            states[nodeId] = 2;
        }
    }

    private static string GetConditionKey(RuleCondition? condition) =>
        condition is null
            ? "always"
            : JsonSerializer.Serialize(condition, ConditionOptions);

    private static string GetRelationKey(
        string sourceId,
        string type,
        string targetId,
        string conditionKey) =>
        $"{sourceId}/{type}/{targetId}/{conditionKey}";

    private static JsonSerializerOptions CreateConditionOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record NormalizedRelation(
        KnowledgeGraphRelationDeclarationView Primary,
        IReadOnlyList<KnowledgeGraphRelationDeclarationView> Declarations);
}
