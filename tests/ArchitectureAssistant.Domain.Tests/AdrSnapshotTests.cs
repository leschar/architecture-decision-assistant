using ArchitectureAssistant.Domain.Adrs;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class AdrSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComposeAndAccept_PreserveDecisionAndRequireExplicitCompletion()
    {
        var created = Project.Create(Guid.NewGuid(), "Orders", "Process orders", Now);
        Assert.True(created.TryGetValue(out var project));
        var decisionResult = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            "modular-monolith",
            "Architecture",
            ["microservices"],
            DecisionStatus.Proposed,
            "The team selected this option.",
            [],
            "architect",
            Now);
        Assert.True(decisionResult.TryGetValue(out var decision));
        var projectWithDecision = project.AddDecision(decision, Now);
        Assert.True(projectWithDecision.TryGetValue(out var projectSnapshot));

        var composed = AdrSnapshot.Compose(projectSnapshot, decision, "adr-1", Now.AddMinutes(1));
        Assert.True(composed.TryGetValue(out var adr));
        Assert.Equal(AdrStatus.NeedsReview, adr.Status);
        Assert.Equal(AdrCompleteness.NotProvided, adr.Sections.Problem.Completeness);
        Assert.Contains(adr.Sections.Decision.SourceReferences, source => source.SourceId == decision.Id.ToString("D"));
        Assert.False(adr.Accept("reviewer", Now.AddMinutes(2), null).IsSuccess);

        foreach (var section in new[]
                 {
                     AdrSectionKind.Problem,
                     AdrSectionKind.Consequences,
                     AdrSectionKind.Risks,
                     AdrSectionKind.Benefits,
                     AdrSectionKind.TradeOffs
                 })
        {
            var revised = adr.ReviseSection(section, $"Reviewed {section}.", "architect", Now.AddMinutes(3 + (int)section));
            Assert.True(revised.TryGetValue(out adr));
        }

        var accepted = adr.Accept("reviewer", Now.AddMinutes(20), "Reviewed");
        Assert.True(accepted.TryGetValue(out var acceptedAdr));
        Assert.Equal(AdrStatus.Accepted, acceptedAdr.Status);
        Assert.Equal(DecisionStatus.Proposed, projectSnapshot.Decisions.Single().Status);
        Assert.Equal(1, projectSnapshot.Decisions.Single().Revision);
    }

    [Fact]
    public void SupersedeBy_CreatesImmutableLinkedRevisionWithoutChangingDecision()
    {
        var created = Project.Create(Guid.NewGuid(), "Orders", "Process orders", Now);
        Assert.True(created.TryGetValue(out var project));
        var decisionResult = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            "modular-monolith",
            "Architecture",
            ["microservices"],
            DecisionStatus.Proposed,
            "The team selected this option.",
            [],
            "architect",
            Now);
        Assert.True(decisionResult.TryGetValue(out var decision));
        var projectWithDecision = project.AddDecision(decision, Now);
        Assert.True(projectWithDecision.TryGetValue(out var projectSnapshot));
        var composed = AdrSnapshot.Compose(projectSnapshot, decision, "adr-1", Now.AddMinutes(1));
        Assert.True(composed.TryGetValue(out var original));

        var superseded = original.SupersedeBy("adr-2", Now.AddMinutes(2));

        Assert.True(superseded.TryGetValue(out var next));
        Assert.Equal(AdrStatus.Superseded, next.Status);
        Assert.Equal("adr-2", next.SupersededByAdrId);
        Assert.Equal(2, next.Revision);
        Assert.Equal(AdrStatus.NeedsReview, original.Status);
        Assert.Null(original.SupersededByAdrId);
        Assert.Equal(DecisionStatus.Proposed, projectSnapshot.Decisions.Single().Status);
        Assert.False(next.SupersedeBy("adr-3", Now.AddMinutes(3)).IsSuccess);
        Assert.False(original.SupersedeBy("adr-1", Now.AddMinutes(3)).IsSuccess);
    }
}
