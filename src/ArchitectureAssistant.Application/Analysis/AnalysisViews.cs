using ArchitectureAssistant.Domain.Analysis;

namespace ArchitectureAssistant.Application.Analysis;

public sealed record AnalysisHistoryItem(
    AnalysisSnapshot Snapshot,
    IReadOnlyList<AnalysisStalenessReason> StalenessReasons)
{
    public bool IsOutdated => StalenessReasons.Count > 0;
}

public enum AnalysisStalenessReason
{
    ProjectRevisionChanged,
    ProjectContentChanged,
    CatalogChanged,
    RuleSetChanged,
    EvaluatorVersionChanged
}
