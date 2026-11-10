using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class KnowledgeGraphProjectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly string CatalogHash = new('a', 64);

    [Fact]
    public void Project_IsStableAndCanonicalizesReciprocalSymmetricRelations()
    {
        var first = Item("alpha", "Architecture",
            Relation("ConflictsWith", "beta", "Incompatible choices."));
        var second = Item("beta", "Technology",
            Relation("ConflictsWith", "alpha", "Incompatible choices."));
        var projector = new KnowledgeGraphProjector();

        var firstResult = projector.Project(CatalogHash, [second, first]);
        var secondResult = projector.Project(CatalogHash, [first, second]);

        Assert.True(firstResult.TryGetValue(out var firstProjection));
        Assert.True(secondResult.TryGetValue(out var secondProjection));
        Assert.Equal(["alpha", "beta"], firstProjection.Nodes.Select(node => node.ItemId));
        var relation = Assert.Single(firstProjection.Relations);
        Assert.Equal("alpha", relation.SourceId);
        Assert.Equal("beta", relation.TargetId);
        Assert.Equal(2, relation.Declarations.Count);
        Assert.Single(firstProjection.Warnings);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(firstProjection),
            System.Text.Json.JsonSerializer.Serialize(secondProjection));
    }

    [Fact]
    public void Project_RejectsDependsOnCyclesWithoutReturningAPartialProjection()
    {
        var result = new KnowledgeGraphProjector().Project(
            CatalogHash,
            [
                Item("alpha", "Architecture", Relation("DependsOn", "beta", "Requires beta.")),
                Item("beta", "Pattern", Relation("DependsOn", "alpha", "Requires alpha."))
            ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "knowledge.graph.dependency_cycle");
    }

    [Fact]
    public void Analyze_EvaluatesExplicitRelationsWithUnknownFactsAndNoAutomaticScoringOrSelection()
    {
        var items = new[]
        {
            Item("architecture-a", "Architecture",
                Relation("DependsOn", "technology-b", "Needs a compatible technology."),
                Relation("Recommends", "pattern-c", "Can complement the architecture."),
                Relation("ConflictsWith", "technology-d", "Cannot coexist in this context."),
                Relation("AlternativeTo", "pattern-e", "Alternative for the same concern."),
                Relation("Recommends", "technology-f", "Useful for larger teams.",
                    Predicate("project.teamSize", RuleOperator.GreaterThan, Number(3)))),
            Item("technology-b", "Technology"),
            Item("pattern-c", "Pattern"),
            Item("technology-d", "Technology"),
            Item("pattern-e", "Pattern"),
            Item("technology-f", "Technology")
        };
        var projectorResult = new KnowledgeGraphProjector().Project(
            CatalogHash,
            items);
        Assert.True(projectorResult.TryGetValue(out var graph));

        var projectResult = Project.Create(Guid.NewGuid(), "Orders", "Order processing", Now);
        Assert.True(projectResult.TryGetValue(out var project));
        var architectureDecision = Decision("architecture-a", "Architecture");
        var conflictDecision = Decision("technology-d", "Technology");
        Assert.True(project.AddDecision(architectureDecision, Now).TryGetValue(out var selectedArchitecture));
        Assert.True(selectedArchitecture.AddDecision(conflictDecision, Now).TryGetValue(out var selectedProject));

        var candidates = items.Select(Candidate).ToArray();
        var analysisResult = new BasicAnalysisEngine().Analyze(
            Guid.NewGuid(),
            selectedProject,
            CatalogHash,
            "rules",
            candidates,
            [],
            Now,
            graph);

        Assert.True(analysisResult.TryGetValue(out var analysis));
        Assert.NotNull(analysis.GraphProjection);
        var candidate = Assert.Single(
            analysis.CandidateGroups.SelectMany(group => group.Candidates),
            item => item.CandidateId == "architecture-a");
        Assert.Null(candidate.FitScore);
        var findingsByType = candidate.RelationFindings
            .GroupBy(finding => finding.Type)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        Assert.Equal("Missing", Assert.Single(findingsByType["DependsOn"]).Status);
        Assert.Equal("Suggested", Assert.Single(findingsByType["Recommends"], finding =>
            finding.TargetId == "pattern-c").Status);
        var unknown = Assert.Single(findingsByType["Recommends"], finding =>
            finding.TargetId == "technology-f");
        Assert.Equal("Unknown", unknown.Status);
        Assert.Equal("Unknown", unknown.Applicability);
        Assert.Equal(["project.teamSize"], unknown.UnknownFacts);
        Assert.Equal("knowledge/architecture-a.json", unknown.KnowledgeSource);
        Assert.Equal("Conflict", Assert.Single(findingsByType["ConflictsWith"]).Status);
        Assert.Equal("Alternative", Assert.Single(findingsByType["AlternativeTo"]).Status);
        Assert.Equal(2, selectedProject.Decisions.Count);
        Assert.DoesNotContain(selectedProject.Decisions, decision =>
            decision.SelectedAlternativeId == "technology-b");
    }

    [Fact]
    public void Expand_FollowsOnlyApplicableDependenciesAndPreservesAlternativePaths()
    {
        var items = new[]
        {
            Item("root", "Architecture",
                Relation("DependsOn", "left", "Left branch."),
                Relation("DependsOn", "right", "Right branch."),
                Relation("DependsOn", "unknown", "Conditional dependency.",
                    Predicate("project.teamSize", RuleOperator.GreaterThan, Number(3))),
                Relation("Recommends", "suggested", "Optional complement.")),
            Item("left", "Pattern", Relation("DependsOn", "shared", "Left path.")),
            Item("right", "Technology", Relation("DependsOn", "shared", "Right path.")),
            Item("shared", "Pattern", Relation("DependsOn", "transitive", "Transitive dependency.")),
            Item("transitive", "Technology"),
            Item("unknown", "Technology", Relation("DependsOn", "unknown-child", "Must not be expanded.")),
            Item("unknown-child", "Pattern"),
            Item("suggested", "Technology", Relation("DependsOn", "suggested-child", "Must not be expanded.")),
            Item("suggested-child", "Pattern")
        };
        var projectionResult = new KnowledgeGraphProjector().Project(CatalogHash, items);
        Assert.True(projectionResult.TryGetValue(out var projection));
        var project = CreateProject(teamSize: null);
        var candidates = items.Select(Candidate).ToArray();

        var result = new KnowledgeGraphExpander().Expand(project, candidates, projection);

        Assert.True(result.TryGetValue(out var expansion));
        Assert.False(expansion.Truncated);
        var rootDependencies = expansion.Dependencies
            .Where(item => item.RootId == "root")
            .ToArray();
        var sharedDependencies = rootDependencies.Where(item => item.TargetId == "shared").ToArray();
        Assert.Equal(2, sharedDependencies.Length);
        Assert.Equal(2, sharedDependencies
            .SelectMany(dependency => dependency.PathsFromRoot)
            .DistinctBy(path => string.Join("/", path), StringComparer.Ordinal)
            .Count());
        Assert.All(sharedDependencies.SelectMany(dependency => dependency.PathsFromRoot), path =>
            Assert.Equal(2, path.Count));
        Assert.Contains(rootDependencies, item =>
            item.TargetId == "unknown" &&
            item.Applicability == "Unknown" &&
            item.Finding == "Unknown" &&
            item.UnknownFacts.SequenceEqual(["project.teamSize"]));
        Assert.DoesNotContain(rootDependencies, item => item.TargetId == "unknown-child");
        Assert.DoesNotContain(rootDependencies, item => item.TargetId == "suggested-child");
        Assert.Contains(expansion.Nodes, node =>
            node.ItemId == "unknown" && node.Roles.Contains("PotentialRequired"));
        Assert.DoesNotContain(expansion.Nodes, node =>
            node.ItemId == "suggested" && node.Roles.Contains("Required"));
    }

    [Fact]
    public void Expand_ReportsDepthAndGlobalPathLimitsAsTruncated()
    {
        var chain = Enumerable.Range(0, KnowledgeGraphExpander.MaximumDependencyDepth + 2)
            .Select(index => new KnowledgeGraphItem(
                $"node-{index:D2}",
                index == 0 ? "Architecture" : "Pattern",
                "1.0.0",
                $"node-{index:D2}",
                null,
                $"knowledge/node-{index:D2}.json",
                index < KnowledgeGraphExpander.MaximumDependencyDepth + 1
                    ? [Relation("DependsOn", $"node-{index + 1:D2}", "Next node.")]
                    : []))
            .ToArray();
        var chainProjectionResult = new KnowledgeGraphProjector().Project(CatalogHash, chain);
        Assert.True(chainProjectionResult.TryGetValue(out var chainProjection));
        var chainCandidates = chain.Select(Candidate).ToArray();
        var chainExpansionResult = new KnowledgeGraphExpander().Expand(
            CreateProject(teamSize: 4),
            chainCandidates,
            chainProjection);

        Assert.True(chainExpansionResult.TryGetValue(out var chainExpansion));
        Assert.True(chainExpansion.Truncated);
        Assert.Contains(chainExpansion.Warnings, warning =>
            warning.Contains($"maximum depth of {KnowledgeGraphExpander.MaximumDependencyDepth}", StringComparison.Ordinal));
        Assert.Equal(
            KnowledgeGraphExpander.MaximumDependencyDepth,
            chainExpansion.Dependencies.Count(item => item.RootId == "node-00"));

        var layeredItems = Enumerable.Range(0, 15)
            .SelectMany(layer => Enumerable.Range(0, 2).Select(index =>
                Item(
                    $"layer-{layer:D2}-{index}",
                    layer == 0 ? "Architecture" : "Pattern",
                    layer == 14
                        ? []
                        : Enumerable.Range(0, 2)
                            .Select(targetIndex => Relation(
                                "DependsOn",
                                $"layer-{layer + 1:D2}-{targetIndex}",
                                "Next layer."))
                            .ToArray())))
            .ToArray();
        var layeredProjectionResult = new KnowledgeGraphProjector().Project(CatalogHash, layeredItems);
        Assert.True(layeredProjectionResult.TryGetValue(out var layeredProjection));
        var layeredExpansionResult = new KnowledgeGraphExpander().Expand(
            CreateProject(teamSize: 4),
            layeredItems.Select(Candidate).ToArray(),
            layeredProjection);

        Assert.True(layeredExpansionResult.TryGetValue(out var layeredExpansion));
        Assert.True(layeredExpansion.Truncated);
        Assert.Equal(KnowledgeGraphExpander.MaximumPathCount, layeredExpansion.EvaluatedPathCount);
        Assert.Contains(layeredExpansion.Warnings, warning =>
            warning.Contains($"global limit of {KnowledgeGraphExpander.MaximumPathCount}", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_PreservesRelationsDependenciesAndIndividualScoresWithoutChangingDecisions()
    {
        var items = new[]
        {
            Item("architecture-a", "Architecture",
                Relation("DependsOn", "pattern-c", "Shared dependency."),
                Relation("ConflictsWith", "architecture-d", "Cannot coexist.")),
            Item("architecture-d", "Architecture",
                Relation("DependsOn", "pattern-c", "Shared dependency."),
                Relation("AlternativeTo", "architecture-a", "Alternative candidate.")),
            Item("technology-b", "Technology",
                Relation("DependsOn", "pattern-c", "Shared dependency.")),
            Item("pattern-c", "Pattern")
        };
        var projectionResult = new KnowledgeGraphProjector().Project(CatalogHash, items);
        Assert.True(projectionResult.TryGetValue(out var projection));
        var project = CreateProject(teamSize: 4);
        Assert.True(project.AddDecision(Decision("architecture-a", "Architecture"), Now)
            .TryGetValue(out var withArchitecture));
        Assert.True(withArchitecture.AddDecision(Decision("technology-b", "Technology"), Now)
            .TryGetValue(out var selectedProject));
        var analysisResult = new BasicAnalysisEngine().Analyze(
            Guid.NewGuid(),
            selectedProject,
            CatalogHash,
            "rules",
            items.Select(Candidate).ToArray(),
            [],
            Now,
            projection);
        Assert.True(analysisResult.TryGetValue(out var snapshot));
        snapshot = snapshot with
        {
            CandidateGroups = snapshot.CandidateGroups.Select(group => group with
            {
                Candidates = group.Candidates.Select(candidate => candidate.CandidateId switch
                {
                    "architecture-a" => candidate with { FitScore = 72m, EvidenceCoverage = 0.6m },
                    "architecture-d" => candidate with { FitScore = 31m, EvidenceCoverage = 0.9m },
                    _ => candidate
                }).ToArray()
            }).ToArray()
        };

        var differentKinds = new AnalysisCandidateComparer().Compare(
            snapshot,
            "architecture-a",
            "technology-b");
        Assert.False(differentKinds.IsSuccess);
        Assert.Contains(differentKinds.Errors, error => error.Code == "analysis.comparison.kind_mismatch");

        var result = new AnalysisCandidateComparer().Compare(
            snapshot,
            "architecture-a",
            "architecture-d");

        Assert.True(result.TryGetValue(out var comparison));
        Assert.Equal("Complete", comparison.DependencyDataStatus);
        Assert.Contains(comparison.RelationsBetweenCandidates, finding =>
            finding.Type == "ConflictsWith");
        Assert.Contains(comparison.RelationsBetweenCandidates, finding =>
            finding.Type == "AlternativeTo");
        Assert.Equal(["pattern-c"], comparison.SharedRequiredDependencyIds);
        Assert.Empty(comparison.FirstOnlyRequiredDependencyIds);
        Assert.Empty(comparison.SecondOnlyRequiredDependencyIds);
        Assert.Equal(
            72m,
            comparison.FirstCandidate.FitScore);
        Assert.Equal(31m, comparison.SecondCandidate.FitScore);
        Assert.Equal(0.6m, comparison.FirstCandidate.EvidenceCoverage);
        Assert.Equal(0.9m, comparison.SecondCandidate.EvidenceCoverage);
        Assert.Equal(2, selectedProject.Decisions.Count);
        Assert.Contains(selectedProject.Decisions, decision =>
            decision.SelectedAlternativeId == "architecture-a");
        Assert.Contains(selectedProject.Decisions, decision =>
            decision.SelectedAlternativeId == "technology-b");
    }

    private static KnowledgeGraphItem Item(
        string id,
        string kind,
        params KnowledgeGraphRelationDeclaration[] relations) =>
        new(
            id,
            kind,
            "1.0.0",
            id,
            null,
            $"knowledge/{id}.json",
            relations);

    private static KnowledgeGraphRelationDeclaration Relation(
        string type,
        string targetId,
        string rationale,
        RuleCondition? appliesWhen = null) =>
        new(type, targetId, rationale, null, appliesWhen, "knowledge/architecture-a.json");

    private static RuleCondition Predicate(string fact, RuleOperator @operator, RuleValue value) =>
        new(RuleConditionKind.Predicate, fact, @operator, value, []);

    private static RuleValue Number(decimal value) =>
        new(RuleValueKind.Number, NumberValue: value);

    private static ArchitecturalDecision Decision(string itemId, string kind)
    {
        var result = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            itemId,
            kind,
            [],
            DecisionStatus.Accepted,
            "Explicitly selected for this test.",
            [],
            "test",
            Now);
        Assert.True(result.TryGetValue(out var decision));
        return decision;
    }

    private static BasicAnalysisCandidate Candidate(KnowledgeGraphItem item) =>
        new(
            item.Id,
            item.Kind,
            item.Name,
            item.Version,
            "Candidate description.",
            null,
            [],
            null,
            null,
            null,
            null,
            [],
            []);

    private static Project CreateProject(int? teamSize)
    {
        var created = Project.Create(Guid.NewGuid(), "Orders", "Order processing", Now);
        Assert.True(created.TryGetValue(out var project));
        var updatedContext = project.Context.Update(null, teamSize, null, []);
        Assert.True(updatedContext.TryGetValue(out var context));
        var updated = project.UpdateContext(context, Now);
        Assert.True(updated.TryGetValue(out var revisedProject));
        return revisedProject;
    }
}
