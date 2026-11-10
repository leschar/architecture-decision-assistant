namespace ArchitectureAssistant.Domain.Analysis;

public static class RuleFactRegistry
{
    private static readonly IReadOnlySet<RuleOperator> OrderedOperators =
        new HashSet<RuleOperator>
        {
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.GreaterThan,
            RuleOperator.GreaterThanOrEqual,
            RuleOperator.LessThan,
            RuleOperator.LessThanOrEqual,
            RuleOperator.In
        };

    private static readonly IReadOnlySet<RuleOperator> StringOperators =
        new HashSet<RuleOperator>
        {
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.Contains,
            RuleOperator.In
        };

    private static readonly IReadOnlySet<RuleOperator> ArrayOperators =
        new HashSet<RuleOperator>
        {
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.Contains,
            RuleOperator.In
        };

    private static readonly IReadOnlySet<RuleOperator> ExistenceOperators =
        new HashSet<RuleOperator> { RuleOperator.Exists, RuleOperator.NotExists };

    private static readonly IReadOnlyDictionary<string, RuleFactDefinition> Definitions =
        new Dictionary<string, RuleFactDefinition>(StringComparer.Ordinal)
        {
            ["project.teamSize"] = new("project.teamSize", RuleValueKind.Number, OrderedOperators),
            ["project.scalabilityTarget"] = new("project.scalabilityTarget", RuleValueKind.String, StringOperators),
            ["project.criticality"] = new("project.criticality", RuleValueKind.String, StringOperators),
            ["project.businessDomain"] = new("project.businessDomain", RuleValueKind.String, StringOperators),
            ["project.constraints"] = new("project.constraints", RuleValueKind.Array, ArrayOperators, RuleValueKind.String),
            ["project.confirmedRequirementCount"] = new("project.confirmedRequirementCount", RuleValueKind.Number, OrderedOperators),
            ["project.confirmedFunctionalRequirementCount"] = new("project.confirmedFunctionalRequirementCount", RuleValueKind.Number, OrderedOperators),
            ["project.confirmedQualityRequirementCount"] = new("project.confirmedQualityRequirementCount", RuleValueKind.Number, OrderedOperators),
            ["project.confirmedConstraintRequirementCount"] = new("project.confirmedConstraintRequirementCount", RuleValueKind.Number, OrderedOperators),
            ["candidate.id"] = new("candidate.id", RuleValueKind.String, StringOperators),
            ["candidate.kind"] = new("candidate.kind", RuleValueKind.String, StringOperators),
            ["candidate.tags"] = new("candidate.tags", RuleValueKind.Array, ArrayOperators, RuleValueKind.String),
            ["candidate.category"] = new("candidate.category", RuleValueKind.String, StringOperators),
            ["candidate.complexity"] = new("candidate.complexity", RuleValueKind.Number, OrderedOperators),
            ["candidate.scalability"] = new("candidate.scalability", RuleValueKind.Number, OrderedOperators),
            ["candidate.operationalCost"] = new("candidate.operationalCost", RuleValueKind.Number, OrderedOperators)
        };

    public static bool TryGet(string path, out RuleFactDefinition definition) =>
        Definitions.TryGetValue(path, out definition!);

    public static bool IsSupportedOperator(
        RuleFactDefinition definition,
        RuleOperator @operator,
        RuleValue? value)
    {
        if (ExistenceOperators.Contains(@operator))
        {
            return value is null;
        }

        if (!definition.Operators.Contains(@operator))
        {
            return false;
        }

        if (value is null)
        {
            return false;
        }

        if (@operator == RuleOperator.In)
        {
            var itemKind = definition.ElementKind ?? definition.ValueKind;
            return value.Kind == RuleValueKind.Array &&
                   value.Items is { Count: > 0 } items &&
                   items.All(item => item.Kind == itemKind && IsValidLiteral(item));
        }

        if (@operator == RuleOperator.Contains && definition.ValueKind == RuleValueKind.Array)
        {
            return definition.ElementKind is { } elementKind &&
                   value.Kind == elementKind &&
                   IsValidLiteral(value);
        }

        if (definition.ValueKind == RuleValueKind.Array &&
            @operator is RuleOperator.Equals or RuleOperator.NotEquals)
        {
            return value.Kind == RuleValueKind.Array &&
                   value.Items is not null &&
                   value.Items.All(item =>
                       item.Kind == definition.ElementKind && IsValidLiteral(item));
        }

        return value.Kind == definition.ValueKind && IsValidLiteral(value);
    }

    private static bool IsValidLiteral(RuleValue value) =>
        value.Kind switch
        {
            RuleValueKind.String => value.StringValue is not null &&
                                    value.NumberValue is null &&
                                    value.BooleanValue is null &&
                                    value.Items is null,
            RuleValueKind.Number => value.NumberValue.HasValue &&
                                    value.StringValue is null &&
                                    value.BooleanValue is null &&
                                    value.Items is null,
            RuleValueKind.Boolean => value.BooleanValue.HasValue &&
                                     value.StringValue is null &&
                                     value.NumberValue is null &&
                                     value.Items is null,
            RuleValueKind.Array => value.Items is not null &&
                                   value.StringValue is null &&
                                   value.NumberValue is null &&
                                   value.BooleanValue is null &&
                                   value.Items.All(IsValidLiteral),
            RuleValueKind.Null => value.StringValue is null &&
                                  value.NumberValue is null &&
                                  value.BooleanValue is null &&
                                  value.Items is null,
            _ => false
        };
}
