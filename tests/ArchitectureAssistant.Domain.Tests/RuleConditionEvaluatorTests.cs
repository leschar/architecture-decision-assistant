using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class RuleConditionEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_UsesThreeValuedTruthTablesForNestedConditions()
    {
        var project = CreateProject(teamSize: null);
        var candidate = CreateCandidate(category: "General");
        var evaluator = new RuleConditionEvaluator();
        var evidence = new List<AnalysisFactEvidence>();
        var condition = Group(
            RuleConditionKind.AllOf,
            Predicate("project.teamSize", RuleOperator.GreaterThan, Number(3)),
            Group(
                RuleConditionKind.AnyOf,
                Predicate("candidate.category", RuleOperator.Equals, String("Other")),
                Predicate("candidate.category", RuleOperator.Equals, String("General"))));

        var result = evaluator.Evaluate(condition, project, candidate, "Condition", evidence);

        Assert.Equal(RuleConditionResult.Unknown, result);
        Assert.Equal(3, evidence.Count);
        Assert.Equal("Unknown", evidence[0].Status);

        var negatedUnknown = Group(
            RuleConditionKind.Not,
            Predicate("project.teamSize", RuleOperator.Equals, Number(3)));
        Assert.Equal(
            RuleConditionResult.Unknown,
            evaluator.Evaluate(negatedUnknown, project, candidate, "Condition", []));

        var falseDominatesAllOf = Group(
            RuleConditionKind.AllOf,
            Predicate("project.teamSize", RuleOperator.Equals, Number(3)),
            Predicate("candidate.category", RuleOperator.Equals, String("Elsewhere")));
        Assert.Equal(
            RuleConditionResult.NotMatched,
            evaluator.Evaluate(falseDominatesAllOf, project, candidate, "Condition", []));
    }

    [Fact]
    public void Evaluate_HandlesExistenceAndArrayOperatorsWithoutCoercion()
    {
        var project = CreateProject(teamSize: 4);
        var candidate = CreateCandidate(category: "General");
        var evaluator = new RuleConditionEvaluator();

        Assert.Equal(
            RuleConditionResult.Matched,
            Evaluate(evaluator, Predicate("candidate.category", RuleOperator.Exists, null), project, candidate));
        Assert.Equal(
            RuleConditionResult.NotMatched,
            Evaluate(evaluator, Predicate("candidate.category", RuleOperator.NotExists, null), project, candidate));
        Assert.Equal(
            RuleConditionResult.Matched,
            Evaluate(
                evaluator,
                Predicate("candidate.tags", RuleOperator.Contains, String("distributed")),
                project,
                candidate));
        Assert.Equal(
            RuleConditionResult.Matched,
            Evaluate(
                evaluator,
                Predicate(
                    "candidate.tags",
                    RuleOperator.In,
                    new RuleValue(RuleValueKind.Array, Items: [String("distributed"), String("modular")])),
                project,
                candidate));

        var invalidArrayComparison = Predicate(
            "candidate.tags",
            RuleOperator.Equals,
            new RuleValue(
                RuleValueKind.Array,
                Items: [String("distributed"), Number(2)]));
        Assert.Contains(
            RuleConditionEvaluator.Validate(invalidArrayComparison),
            error => error.Contains("/fact", StringComparison.Ordinal) ||
                     error.Contains("operator/value", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_UsesOptionalScalabilityTargetAsTypedProjectFact()
    {
        var evaluator = new RuleConditionEvaluator();
        var condition = Predicate("project.scalabilityTarget", RuleOperator.Equals, String("High"));

        var unknown = evaluator.Evaluate(
            condition,
            CreateProject(teamSize: null),
            CreateCandidate(category: "General"),
            "Applicability",
            []);
        var matched = evaluator.Evaluate(
            condition,
            CreateProject(teamSize: null, scalabilityTarget: ScalabilityTarget.High),
            CreateCandidate(category: "General"),
            "Applicability",
            []);

        Assert.Equal(RuleConditionResult.Unknown, unknown);
        Assert.Equal(RuleConditionResult.Matched, matched);
    }

    [Fact]
    public void Analyze_RejectsCandidateFactsInApplicabilityConditions()
    {
        var project = CreateProject(teamSize: 4);
        var rule = new BasicAnalysisRule(
            "candidate-applicability",
            "1.0.0",
            "Invalid applicability",
            null,
            "test/rule.json",
            "rule-hash",
            true,
            1,
            ["Architecture"],
            [],
            [],
            Predicate("candidate.category", RuleOperator.Exists, null),
            Predicate("project.teamSize", RuleOperator.GreaterThan, Number(1)),
            [new BasicAnalysisEffect("Flag", false, null, "Warning", "Review the context.")]);

        var result = new BasicAnalysisEngine().Analyze(
            Guid.NewGuid(),
            project,
            "catalog",
            "rules",
            [CreateCandidate("General")],
            [rule],
            Now);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "analysis.rule.invalid");
    }

    private static RuleConditionResult Evaluate(
        RuleConditionEvaluator evaluator,
        RuleCondition condition,
        Project project,
        BasicAnalysisCandidate candidate) =>
        evaluator.Evaluate(condition, project, candidate, "Condition", []);

    private static RuleCondition Predicate(string fact, RuleOperator @operator, RuleValue? value) =>
        new(RuleConditionKind.Predicate, fact, @operator, value, []);

    private static RuleCondition Group(RuleConditionKind kind, params RuleCondition[] children) =>
        new(kind, null, null, null, children);

    private static RuleValue Number(decimal value) =>
        new(RuleValueKind.Number, NumberValue: value);

    private static RuleValue String(string value) =>
        new(RuleValueKind.String, StringValue: value);

    private static BasicAnalysisCandidate CreateCandidate(string category) =>
        new(
            "distributed-services",
            "Architecture",
            "Distributed services",
            "1.0.0",
            "A distributed architecture.",
            null,
            ["modular", "distributed"],
            category,
            5,
            8,
            7,
            [],
            []);

    private static Project CreateProject(int? teamSize, ScalabilityTarget? scalabilityTarget = null)
    {
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing", Now);
        Assert.True(created.TryGetValue(out var project));
        var updatedContext = project.Context.Update(null, teamSize, null, [], scalabilityTarget);
        Assert.True(updatedContext.TryGetValue(out var context));
        var updated = project.UpdateContext(context, Now);
        Assert.True(updated.TryGetValue(out var revisedProject));
        return revisedProject;
    }
}
