using System.Text.Json;
using ArchitectureAssistant.Domain.History;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class HistoryPolicyReadinessTests
{
    [Fact]
    public void AssessReadiness_ReportsCoverageWithoutExposingFeatureValues()
    {
        var records = new[]
        {
            Record(
                Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 10, "people"),
                Feature("project.businessDomain", HistoryFeatureStatus.Confirmed, "Retail")),
            Record(
                Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 20, "people"),
                Feature("project.businessDomain", HistoryFeatureStatus.Unknown, null)),
            Record(
                Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 5, "people"),
                Feature("project.businessDomain", HistoryFeatureStatus.Confirmed, "Retail"))
        };

        var result = HistorySimilarityPolicyRules.AssessReadiness(records, []);
        var teamSize = Assert.Single(result.Dimensions, dimension => dimension.FeaturePath == "project.teamSize");
        var businessDomain = Assert.Single(result.Dimensions, dimension => dimension.FeaturePath == "project.businessDomain");

        Assert.Equal(3, result.VerifiedArchiveCount);
        Assert.Equal(3, result.TotalProjectPairs);
        Assert.Equal(3, teamSize.ConfirmedCount);
        Assert.Equal(3, teamSize.ComparableProjectPairs);
        Assert.Equal(3, teamSize.DistinctValueCount);
        Assert.Equal(5m, teamSize.ObservedMinimum);
        Assert.Equal(20m, teamSize.ObservedMaximum);
        Assert.Equal(2, businessDomain.ConfirmedCount);
        Assert.Equal(1, businessDomain.UnknownCount);
        Assert.Equal(1, businessDomain.ComparableProjectPairs);
        Assert.DoesNotContain("Retail", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void AssessReadiness_ExcludesPairsWithIncompatibleProfiles()
    {
        var records = new[]
        {
            Record("1.0.0", Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 10, "people")),
            Record("2.0.0", Feature("project.teamSize", HistoryFeatureStatus.Confirmed, 20, "people"))
        };

        var result = HistorySimilarityPolicyRules.AssessReadiness(records, []);
        var teamSize = Assert.Single(result.Dimensions, dimension => dimension.FeaturePath == "project.teamSize");

        Assert.Equal(1, result.TotalProjectPairs);
        Assert.Equal(0, teamSize.ComparableProjectPairs);
    }

    private static HistoricalProjectRecord Record(params HistoryFeature[] features) =>
        Record("1.0.0", features);

    private static HistoricalProjectRecord Record(string profileVersion, params HistoryFeature[] features) =>
        new(
            "1.0",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("D"),
            "1",
            "snapshot.json",
            "sha256:" + new string('a', 64),
            DateTimeOffset.UtcNow,
            new HistoryArchiveConfirmation("tester", DateTimeOffset.UtcNow, null),
            profileVersion,
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
