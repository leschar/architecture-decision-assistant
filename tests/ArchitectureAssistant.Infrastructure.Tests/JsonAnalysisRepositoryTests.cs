using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using ArchitectureAssistant.Domain.Projects;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArchitectureAssistant.Infrastructure.Tests;

public sealed class JsonAnalysisRepositoryTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(),
        "ArchitectureDecisionAssistant.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly JsonAnalysisRepository _repository;

    public JsonAnalysisRepositoryTests()
    {
        _repository = new JsonAnalysisRepository(_workspace, NullLogger<JsonAnalysisRepository>.Instance);
    }

    [Fact]
    public async Task SaveAndList_PreservesImmutableAnalysisSnapshots()
    {
        var snapshot = CreateSnapshot();

        Assert.True((await _repository.SaveAsync(snapshot, CancellationToken.None)).IsSuccess);
        var duplicate = await _repository.SaveAsync(snapshot, CancellationToken.None);
        var listed = await _repository.ListByProjectAsync(snapshot.ProjectId, CancellationToken.None);

        Assert.False(duplicate.IsSuccess);
        Assert.Contains(duplicate.Errors, error => error.Code == "analysis.snapshot.duplicate");
        Assert.True(listed.TryGetValue(out var snapshots));
        var persisted = Assert.Single(snapshots);
        Assert.Equal(snapshot.AnalysisId, persisted.AnalysisId);
        Assert.Equal(snapshot.ProjectId, persisted.ProjectId);
        Assert.Equal(snapshot.CatalogHash, persisted.CatalogHash);
        Assert.NotNull(persisted.GraphProjection);
        Assert.NotNull(persisted.GraphExpansion);
        Assert.Equal(
            ["Architecture", "Pattern", "Technology"],
            persisted.CandidateGroups.Select(group => group.Kind));
        Assert.True(File.Exists(Path.Combine(
            _workspace,
            "analyses",
            snapshot.ProjectId.ToString("N"),
            $"{snapshot.AnalysisId:N}.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private static AnalysisSnapshot CreateSnapshot()
    {
        var projectId = Guid.Parse("1ecb052d-2f2c-4a34-bbe6-8303787975a9");
        var now = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        var created = Project.Create(projectId, "Orders", "Order processing platform", now);
        Assert.True(created.TryGetValue(out var project));
        var updatedContext = project.Context.Update(null, 4, null, []);
        Assert.True(updatedContext.TryGetValue(out var context));
        var revisedProject = project.UpdateContext(context, now);
        Assert.True(revisedProject.TryGetValue(out project));

        var rule = new BasicAnalysisRule(
            "small-team-microservices-cost",
            "1.0.0",
            "Small team cost",
            "Curated example",
            "built-in/rule-small-team-microservices.json",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            true,
            3,
            ["Architecture"],
            ["microservices"],
            [],
            null,
            new RuleCondition(
                RuleConditionKind.Predicate,
                "project.teamSize",
                RuleOperator.LessThanOrEqual,
                new RuleValue(RuleValueKind.Number, NumberValue: 8),
                []),
            [
                new BasicAnalysisEffect("Score", false, -60, null, "A small team can face operational complexity."),
                new BasicAnalysisEffect("Flag", false, null, "Warning", "Review operational readiness.")
            ]);
        var result = new BasicAnalysisEngine().Analyze(
            Guid.Parse("3f21d9d3-68ee-4dd9-8850-aecfb4773bac"),
            project,
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            [
                new BasicAnalysisCandidate(
                    "microservices",
                    "Architecture",
                    "Microservices",
                    "1.0.0",
                    "Distributed services architecture.",
                    "Architecture source",
                    [],
                    "Distributed",
                    5,
                    8,
                    4,
                    ["Independent deployment"],
                    ["Operational complexity"])
            ],
            [rule],
            now,
            new KnowledgeGraphProjection(
                "1.0",
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                [
                    new KnowledgeGraphNode(
                        "microservices",
                        "Architecture",
                        "Microservices",
                        "1.0.0",
                        "Architecture source",
                        "test/catalog/microservices.json")
                ],
                [],
                []));

        Assert.True(result.TryGetValue(out var snapshot));
        return snapshot;
    }
}
