namespace ArchitectureAssistant.Domain.Analysis;

public sealed record RuleCondition(
    RuleConditionKind Kind,
    string? Fact,
    RuleOperator? Operator,
    RuleValue? Value,
    IReadOnlyList<RuleCondition> Children);

public sealed record RuleValue(
    RuleValueKind Kind,
    string? StringValue = null,
    decimal? NumberValue = null,
    bool? BooleanValue = null,
    IReadOnlyList<RuleValue>? Items = null);

public sealed record RuleFactDefinition(
    string Path,
    RuleValueKind ValueKind,
    IReadOnlySet<RuleOperator> Operators,
    RuleValueKind? ElementKind = null);

public enum RuleConditionKind
{
    Predicate,
    AllOf,
    AnyOf,
    Not
}

public enum RuleValueKind
{
    String,
    Number,
    Boolean,
    Array,
    Null
}

public enum RuleOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    In,
    Exists,
    NotExists
}
