using ArchitectureAssistant.Domain.History;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class HistorySimilarityPolicyRulesTests
{
    [Fact]
    public void Validate_AcceptsSupportedDistinctDimensions()
    {
        var policy = Policy(
            new HistorySimilarityDimension("team-size", "project.teamSize", 2m, "NumericLinear", 1m, 100m),
            new HistorySimilarityDimension("domain", "project.businessDomain", 1m, "Exact", null, null),
            new HistorySimilarityDimension("architectures", "project.selectedAlternativeIds", 1m, "SetJaccard", null, null));

        Assert.True(HistorySimilarityPolicyRules.Validate(policy).IsSuccess);
    }

    [Fact]
    public void Validate_RejectsDuplicateFeaturePaths()
    {
        var policy = Policy(
            new HistorySimilarityDimension("domain", "project.businessDomain", 1m, "Exact", null, null),
            new HistorySimilarityDimension("business-area", "project.businessDomain", 2m, "Exact", null, null));

        Assert.False(HistorySimilarityPolicyRules.Validate(policy).IsSuccess);
    }

    [Theory]
    [InlineData("project.teamSize", "Exact")]
    [InlineData("project.purpose", "Exact")]
    [InlineData("project.criticality", "SetJaccard")]
    public void Validate_RejectsUnsupportedPathsOrComparators(string featurePath, string comparison)
    {
        var policy = Policy(
            new HistorySimilarityDimension("dimension", featurePath, 1m, comparison, null, null));

        Assert.False(HistorySimilarityPolicyRules.Validate(policy).IsSuccess);
    }

    [Fact]
    public void Validate_RejectsNumericDimensionWithInvalidRange()
    {
        var policy = Policy(
            new HistorySimilarityDimension("team-size", "project.teamSize", 1m, "NumericLinear", 10m, 10m));

        Assert.False(HistorySimilarityPolicyRules.Validate(policy).IsSuccess);
    }

    private static HistorySimilarityPolicy Policy(params HistorySimilarityDimension[] dimensions) =>
        new("1.0", "local-history", "1.0.0", dimensions, 0.5m);
}
