using System.Text.Json;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class BasicAnalysisEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AnalysisId = Guid.Parse("936c032d-0c1e-49d5-907b-95f217e0d144");

    [Fact]
    public void Analyze_IsDeterministicAndKeepsCandidateKindsSeparateWithEvidence()
    {
        var project = CreateProject(teamSize: 4);
        var engine = new BasicAnalysisEngine();
        var candidates = new[]
        {
            Candidate("rabbitmq", "Technology"),
            Candidate("microservices", "Architecture"),
            Candidate("saga", "Pattern"),
            Candidate("monolith", "Architecture")
        };
        var rules = new[] { SmallTeamRule() };

        var first = engine.Analyze(AnalysisId, project, "catalog-a", "rules-a", candidates, rules, Now);
        var second = engine.Analyze(
            AnalysisId,
            project,
            "catalog-a",
            "rules-a",
            candidates.Reverse().ToArray(),
            rules.Reverse().ToArray(),
            Now);

        Assert.True(first.TryGetValue(out var firstSnapshot));
        Assert.True(second.TryGetValue(out var secondSnapshot));
        Assert.Equal(
            JsonSerializer.Serialize(firstSnapshot),
            JsonSerializer.Serialize(secondSnapshot));

        Assert.Collection(
            firstSnapshot.CandidateGroups,
            group => Assert.Equal("Architecture", group.Kind),
            group => Assert.Equal("Pattern", group.Kind),
            group => Assert.Equal("Technology", group.Kind));
        var architectureGroup = firstSnapshot.CandidateGroups[0];
        Assert.Equal(["microservices", "monolith"], architectureGroup.Candidates.Select(candidate => candidate.CandidateId));

        var microservices = architectureGroup.Candidates[0];
        Assert.Equal(AnalysisEligibility.NotEvaluated, microservices.Eligibility);
        Assert.Equal(20m, microservices.FitScore);
        var evaluation = Assert.Single(microservices.RuleEvaluations);
        Assert.Equal("small-team-microservices-cost", evaluation.RuleId);
        Assert.Equal("Matched", evaluation.ConditionResult.ToString());
        Assert.Equal("built-in/rule-small-team-microservices.json", evaluation.KnowledgeSource);
        Assert.Contains(evaluation.Effects, effect =>
            effect.Type == "Score" && effect.Points == -60 && effect.Applied);
        Assert.Equal(
            new AnalysisFactEvidence("project.teamSize", "Condition", "LessThanOrEqual", "8", "4", "Confirmed"),
            Assert.Single(evaluation.FactEvidence));
        Assert.Equal(1m, microservices.EvidenceCoverage);

        Assert.Null(Assert.Single(firstSnapshot.CandidateGroups[1].Candidates).FitScore);
        Assert.Null(Assert.Single(firstSnapshot.CandidateGroups[2].Candidates).FitScore);
    }

    [Fact]
    public void Analyze_PreservesMissingTeamSizeAsUnknownWithoutNeutralScore()
    {
        var project = CreateProject(teamSize: null);
        var result = new BasicAnalysisEngine().Analyze(
            AnalysisId,
            project,
            "catalog-a",
            "rules-a",
            [Candidate("microservices", "Architecture")],
            [SmallTeamRule()],
            Now);

        Assert.True(result.TryGetValue(out var snapshot));
        var candidate = Assert.Single(snapshot.CandidateGroups[0].Candidates);
        var evaluation = Assert.Single(candidate.RuleEvaluations);

        Assert.Equal(RuleConditionResult.Unknown, evaluation.ConditionResult);
        Assert.Null(candidate.FitScore);
        Assert.Equal(0m, candidate.EvidenceCoverage);
        Assert.Equal(AnalysisEligibility.NotEvaluated, candidate.Eligibility);
        Assert.Contains("project.teamSize", evaluation.UnknownFacts);
        Assert.Equal("Unknown", Assert.Single(evaluation.FactEvidence).Status);
    }

    [Fact]
    public void Analyze_UsesKnownFalseAsZeroContributionAndEvaluatesCandidateFacts()
    {
        var project = CreateProject(teamSize: 12);
        var knownFalseRule = SmallTeamRule();
        var candidateRule = knownFalseRule with
        {
            Id = "scalable-candidate-score",
            Conditions = Predicate(
                "candidate.scalability",
                RuleOperator.GreaterThanOrEqual,
                Number(8)),
            Effects = [new BasicAnalysisEffect(
                "Score",
                false,
                40,
                null,
                "The candidate meets the scalability threshold.")]
        };
        var result = new BasicAnalysisEngine().Analyze(
            AnalysisId,
            project,
            "catalog-a",
            "rules-a",
            [Candidate("microservices", "Architecture")],
            [knownFalseRule, candidateRule],
            Now);

        Assert.True(result.TryGetValue(out var snapshot));
        var candidate = Assert.Single(snapshot.CandidateGroups[0].Candidates);
        Assert.Equal(60m, candidate.FitScore);
        var knownFalseEvaluation = Assert.Single(candidate.RuleEvaluations, evaluation =>
            evaluation.RuleId == knownFalseRule.Id);
        var candidateEvaluation = Assert.Single(candidate.RuleEvaluations, evaluation =>
            evaluation.RuleId == candidateRule.Id);
        Assert.Equal(RuleConditionResult.NotMatched, knownFalseEvaluation.ConditionResult);
        Assert.Equal(RuleConditionResult.Matched, candidateEvaluation.ConditionResult);
        Assert.Contains(candidateEvaluation.FactEvidence, evidence =>
            evidence.Fact == "candidate.scalability" && evidence.Value == "8");
    }

    [Fact]
    public void Analyze_EvaluatesNestedConditionsAndSeparatesApplicabilityEvidence()
    {
        var rule = SmallTeamRule() with
        {
            ApplicabilityCondition = Predicate(
                "project.teamSize",
                RuleOperator.GreaterThan,
                Number(0)),
            Conditions = new RuleCondition(
                RuleConditionKind.AllOf,
                null,
                null,
                null,
                [
                    Predicate("project.teamSize", RuleOperator.LessThanOrEqual, Number(4)),
                    new RuleCondition(
                        RuleConditionKind.AnyOf,
                        null,
                        null,
                        null,
                        [
                            Predicate("candidate.category", RuleOperator.Equals, String("General")),
                            new RuleCondition(
                                RuleConditionKind.Not,
                                null,
                                null,
                                null,
                                [Predicate("candidate.complexity", RuleOperator.GreaterThan, Number(9))])
                        ])
                ])
        };

        var result = new BasicAnalysisEngine().Analyze(
            AnalysisId,
            CreateProject(teamSize: 4),
            "catalog-a",
            "rules-a",
            [Candidate("microservices", "Architecture")],
            [rule],
            Now);

        Assert.True(result.TryGetValue(out var snapshot));
        var evaluation = Assert.Single(Assert.Single(snapshot.CandidateGroups[0].Candidates).RuleEvaluations);
        Assert.Equal(RuleApplicability.Applicable, evaluation.Applicability);
        Assert.Equal(RuleConditionResult.Matched, evaluation.ConditionResult);
        Assert.Equal(
            ["Applicability", "Condition", "Condition", "Condition"],
            evaluation.FactEvidence.Select(evidence => evidence.Phase));
        Assert.All(evaluation.Effects, effect => Assert.True(effect.Applied));
    }

    [Fact]
    public void Analyze_DoesNotApplyUnknownApplicabilityOrHardExclusion()
    {
        var rule = SmallTeamRule() with
        {
            ApplicabilityCondition = Predicate(
                "project.businessDomain",
                RuleOperator.Equals,
                String("commerce")),
            Effects =
            [
                new BasicAnalysisEffect(
                    "Exclude",
                    true,
                    null,
                    null,
                    "This candidate is excluded for the relevant domain.")
            ]
        };

        var result = new BasicAnalysisEngine().Analyze(
            AnalysisId,
            CreateProject(teamSize: 4),
            "catalog-a",
            "rules-a",
            [Candidate("microservices", "Architecture")],
            [rule],
            Now);

        Assert.True(result.TryGetValue(out var snapshot));
        var candidate = Assert.Single(snapshot.CandidateGroups[0].Candidates);
        var evaluation = Assert.Single(candidate.RuleEvaluations);
        Assert.Equal(RuleApplicability.Unknown, evaluation.Applicability);
        Assert.Equal(RuleConditionResult.NotEvaluated, evaluation.ConditionResult);
        Assert.Equal(AnalysisEligibility.NotEvaluated, candidate.Eligibility);
        Assert.False(Assert.Single(evaluation.Effects).Applied);
        Assert.Contains("project.businessDomain", evaluation.UnknownFacts);
        Assert.Equal("Applicability", Assert.Single(evaluation.FactEvidence).Phase);
    }

    [Fact]
    public void Analyze_ExcludesOnlyWhenHardConstraintMatches()
    {
        var rule = SmallTeamRule() with
        {
            Conditions = Predicate(
                "candidate.category",
                RuleOperator.Equals,
                String("General")),
            Effects =
            [
                new BasicAnalysisEffect(
                    "Exclude",
                    true,
                    null,
                    null,
                    "The catalog category is disallowed by this constraint.")
            ]
        };

        var result = new BasicAnalysisEngine().Analyze(
            AnalysisId,
            CreateProject(teamSize: 4),
            "catalog-a",
            "rules-a",
            [Candidate("microservices", "Architecture")],
            [rule],
            Now);

        Assert.True(result.TryGetValue(out var snapshot));
        var candidate = Assert.Single(snapshot.CandidateGroups[0].Candidates);
        Assert.Equal(AnalysisEligibility.Excluded, candidate.Eligibility);
        Assert.True(Assert.Single(Assert.Single(candidate.RuleEvaluations).Effects).Applied);
    }

    private static Project CreateProject(int? teamSize)
    {
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing platform", Now);
        Assert.True(created.TryGetValue(out var project));
        var context = project.Context.Update(null, teamSize, null, []);
        Assert.True(context.TryGetValue(out var updatedContext));
        var updated = project.UpdateContext(updatedContext, Now);
        Assert.True(updated.TryGetValue(out var revisedProject));
        return revisedProject;
    }

    private static BasicAnalysisCandidate Candidate(string id, string kind) =>
        new(id, kind, id, "1.0.0", $"{id} description", "Curated source", [], "General", 5, 8, 4, ["benefit"], ["limitation"]);

    private static BasicAnalysisRule SmallTeamRule() =>
        new(
            "small-team-microservices-cost",
            "1.0.0",
            "Small team cost",
            "Curated example",
            "built-in/rule-small-team-microservices.json",
            "rule-hash",
            true,
            3,
            ["Architecture"],
            ["microservices"],
            [],
            null,
            Predicate("project.teamSize", RuleOperator.LessThanOrEqual, Number(8)),
            [
                new BasicAnalysisEffect(
                    "Score",
                    false,
                    -60,
                    null,
                    "A small team may struggle to sustain distributed operations."),
                new BasicAnalysisEffect(
                    "Flag",
                    false,
                    null,
                    "Warning",
                    "Validate operations capacity before selecting microservices.")
            ]);

    private static RuleCondition Predicate(string fact, RuleOperator @operator, RuleValue value) =>
        new(RuleConditionKind.Predicate, fact, @operator, value, []);

    private static RuleValue Number(decimal value) =>
        new(RuleValueKind.Number, NumberValue: value);

    private static RuleValue String(string value) =>
        new(RuleValueKind.String, StringValue: value);
}
