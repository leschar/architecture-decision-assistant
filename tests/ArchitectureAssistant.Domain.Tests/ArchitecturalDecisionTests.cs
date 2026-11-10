using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Tests;

public sealed class ArchitecturalDecisionTests
{
    private static readonly DateTimeOffset RecordedAt =
        new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TransitionTo_AcceptedPreservesOriginalProvenanceAndIncrementsRevision()
    {
        var analysisId = Guid.NewGuid();
        var created = CreateDecision(analysisId);
        Assert.True(created.TryGetValue(out var decision));

        var result = decision.TransitionTo(
            DecisionStatus.Accepted,
            "reviewer",
            RecordedAt.AddMinutes(1));

        Assert.True(result.TryGetValue(out var accepted));
        Assert.Equal(DecisionStatus.Accepted, accepted.Status);
        Assert.Equal(2, accepted.Revision);
        Assert.Equal("architect", accepted.RecordedBy);
        Assert.Equal(RecordedAt, accepted.RecordedAt);
        Assert.Equal("reviewer", accepted.StatusChangedBy);
        Assert.Equal(RecordedAt.AddMinutes(1), accepted.StatusChangedAt);
        Assert.Equal(2, Assert.Single(accepted.StatusHistory).Revision);
        Assert.Equal(DecisionStatus.Proposed, accepted.StatusHistory[0].PreviousStatus);
        Assert.Equal(DecisionStatus.Accepted, accepted.StatusHistory[0].Status);
        Assert.Equal(analysisId, accepted.AnalysisId);
        Assert.Equal("2.1", accepted.SelectedAlternativeVersion);
    }

    [Fact]
    public void TransitionTo_SupersededRequiresReplacementDecision()
    {
        var created = CreateDecision();
        Assert.True(created.TryGetValue(out var decision));

        var result = decision.TransitionTo(
            DecisionStatus.Superseded,
            "reviewer",
            RecordedAt.AddMinutes(1));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "decision.transition.invalid");
    }

    [Fact]
    public void TransitionTo_RejectedDecisionIsTerminal()
    {
        var created = CreateDecision();
        Assert.True(created.TryGetValue(out var decision));
        var rejectedResult = decision.TransitionTo(
            DecisionStatus.Rejected,
            "reviewer",
            RecordedAt.AddMinutes(1));
        Assert.True(rejectedResult.TryGetValue(out var rejected));

        var result = rejected.TransitionTo(
            DecisionStatus.Accepted,
            "reviewer",
            RecordedAt.AddMinutes(2));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "decision.transition.invalid");
    }

    [Fact]
    public void ReviseDecision_PreservesSupersededDecisionAndLinksNewProposal()
    {
        var projectResult = Project.Create(
            Guid.NewGuid(),
            "Orders",
            "Order processing",
            RecordedAt);
        Assert.True(projectResult.TryGetValue(out var project));
        var originalResult = CreateDecision();
        Assert.True(originalResult.TryGetValue(out var original));
        var withDecisionResult = project.AddDecision(original, RecordedAt);
        Assert.True(withDecisionResult.TryGetValue(out var withDecision));

        var replacementResult = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            "architecture-b",
            "Architecture",
            ["architecture-a"],
            DecisionStatus.Proposed,
            "Revised after further review.",
            [],
            "architect",
            RecordedAt.AddMinutes(2),
            supersedesDecisionId: original.Id);
        Assert.True(replacementResult.TryGetValue(out var replacement));

        var result = withDecision.ReviseDecision(
            original.Id,
            replacement,
            "architect",
            RecordedAt.AddMinutes(2));

        Assert.True(result.TryGetValue(out var revisedProject));
        Assert.Equal(3, revisedProject.Revision);
        Assert.Equal(DecisionStatus.Superseded, revisedProject.Decisions[0].Status);
        Assert.Equal(2, revisedProject.Decisions[0].Revision);
        Assert.Equal(DecisionStatus.Superseded, Assert.Single(revisedProject.Decisions[0].StatusHistory).Status);
        Assert.Equal(replacement.Id, revisedProject.Decisions[0].SupersededByDecisionId);
        Assert.Equal(original.Id, revisedProject.Decisions[1].SupersedesDecisionId);
        Assert.Equal(DecisionStatus.Proposed, revisedProject.Decisions[1].Status);
    }

    [Fact]
    public void Restore_AllowsLegacySupersededDecisionWithoutReplacementReference()
    {
        var result = ArchitecturalDecision.Restore(
            Guid.NewGuid(),
            "architecture-a",
            "Architecture",
            [],
            DecisionStatus.Superseded,
            "Legacy decision.",
            [],
            "architect",
            RecordedAt,
            2);

        Assert.True(result.TryGetValue(out var decision));
        Assert.Null(decision.SupersededByDecisionId);
    }

    private static ArchitectureAssistant.Domain.Common.Result<ArchitecturalDecision> CreateDecision(
        Guid? analysisId = null) =>
        ArchitecturalDecision.Create(
            Guid.NewGuid(),
            "architecture-a",
            "Architecture",
            ["architecture-b"],
            DecisionStatus.Proposed,
            "Chosen after review.",
            ["Requires an operational team."],
            "architect",
            RecordedAt,
            analysisId,
            "2.1");
}
