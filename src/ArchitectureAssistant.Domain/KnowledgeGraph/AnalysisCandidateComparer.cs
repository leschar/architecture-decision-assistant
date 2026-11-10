using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Domain.KnowledgeGraph;

public sealed class AnalysisCandidateComparer
{
    public Result<AnalysisCandidateComparison> Compare(
        AnalysisSnapshot snapshot,
        string firstCandidateId,
        string secondCandidateId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(firstCandidateId) ||
            string.IsNullOrWhiteSpace(secondCandidateId))
        {
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.candidate_id_required",
                    "Both candidate IDs are required for comparison."));
        }

        if (string.Equals(firstCandidateId, secondCandidateId, StringComparison.Ordinal))
        {
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.same_candidate",
                    "Two different candidates are required for comparison."));
        }

        var candidates = snapshot.CandidateGroups.SelectMany(group => group.Candidates).ToArray();
        var firstMatches = candidates.Where(candidate =>
            string.Equals(candidate.CandidateId, firstCandidateId, StringComparison.Ordinal)).ToArray();
        var secondMatches = candidates.Where(candidate =>
            string.Equals(candidate.CandidateId, secondCandidateId, StringComparison.Ordinal)).ToArray();
        if (firstMatches.Length > 1 || secondMatches.Length > 1)
        {
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.candidate_not_unique",
                    "The snapshot contains duplicate candidate IDs for one or both requested candidates."));
        }

        var first = firstMatches.FirstOrDefault();
        var second = secondMatches.FirstOrDefault();
        if (first is null || second is null)
        {
            var missingIds = new[]
                {
                    first is null ? firstCandidateId : null,
                    second is null ? secondCandidateId : null
                }
                .Where(id => id is not null)
                .Order(StringComparer.Ordinal);
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.candidate_not_found",
                    $"Candidate(s) '{string.Join("', '", missingIds)}' are not present in analysis '{snapshot.AnalysisId}'."));
        }

        if (!string.Equals(first.CandidateKind, second.CandidateKind, StringComparison.Ordinal))
        {
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.kind_mismatch",
                    "Candidates of different kinds cannot be compared on the same adequacy scale."));
        }

        var relationFindings = first.RelationFindings
            .Concat(second.RelationFindings)
            .Where(finding =>
                (finding.SourceId == firstCandidateId && finding.TargetId == secondCandidateId) ||
                (finding.SourceId == secondCandidateId && finding.TargetId == firstCandidateId))
            .GroupBy(
                finding => finding.RelationKey ?? $"{finding.Type}/{finding.SourceId}/{finding.TargetId}",
                StringComparer.Ordinal)
            .Select(group => group.OrderBy(finding => finding.SourceId, StringComparer.Ordinal)
                .ThenBy(finding => finding.TargetId, StringComparer.Ordinal)
                .First())
            .OrderBy(finding => finding.Type, StringComparer.Ordinal)
            .ThenBy(finding => finding.SourceId, StringComparer.Ordinal)
            .ThenBy(finding => finding.TargetId, StringComparer.Ordinal)
            .ThenBy(finding => finding.RelationKey, StringComparer.Ordinal)
            .ToArray();

        var firstDependencies = snapshot.GraphExpansion?.Dependencies
            .Where(dependency => dependency.RootId == firstCandidateId)
            .OrderBy(dependency => dependency.TargetId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.RelationKey, StringComparer.Ordinal)
            .ToArray() ?? [];
        var secondDependencies = snapshot.GraphExpansion?.Dependencies
            .Where(dependency => dependency.RootId == secondCandidateId)
            .OrderBy(dependency => dependency.TargetId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.RelationKey, StringComparer.Ordinal)
            .ToArray() ?? [];

        var firstRequired = firstDependencies
            .Where(dependency => dependency.Applicability == "Applicable")
            .Select(dependency => dependency.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var secondRequired = secondDependencies
            .Where(dependency => dependency.Applicability == "Applicable")
            .Select(dependency => dependency.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var dependencyDataStatus = snapshot.GraphExpansion switch
        {
            { Truncated: true } => "Truncated",
            not null => "Complete",
            _ when snapshot.GraphProjection is not null => "DirectOnly",
            _ => "Unavailable"
        };
        var limitations = new SortedSet<string>(snapshot.Limitations, StringComparer.Ordinal);
        if (dependencyDataStatus == "Truncated")
        {
            limitations.Add(
                "Dependency comparison uses a truncated expansion and may omit paths or dependencies.");
        }
        else if (dependencyDataStatus == "DirectOnly")
        {
            limitations.Add(
                "This snapshot has a graph projection but no dependency expansion; comparison is limited to direct relation findings.");
        }
        else if (dependencyDataStatus == "Unavailable")
        {
            limitations.Add(
                "This historical snapshot has no graph projection or expansion; only relation findings already stored on candidates are included.");
        }

        return Result<AnalysisCandidateComparison>.Success(new AnalysisCandidateComparison(
            "1.0",
            snapshot.AnalysisId,
            snapshot.ProjectId,
            snapshot.CatalogHash,
            ToComparisonSide(first),
            ToComparisonSide(second),
            Array.AsReadOnly(relationFindings),
            Array.AsReadOnly(firstDependencies),
            Array.AsReadOnly(secondDependencies),
            Array.AsReadOnly(firstRequired.Intersect(secondRequired, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(firstRequired.Except(secondRequired, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(secondRequired.Except(firstRequired, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray()),
            dependencyDataStatus,
            Array.AsReadOnly(limitations.ToArray())));
    }

    private static CandidateComparisonSide ToComparisonSide(AnalysisCandidateResult candidate) =>
        new(
            candidate.CandidateId,
            candidate.CandidateKind,
            candidate.CandidateName,
            candidate.CandidateVersion,
            candidate.Eligibility,
            candidate.FitScore,
            candidate.EvidenceCoverage);
}
