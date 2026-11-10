using System.Globalization;
using System.Text.Json;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Analysis;

public sealed class RuleConditionEvaluator
{
    private const int MaximumDepth = 16;
    private const int MaximumNodeCount = 64;

    public static IReadOnlyList<string> Validate(RuleCondition? condition, string rootPath = "/")
    {
        if (condition is null)
        {
            return ["A rule condition is required."];
        }

        var errors = new List<string>();
        var nodeCount = 0;
        ValidateNode(condition, rootPath, 1, ref nodeCount, errors);
        return errors;
    }

    public RuleConditionResult Evaluate(
        RuleCondition condition,
        Project project,
        BasicAnalysisCandidate candidate,
        string phase,
        ICollection<AnalysisFactEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(evidence);

        return EvaluateNode(condition, project, candidate, phase, evidence);
    }

    private static void ValidateNode(
        RuleCondition condition,
        string path,
        int depth,
        ref int nodeCount,
        ICollection<string> errors)
    {
        if (condition is null)
        {
            errors.Add($"{path}: condition is required.");
            return;
        }

        nodeCount++;
        if (depth > MaximumDepth)
        {
            errors.Add($"{path}: rule condition nesting exceeds {MaximumDepth} levels.");
            return;
        }

        if (nodeCount > MaximumNodeCount)
        {
            errors.Add($"{path}: rule condition exceeds {MaximumNodeCount} nodes.");
            return;
        }

        if (condition.Kind == RuleConditionKind.Predicate)
        {
            if (condition.Children is null || condition.Children.Count != 0)
            {
                errors.Add($"{path}: a predicate cannot contain child conditions.");
            }

            if (string.IsNullOrWhiteSpace(condition.Fact) ||
                !RuleFactRegistry.TryGet(condition.Fact, out var definition))
            {
                errors.Add($"{path}/fact: fact '{condition.Fact}' is not supported.");
                return;
            }

            if (condition.Operator is null ||
                !RuleFactRegistry.IsSupportedOperator(definition, condition.Operator.Value, condition.Value))
            {
                errors.Add($"{path}: operator/value is incompatible with fact '{condition.Fact}'.");
            }

            return;
        }

        if (condition.Children is null)
        {
            errors.Add($"{path}: child conditions are required.");
            return;
        }

        if (condition.Fact is not null || condition.Operator is not null || condition.Value is not null)
        {
            errors.Add($"{path}: a logical condition cannot define predicate fields.");
        }

        if (condition.Kind == RuleConditionKind.Not && condition.Children.Count != 1)
        {
            errors.Add($"{path}/not: the 'not' condition must contain exactly one child.");
            return;
        }

        if (condition.Kind is RuleConditionKind.AllOf or RuleConditionKind.AnyOf &&
            condition.Children.Count == 0)
        {
            errors.Add($"{path}: logical condition groups must contain at least one child.");
            return;
        }

        if (condition.Kind is not (RuleConditionKind.AllOf or RuleConditionKind.AnyOf or RuleConditionKind.Not))
        {
            errors.Add($"{path}: condition kind '{condition.Kind}' is not supported.");
            return;
        }

        var groupName = condition.Kind switch
        {
            RuleConditionKind.AllOf => "allOf",
            RuleConditionKind.AnyOf => "anyOf",
            _ => "not"
        };
        for (var index = 0; index < condition.Children.Count; index++)
        {
            ValidateNode(
                condition.Children[index],
                $"{path}/{groupName}/{index}",
                depth + 1,
                ref nodeCount,
                errors);
        }
    }

    private static RuleConditionResult EvaluateNode(
        RuleCondition condition,
        Project project,
        BasicAnalysisCandidate candidate,
        string phase,
        ICollection<AnalysisFactEvidence> evidence)
    {
        if (condition.Kind == RuleConditionKind.Predicate)
        {
            var actual = ResolveFact(condition.Fact!, project, candidate);
            var result = actual is null
                ? RuleConditionResult.Unknown
                : EvaluatePredicate(actual, condition.Operator!.Value, condition.Value!);
            evidence.Add(new AnalysisFactEvidence(
                condition.Fact!,
                phase,
                condition.Operator.ToString(),
                condition.Value is null ? null : Format(condition.Value),
                actual is null ? null : Format(actual),
                actual is null ? "Unknown" : "Confirmed"));
            return result;
        }

        var results = condition.Children
            .Select(child => EvaluateNode(child, project, candidate, phase, evidence))
            .ToArray();
        return condition.Kind switch
        {
            RuleConditionKind.AllOf => results.Contains(RuleConditionResult.NotMatched)
                ? RuleConditionResult.NotMatched
                : results.All(result => result == RuleConditionResult.Matched)
                    ? RuleConditionResult.Matched
                    : RuleConditionResult.Unknown,
            RuleConditionKind.AnyOf => results.Contains(RuleConditionResult.Matched)
                ? RuleConditionResult.Matched
                : results.All(result => result == RuleConditionResult.NotMatched)
                    ? RuleConditionResult.NotMatched
                    : RuleConditionResult.Unknown,
            RuleConditionKind.Not => results[0] switch
            {
                RuleConditionResult.Matched => RuleConditionResult.NotMatched,
                RuleConditionResult.NotMatched => RuleConditionResult.Matched,
                _ => RuleConditionResult.Unknown
            },
            _ => RuleConditionResult.NotEvaluated
        };
    }

    private static RuleValue? ResolveFact(
        string path,
        Project project,
        BasicAnalysisCandidate candidate)
    {
        if (path == "project.teamSize")
        {
            return project.Context.TeamSize is int teamSize
                ? Number(teamSize)
                : null;
        }

        if (path == "project.scalabilityTarget")
        {
            return project.Context.ScalabilityTarget is { } scalabilityTarget
                ? String(scalabilityTarget.ToString())
                : null;
        }

        if (path == "project.criticality")
        {
            return project.Context.Criticality is { } criticality
                ? String(criticality.ToString())
                : null;
        }

        if (path == "project.businessDomain")
        {
            return string.IsNullOrWhiteSpace(project.Context.BusinessDomain)
                ? null
                : String(project.Context.BusinessDomain);
        }

        if (path == "project.constraints")
        {
            return Array(project.Context.Constraints
                .Order(StringComparer.Ordinal)
                .Select(String));
        }

        var confirmedRequirements = project.Requirements
            .Where(requirement => requirement.Status == RequirementStatus.Confirmed)
            .ToArray();
        if (path == "project.confirmedRequirementCount")
        {
            return Number(confirmedRequirements.Length);
        }

        if (path == "project.confirmedFunctionalRequirementCount")
        {
            return Number(confirmedRequirements.Count(requirement =>
                requirement.Classification == RequirementClassification.Functional));
        }

        if (path == "project.confirmedQualityRequirementCount")
        {
            return Number(confirmedRequirements.Count(requirement =>
                requirement.Classification == RequirementClassification.Quality));
        }

        if (path == "project.confirmedConstraintRequirementCount")
        {
            return Number(confirmedRequirements.Count(requirement =>
                requirement.Classification == RequirementClassification.Constraint));
        }

        if (path == "candidate.id")
        {
            return String(candidate.Id);
        }

        if (path == "candidate.kind")
        {
            return String(candidate.Kind);
        }

        if (path == "candidate.tags")
        {
            return Array(candidate.Tags
                .Order(StringComparer.Ordinal)
                .Select(String));
        }

        if (path == "candidate.category")
        {
            return string.IsNullOrWhiteSpace(candidate.Category)
                ? null
                : String(candidate.Category);
        }

        if (path == "candidate.complexity")
        {
            return candidate.Complexity is int complexity ? Number(complexity) : null;
        }

        if (path == "candidate.scalability")
        {
            return candidate.Scalability is int scalability ? Number(scalability) : null;
        }

        if (path == "candidate.operationalCost")
        {
            return candidate.OperationalCost is int cost ? Number(cost) : null;
        }

        return null;
    }

    private static RuleConditionResult EvaluatePredicate(
        RuleValue actual,
        RuleOperator @operator,
        RuleValue? expected)
    {
        var matches = @operator switch
        {
            RuleOperator.Equals => Compare(actual, expected!) == 0,
            RuleOperator.NotEquals => Compare(actual, expected!) != 0,
            RuleOperator.GreaterThan => Compare(actual, expected!) > 0,
            RuleOperator.GreaterThanOrEqual => Compare(actual, expected!) >= 0,
            RuleOperator.LessThan => Compare(actual, expected!) < 0,
            RuleOperator.LessThanOrEqual => Compare(actual, expected!) <= 0,
            RuleOperator.Contains => Contains(actual, expected!),
            RuleOperator.In => IsIn(actual, expected!),
            RuleOperator.Exists => true,
            RuleOperator.NotExists => false,
            _ => false
        };
        return matches ? RuleConditionResult.Matched : RuleConditionResult.NotMatched;
    }

    private static int Compare(RuleValue actual, RuleValue expected)
    {
        if (actual.Kind == RuleValueKind.Array)
        {
            return SequenceEqual(actual.Items!, expected.Items!) ? 0 : 1;
        }

        return actual.Kind switch
        {
            RuleValueKind.String => string.CompareOrdinal(actual.StringValue, expected.StringValue),
            RuleValueKind.Number => actual.NumberValue!.Value.CompareTo(expected.NumberValue!.Value),
            RuleValueKind.Boolean => actual.BooleanValue!.Value.CompareTo(expected.BooleanValue!.Value),
            _ => 1
        };
    }

    private static bool Contains(RuleValue actual, RuleValue expected)
    {
        if (actual.Kind == RuleValueKind.String)
        {
            return actual.StringValue!.Contains(expected.StringValue!, StringComparison.Ordinal);
        }

        return actual.Items!.Any(item => Compare(item, expected) == 0);
    }

    private static bool IsIn(RuleValue actual, RuleValue expected) =>
        expected.Items!.Any(item => actual.Kind == RuleValueKind.Array
            ? actual.Items!.Any(actualItem => Compare(actualItem, item) == 0)
            : Compare(actual, item) == 0);

    private static bool SequenceEqual(IReadOnlyList<RuleValue> left, IReadOnlyList<RuleValue> right) =>
        left.Count == right.Count &&
        left.OrderBy(Format, StringComparer.Ordinal)
            .SequenceEqual(right.OrderBy(Format, StringComparer.Ordinal));

    private static RuleValue String(string value) => new(RuleValueKind.String, StringValue: value);

    private static RuleValue Number(decimal value) => new(RuleValueKind.Number, NumberValue: value);

    private static RuleValue Array(IEnumerable<RuleValue> values) =>
        new(RuleValueKind.Array, Items: values.ToArray());

    private static string Format(RuleValue value) =>
        value.Kind switch
        {
            RuleValueKind.String => value.StringValue!,
            RuleValueKind.Number => value.NumberValue!.Value.ToString(CultureInfo.InvariantCulture),
            RuleValueKind.Boolean => value.BooleanValue!.Value ? "true" : "false",
            RuleValueKind.Null => "null",
            RuleValueKind.Array => JsonSerializer.Serialize(value.Items!.Select(Format)),
            _ => string.Empty
        };
}
