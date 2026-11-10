using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Domain.Projects;

public sealed record ArchitecturalDecision(
    Guid Id,
    string SelectedAlternativeId,
    string SelectedAlternativeKind,
    IReadOnlyList<string> ConsideredAlternatives,
    DecisionStatus Status,
    string Justification,
    IReadOnlyList<string> Consequences,
    string RecordedBy,
    DateTimeOffset RecordedAt,
    long Revision)
{
    public IReadOnlyList<DecisionStatusChange> StatusHistory { get; init; } = Array.Empty<DecisionStatusChange>();

    public Guid? AnalysisId { get; init; }

    public string? SelectedAlternativeVersion { get; init; }

    public string? StatusChangedBy { get; init; }

    public DateTimeOffset? StatusChangedAt { get; init; }

    public Guid? SupersededByDecisionId { get; init; }

    public Guid? SupersedesDecisionId { get; init; }

    public HistoricalDecisionSource? HistoricalSource { get; init; }

    public static Result<ArchitecturalDecision> Create(
        Guid id,
        string selectedAlternativeId,
        string selectedAlternativeKind,
        IEnumerable<string> consideredAlternatives,
        DecisionStatus status,
        string justification,
        IEnumerable<string> consequences,
        string recordedBy,
        DateTimeOffset recordedAt,
        Guid? analysisId = null,
        string? selectedAlternativeVersion = null,
        Guid? supersedesDecisionId = null,
        HistoricalDecisionSource? historicalSource = null)
    {
        var alternatives = consideredAlternatives
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var decisionConsequences = consequences
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

        if (id == Guid.Empty ||
            string.IsNullOrWhiteSpace(selectedAlternativeId) ||
            selectedAlternativeId.Trim().Length > 200 ||
            !new[] { "Architecture", "Pattern", "Technology" }.Contains(selectedAlternativeKind, StringComparer.Ordinal) ||
            !Enum.IsDefined(status) ||
            recordedAt == default ||
            analysisId == Guid.Empty ||
            supersedesDecisionId == Guid.Empty ||
            supersedesDecisionId == id ||
            historicalSource is not null &&
            (string.IsNullOrWhiteSpace(historicalSource.ArchiveId) ||
             historicalSource.SourceProjectId == Guid.Empty ||
             historicalSource.SourceDecisionId == Guid.Empty ||
             string.IsNullOrWhiteSpace(historicalSource.SourceProjectRevision) ||
             string.IsNullOrWhiteSpace(historicalSource.SnapshotFingerprint) ||
             !historicalSource.SnapshotFingerprint.StartsWith("sha256:", StringComparison.Ordinal) ||
             historicalSource.SnapshotFingerprint.Length != 71) ||
            string.IsNullOrWhiteSpace(justification) ||
            string.IsNullOrWhiteSpace(recordedBy) ||
            alternatives.Length > 100 ||
            alternatives.Any(value => value.Length > 200) ||
            decisionConsequences.Any(value => value.Length > 2000) ||
            justification.Trim().Length > 4000 ||
            recordedBy.Trim().Length > 120 ||
            selectedAlternativeVersion is not null &&
            (string.IsNullOrWhiteSpace(selectedAlternativeVersion) || selectedAlternativeVersion.Length > 100))
        {
            return Result<ArchitecturalDecision>.Failure(
                new DomainError("decision.invalid", "Decision fields are incomplete or invalid."));
        }

        return Result<ArchitecturalDecision>.Success(new ArchitecturalDecision(
            id,
            selectedAlternativeId.Trim(),
            selectedAlternativeKind,
            alternatives,
            status,
            justification.Trim(),
            decisionConsequences,
            recordedBy.Trim(),
            recordedAt,
            1)
        {
            AnalysisId = analysisId,
            SelectedAlternativeVersion = selectedAlternativeVersion?.Trim(),
            SupersedesDecisionId = supersedesDecisionId,
            HistoricalSource = historicalSource
        });
    }

    public static Result<ArchitecturalDecision> Restore(
        Guid id,
        string selectedAlternativeId,
        string selectedAlternativeKind,
        IReadOnlyList<string> consideredAlternatives,
        DecisionStatus status,
        string justification,
        IReadOnlyList<string> consequences,
        string recordedBy,
        DateTimeOffset recordedAt,
        long revision,
        Guid? analysisId = null,
        string? selectedAlternativeVersion = null,
        string? statusChangedBy = null,
        DateTimeOffset? statusChangedAt = null,
        Guid? supersededByDecisionId = null,
        Guid? supersedesDecisionId = null,
        IReadOnlyList<DecisionStatusChange>? statusHistory = null,
        HistoricalDecisionSource? historicalSource = null)
    {
        var result = Create(
            id,
            selectedAlternativeId,
            selectedAlternativeKind,
            consideredAlternatives,
            status,
            justification,
            consequences,
            recordedBy,
            recordedAt,
            analysisId,
            selectedAlternativeVersion,
            supersedesDecisionId,
            historicalSource);
        if (!result.TryGetValue(out var decision))
        {
            return result;
        }

        if (revision < 1 ||
            (statusChangedBy is null) != (statusChangedAt is null) ||
            statusChangedBy is not null && string.IsNullOrWhiteSpace(statusChangedBy) ||
            statusChangedAt.HasValue && statusChangedAt.Value == default ||
            supersededByDecisionId == Guid.Empty ||
            (statusChangedBy is null && supersededByDecisionId is not null) ||
            (statusChangedBy is not null && decision.Status == DecisionStatus.Superseded) !=
            (supersededByDecisionId is not null))
        {
            return Result<ArchitecturalDecision>.Failure(
                new DomainError("decision.history.invalid", "The decision revision or transition provenance is invalid."));
        }

        var restoredHistory = statusHistory ?? Array.Empty<DecisionStatusChange>();
        if (!IsValidStatusHistory(
                decision.Status,
                revision,
                decision.RecordedAt,
                supersededByDecisionId,
                restoredHistory))
        {
            return Result<ArchitecturalDecision>.Failure(
                new DomainError("decision.history.invalid", "The decision status history is inconsistent."));
        }
        if (restoredHistory.Count > 0 &&
            (restoredHistory[^1].Revision != revision ||
             restoredHistory[^1].ChangedBy != statusChangedBy ||
             restoredHistory[^1].ChangedAt != statusChangedAt))
        {
            return Result<ArchitecturalDecision>.Failure(
                new DomainError("decision.history.invalid", "Decision transition metadata does not match its status history."));
        }

        return Result<ArchitecturalDecision>.Success(decision with
        {
            Revision = revision,
            StatusChangedBy = statusChangedBy?.Trim(),
            StatusChangedAt = statusChangedAt,
            SupersededByDecisionId = supersededByDecisionId,
            StatusHistory = restoredHistory.ToArray()
        });
    }

    public Result<ArchitecturalDecision> TransitionTo(
        DecisionStatus status,
        string changedBy,
        DateTimeOffset changedAt,
        Guid? supersededByDecisionId = null)
    {
        var transitionAllowed = Status switch
        {
            DecisionStatus.Proposed => status is DecisionStatus.Accepted
                or DecisionStatus.Rejected
                or DecisionStatus.Superseded,
            DecisionStatus.Accepted => status == DecisionStatus.Superseded,
            DecisionStatus.Rejected or DecisionStatus.Superseded => false,
            _ => false
        };
        var replacementIsValid = status == DecisionStatus.Superseded
            ? supersededByDecisionId.HasValue &&
                supersededByDecisionId != Guid.Empty &&
                supersededByDecisionId != Id
            : supersededByDecisionId is null;

        if (!transitionAllowed || !replacementIsValid ||
            string.IsNullOrWhiteSpace(changedBy) ||
            changedBy.Trim().Length > 120 ||
            changedAt == default ||
            changedAt < RecordedAt ||
            StatusChangedAt.HasValue && changedAt < StatusChangedAt.Value ||
            Revision == long.MaxValue)
        {
            return Result<ArchitecturalDecision>.Failure(
                new DomainError("decision.transition.invalid", "The requested decision status transition is invalid."));
        }

        var revisedRevision = Revision + 1;
        var revisedHistory = StatusHistory.Append(new DecisionStatusChange(
            revisedRevision,
            Status,
            status,
            changedBy.Trim(),
            changedAt,
            supersededByDecisionId)).ToArray();

        return Result<ArchitecturalDecision>.Success(this with
        {
            Status = status,
            Revision = revisedRevision,
            StatusChangedBy = changedBy.Trim(),
            StatusChangedAt = changedAt,
            SupersededByDecisionId = supersededByDecisionId,
            StatusHistory = revisedHistory
        });
    }

    private static bool IsValidStatusHistory(
        DecisionStatus currentStatus,
        long revision,
        DateTimeOffset recordedAt,
        Guid? supersededByDecisionId,
        IReadOnlyList<DecisionStatusChange> history)
    {
        if (history.Count == 0)
        {
            return true;
        }

        var previousStatus = DecisionStatus.Proposed;
        var previousRevision = 1L;
        var previousChangedAt = recordedAt;
        for (var index = 0; index < history.Count; index++)
        {
            var change = history[index];
            var isFirstLegacyTransition = index == 0 && change.Revision > 2;
            if (change.Revision <= previousRevision ||
                !isFirstLegacyTransition && change.Revision != previousRevision + 1 ||
                !Enum.IsDefined(change.PreviousStatus) ||
                !Enum.IsDefined(change.Status) ||
                !isFirstLegacyTransition && change.PreviousStatus != previousStatus ||
                change.ChangedAt == default ||
                change.ChangedAt < previousChangedAt ||
                string.IsNullOrWhiteSpace(change.ChangedBy) ||
                change.Revision > revision ||
                !IsTransitionAllowed(change.PreviousStatus, change.Status) ||
                (change.Status == DecisionStatus.Superseded) != change.SupersededByDecisionId.HasValue)
            {
                return false;
            }

            if (isFirstLegacyTransition)
            {
                previousStatus = change.PreviousStatus;
            }
            previousStatus = change.Status;
            previousRevision = change.Revision;
            previousChangedAt = change.ChangedAt;
        }

        return history[^1].Revision == revision &&
            history[^1].Status == currentStatus &&
            (history[^1].SupersededByDecisionId is null ||
                history[^1].SupersededByDecisionId == supersededByDecisionId);
    }

    private static bool IsTransitionAllowed(DecisionStatus current, DecisionStatus next) =>
        current switch
        {
            DecisionStatus.Proposed => next is DecisionStatus.Accepted
                or DecisionStatus.Rejected
                or DecisionStatus.Superseded,
            DecisionStatus.Accepted => next == DecisionStatus.Superseded,
            _ => false
        };
}

public sealed record DecisionStatusChange(
    long Revision,
    DecisionStatus PreviousStatus,
    DecisionStatus Status,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    Guid? SupersededByDecisionId);

public sealed record HistoricalDecisionSource(
    string ArchiveId,
    Guid SourceProjectId,
    Guid SourceDecisionId,
    string SourceProjectRevision,
    string SnapshotFingerprint);

public enum DecisionStatus
{
    Proposed,
    Accepted,
    Rejected,
    Superseded
}
