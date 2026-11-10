using System.Text.Json;
using ArchitectureAssistant.Domain.History;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class HistorySimilarityComparerTests
{
    [Fact]
    public void Compare_ExcludesUnknownAndIncompatibleDimensionsAndUsesConfiguredCoverage()
    {
        var current = Record(
            Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 10, "people"),
            Feature("project.businessDomain", HistoryFeatureStatus.Unknown, null));
        var historical = Record(
            Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 15, "people"),
            Feature("project.businessDomain", HistoryFeatureStatus.Confirmed, "Retail"));
        var policy = new HistorySimilarityPolicy(
            "1.0",
            "comparison",
            "1.0.0",
            [
                new HistorySimilarityDimension("team-size", "project.teamSize", 2m, "NumericLinear", 0m, 20m),
                new HistorySimilarityDimension("domain", "project.businessDomain", 1m, "Exact", null, null)
            ],
            0.75m);

        var result = HistorySimilarityComparer.Compare(current, historical, policy);

        Assert.Equal(HistoryComparisonStatus.InsufficientCoverage, result.Status);
        Assert.Equal(2m / 3m, result.Coverage);
        Assert.Equal(0.75m, result.Similarity);
        Assert.NotNull(result.Dimensions.Single(dimension => dimension.DimensionId == "domain").ExcludedReason);
    }

    [Fact]
    public void Compare_ReturnsNotComparableWhenPolicyHasNoComparableValues()
    {
        var current = Record(Feature("project.name", HistoryFeatureStatus.Confirmed, "Orders"));
        var historical = Record(Feature("project.name", HistoryFeatureStatus.Confirmed, 42));
        var policy = new HistorySimilarityPolicy(
            "1.0",
            "comparison",
            "1.0.0",
            [new HistorySimilarityDimension("name", "project.name", 1m, "Exact", null, null)],
            0.5m);

        var result = HistorySimilarityComparer.Compare(current, historical, policy);

        Assert.Equal(HistoryComparisonStatus.NotComparable, result.Status);
        Assert.Null(result.Similarity);
        Assert.Equal(0m, result.Coverage);
    }

    private static HistoricalProjectRecord Record(params HistoryFeature[] features) =>
        new(
            "1.0",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("D"),
            "1",
            "snapshot.json",
            "sha256:" + new string('a', 64),
            DateTimeOffset.UtcNow,
            new HistoryArchiveConfirmation("tester", DateTimeOffset.UtcNow, null),
            "1.0.0",
            features,
            [],
            null,
            null);

    private static HistoryFeature Feature(string path, HistoryFeatureStatus status, object? value, string? unit = null) =>
        new(
            path,
            status,
            value is null ? null : JsonSerializer.SerializeToElement(value),
            unit,
            null);
}
