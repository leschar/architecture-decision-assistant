using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.History;

public enum HistoryFeatureStatus
{
    Confirmed,
    Unknown,
    NotApplicable
}

public sealed record HistoryFeature(
    string Path,
    HistoryFeatureStatus Status,
    JsonElement? Value,
    string? Unit,
    string? Scenario);

public sealed record HistoryDecisionReference(
    string DecisionId,
    string Title,
    string? DecisionRevision);

public sealed record HistoryArchiveConfirmation(
    string ConfirmedBy,
    DateTimeOffset ConfirmedAt,
    string? Note);

public sealed record HistoricalProjectRecord(
    string SchemaVersion,
    string ArchiveId,
    string ProjectId,
    string ProjectRevision,
    string SnapshotFile,
    string ContentFingerprint,
    DateTimeOffset ArchivedAt,
    HistoryArchiveConfirmation ArchiveConfirmation,
    string ProfileVersion,
    IReadOnlyList<HistoryFeature> Features,
    IReadOnlyList<HistoryDecisionReference> DecisionReferences,
    string? CatalogFingerprint,
    string? AnalysisId);

public sealed record HistoryIndexEntry(
    string ArchiveId,
    string RecordFile,
    string ProjectId,
    DateTimeOffset ArchivedAt,
    string ContentFingerprint);

public sealed record HistoryIndex(
    string SchemaVersion,
    long IndexRevision,
    IReadOnlyList<HistoryIndexEntry> Entries);

public sealed record HistoryDiagnostic(string Code, string Message);

public sealed record HistoryListResult(
    IReadOnlyList<HistoricalProjectRecord> Records,
    IReadOnlyList<HistoryDiagnostic> Diagnostics,
    bool IndexRebuilt);

public sealed record HistorySimilarityDimension(
    string Id,
    string FeaturePath,
    decimal Weight,
    string Comparison,
    decimal? NormalizationMinimum,
    decimal? NormalizationMaximum);

public sealed record HistorySimilarityPolicy(
    string SchemaVersion,
    string PolicyId,
    string Version,
    IReadOnlyList<HistorySimilarityDimension> Dimensions,
    decimal MinimumCoverage);

public sealed record HistoryPolicyDimensionReadiness(
    string FeaturePath,
    string Comparison,
    int ArchiveCount,
    int ConfirmedCount,
    int UnknownCount,
    long ComparableProjectPairs,
    int DistinctValueCount,
    decimal? ObservedMinimum,
    decimal? ObservedMaximum);

public sealed record HistoryPolicyReadiness(
    int VerifiedArchiveCount,
    long TotalProjectPairs,
    IReadOnlyList<HistoryPolicyDimensionReadiness> Dimensions,
    IReadOnlyList<HistoryDiagnostic> Diagnostics);

public static class HistorySimilarityPolicyRules
{
    private static readonly IReadOnlyDictionary<string, string> SupportedDimensions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["project.businessDomain"] = "Exact",
            ["project.teamSize"] = "NumericLinear",
            ["project.criticality"] = "Exact",
            ["project.scalabilityTarget"] = "Exact",
            ["project.selectedAlternativeIds"] = "SetJaccard",
            ["project.acceptedAlternativeKinds"] = "SetJaccard"
        };

    public static HistoryPolicyReadiness AssessReadiness(
        IReadOnlyList<HistoricalProjectRecord> records,
        IReadOnlyList<HistoryDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var dimensions = SupportedDimensions
            .OrderBy(dimension => dimension.Key, StringComparer.Ordinal)
            .Select(dimension => AssessDimension(records, dimension.Key, dimension.Value))
            .ToArray();
        return new HistoryPolicyReadiness(
            records.Count,
            (long)records.Count * (records.Count - 1) / 2,
            dimensions,
            diagnostics.ToArray());
    }

    public static Result<bool> Validate(HistorySimilarityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.SchemaVersion != "1.0" ||
            !Matches(policy.PolicyId, "^[a-z0-9]+(?:-[a-z0-9]+)*$") ||
            !Matches(policy.Version, "^\\d+\\.\\d+\\.\\d+$") ||
            policy.MinimumCoverage is <= 0 or > 1 ||
            policy.Dimensions is null ||
            policy.Dimensions.Count == 0)
        {
            return InvalidPolicy();
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        decimal totalWeight = 0;
        foreach (var dimension in policy.Dimensions)
        {
            if (dimension is null ||
                !Matches(dimension.Id, "^[a-z0-9]+(?:-[a-z0-9]+)*$") ||
                !ids.Add(dimension.Id) ||
                string.IsNullOrWhiteSpace(dimension.FeaturePath) ||
                !SupportedDimensions.TryGetValue(dimension.FeaturePath, out var expectedComparison) ||
                !paths.Add(dimension.FeaturePath) ||
                dimension.Comparison != expectedComparison ||
                dimension.Weight <= 0 ||
                decimal.MaxValue - totalWeight < dimension.Weight)
            {
                return InvalidPolicy();
            }

            if (dimension.Comparison == "NumericLinear")
            {
                if (dimension.NormalizationMinimum is not decimal minimum ||
                    dimension.NormalizationMaximum is not decimal maximum ||
                    maximum <= minimum)
                {
                    return InvalidPolicy();
                }
            }
            else if (dimension.NormalizationMinimum is not null ||
                     dimension.NormalizationMaximum is not null)
            {
                return InvalidPolicy();
            }

            totalWeight += dimension.Weight;
        }

        return Result<bool>.Success(true);
    }

    private static bool Matches(string? value, string pattern) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant);

    private static HistoryPolicyDimensionReadiness AssessDimension(
        IReadOnlyList<HistoricalProjectRecord> records,
        string featurePath,
        string comparison)
    {
        var canonicalValues = new HashSet<string>(StringComparer.Ordinal);
        var comparableGroups =
            new Dictionary<(string SchemaVersion, string ProfileVersion, string? Unit, string? Scenario), long>();
        var confirmedCount = 0;
        decimal? observedMinimum = null;
        decimal? observedMaximum = null;

        foreach (var record in records)
        {
            var matches = record.Features.Where(feature => feature.Path == featurePath).Take(2).ToArray();
            if (matches.Length != 1 ||
                !TryGetComparableValue(matches[0], comparison, out var canonicalValue, out var number))
            {
                continue;
            }

            confirmedCount++;
            canonicalValues.Add(canonicalValue);
            if (number is decimal observed)
            {
                observedMinimum = observedMinimum is decimal minimum
                    ? Math.Min(minimum, observed)
                    : observed;
                observedMaximum = observedMaximum is decimal maximum
                    ? Math.Max(maximum, observed)
                    : observed;
            }

            var key = (record.SchemaVersion, record.ProfileVersion, matches[0].Unit, matches[0].Scenario);
            comparableGroups[key] = comparableGroups.GetValueOrDefault(key) + 1;
        }

        var comparablePairs = comparableGroups.Values.Sum(count => count * (count - 1) / 2);
        return new HistoryPolicyDimensionReadiness(
            featurePath,
            comparison,
            records.Count,
            confirmedCount,
            records.Count - confirmedCount,
            comparablePairs,
            canonicalValues.Count,
            observedMinimum,
            observedMaximum);
    }

    private static bool TryGetComparableValue(
        HistoryFeature feature,
        string comparison,
        out string canonicalValue,
        out decimal? number)
    {
        canonicalValue = string.Empty;
        number = null;
        if (feature.Status != HistoryFeatureStatus.Confirmed || feature.Value is not JsonElement value)
        {
            return false;
        }

        switch (comparison)
        {
            case "Exact" when value.ValueKind == JsonValueKind.String:
                canonicalValue = value.GetString()!;
                return true;
            case "Exact" when value.ValueKind is JsonValueKind.Number:
                if (!value.TryGetDecimal(out var exactNumber))
                {
                    return false;
                }

                canonicalValue = exactNumber.ToString("G29", CultureInfo.InvariantCulture);
                return true;
            case "Exact" when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                canonicalValue = value.GetBoolean().ToString(CultureInfo.InvariantCulture);
                return true;
            case "NumericLinear":
                if (string.IsNullOrWhiteSpace(feature.Unit) ||
                    !value.TryGetDecimal(out var numericValue))
                {
                    return false;
                }

                number = numericValue;
                canonicalValue = numericValue.ToString("G29", CultureInfo.InvariantCulture);
                return true;
            case "SetJaccard" when value.ValueKind == JsonValueKind.Array:
                var items = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        return false;
                    }

                    items.Add(item.GetString()!);
                }

                if (items.Count == 0)
                {
                    return false;
                }

                canonicalValue = JsonSerializer.Serialize(items);
                return true;
            default:
                return false;
        }
    }

    private static Result<bool> InvalidPolicy() =>
        Result<bool>.Failure(new DomainError(
            "history.policy.invalid",
            "The similarity policy contains unsupported or duplicate dimensions, comparators, weights, or metadata."));
}

public enum HistoryComparisonStatus
{
    Comparable,
    NotComparable,
    InsufficientCoverage
}

public sealed record HistoryDimensionResult(
    string DimensionId,
    string FeaturePath,
    decimal Weight,
    decimal? Similarity,
    string? ExcludedReason);

public sealed record HistoryComparisonResult(
    HistoryComparisonStatus Status,
    decimal? Similarity,
    decimal Coverage,
    string PolicyId,
    string PolicyVersion,
    IReadOnlyList<HistoryDimensionResult> Dimensions);

public static class HistoricalProjectRecordFactory
{
    public const string CurrentProfileVersion = "1.0.0";

    public static HistoricalProjectRecord Create(
        Project project,
        string archiveId,
        string confirmedBy,
        DateTimeOffset confirmedAt,
        string snapshotFile,
        string contentFingerprint,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFingerprint);

        var features = new List<HistoryFeature>
        {
            Confirmed("project.name", project.Name),
            Confirmed("project.businessDomain", project.Context.BusinessDomain),
            Confirmed("project.teamSize", project.Context.TeamSize, "people"),
            Confirmed("project.criticality", project.Context.Criticality?.ToString()),
            Confirmed("project.scalabilityTarget", project.Context.ScalabilityTarget?.ToString())
        };
        var selectedAlternatives = project.Decisions
            .Where(decision => decision.Status == DecisionStatus.Accepted)
            .Select(decision => decision.SelectedAlternativeId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        features.Add(Confirmed("project.selectedAlternativeIds", selectedAlternatives));
        if (project.Decisions.Any(decision => decision.Status == DecisionStatus.Accepted))
        {
            features.Add(Confirmed(
                "project.acceptedAlternativeKinds",
                project.Decisions.Where(decision => decision.Status == DecisionStatus.Accepted)
                    .Select(decision => decision.SelectedAlternativeKind)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()));
        }

        var decisions = project.Decisions
            .OrderBy(decision => decision.Id)
            .Select(decision => new HistoryDecisionReference(
                decision.Id.ToString("D"),
                decision.SelectedAlternativeId,
                decision.Revision.ToString()))
            .ToArray();
        return new HistoricalProjectRecord(
            "1.0",
            archiveId,
            project.Id.ToString("D"),
            project.Revision.ToString(),
            snapshotFile,
            contentFingerprint,
            confirmedAt,
            new HistoryArchiveConfirmation(confirmedBy.Trim(), confirmedAt, note?.Trim()),
            CurrentProfileVersion,
            features,
            decisions,
            null,
            null);
    }

    private static HistoryFeature Confirmed<T>(string path, T? value, string? unit = null) =>
        value is null
            ? new HistoryFeature(path, HistoryFeatureStatus.Unknown, null, null, null)
            : new HistoryFeature(path, HistoryFeatureStatus.Confirmed, JsonSerializer.SerializeToElement(value), unit, null);
}

public static class HistorySimilarityComparer
{
    public static HistoryComparisonResult Compare(
        HistoricalProjectRecord current,
        HistoricalProjectRecord historical,
        HistorySimilarityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(historical);
        ArgumentNullException.ThrowIfNull(policy);

        if (current.SchemaVersion != historical.SchemaVersion ||
            current.ProfileVersion != historical.ProfileVersion)
        {
            return new HistoryComparisonResult(
                HistoryComparisonStatus.NotComparable,
                null,
                0,
                policy.PolicyId,
                policy.Version,
                policy.Dimensions.OrderBy(dimension => dimension.Id, StringComparer.Ordinal)
                    .Select(dimension => Excluded(dimension, "Versão de schema ou perfil incompatível."))
                    .ToArray());
        }

        var results = new List<HistoryDimensionResult>();
        decimal comparableWeight = 0;
        decimal weightedSimilarity = 0;
        foreach (var dimension in policy.Dimensions.OrderBy(dimension => dimension.Id, StringComparer.Ordinal))
        {
            var leftMatches = current.Features.Where(feature => feature.Path == dimension.FeaturePath).Take(2).ToArray();
            var rightMatches = historical.Features.Where(feature => feature.Path == dimension.FeaturePath).Take(2).ToArray();
            if (leftMatches.Length != 1 || rightMatches.Length != 1)
            {
                results.Add(Excluded(dimension, "Dimensão ausente ou ambígua em um dos snapshots."));
                continue;
            }

            var left = leftMatches[0];
            var right = rightMatches[0];
            if (left.Status != HistoryFeatureStatus.Confirmed || right.Status != HistoryFeatureStatus.Confirmed ||
                left.Value is null || right.Value is null)
            {
                results.Add(Excluded(dimension, "Valores não confirmados não são comparáveis."));
                continue;
            }

            if (!string.Equals(left.Unit, right.Unit, StringComparison.Ordinal) ||
                !string.Equals(left.Scenario, right.Scenario, StringComparison.Ordinal))
            {
                results.Add(Excluded(dimension, "Unidade ou cenário incompatível."));
                continue;
            }

            if (dimension.Comparison == "NumericLinear" && string.IsNullOrWhiteSpace(left.Unit))
            {
                results.Add(Excluded(dimension, "Dimensão numérica sem unidade conhecida."));
                continue;
            }

            var similarity = CompareValues(left.Value.Value, right.Value.Value, dimension);
            if (similarity is null)
            {
                results.Add(Excluded(dimension, "Tipo, conteúdo ou comparador incompatível."));
                continue;
            }

            comparableWeight += dimension.Weight;
            weightedSimilarity += dimension.Weight * similarity.Value;
            results.Add(new HistoryDimensionResult(
                dimension.Id,
                dimension.FeaturePath,
                dimension.Weight,
                similarity,
                null));
        }

        var totalWeight = policy.Dimensions.Sum(dimension => dimension.Weight);
        var coverage = totalWeight > 0 ? comparableWeight / totalWeight : 0;
        if (comparableWeight == 0)
        {
            return new HistoryComparisonResult(
                HistoryComparisonStatus.NotComparable,
                null,
                coverage,
                policy.PolicyId,
                policy.Version,
                results);
        }

        return new HistoryComparisonResult(
            coverage < policy.MinimumCoverage
                ? HistoryComparisonStatus.InsufficientCoverage
                : HistoryComparisonStatus.Comparable,
            weightedSimilarity / comparableWeight,
            coverage,
            policy.PolicyId,
            policy.Version,
            results);
    }

    private static decimal? CompareValues(
        JsonElement left,
        JsonElement right,
        HistorySimilarityDimension dimension)
    {
        switch (dimension.Comparison)
        {
            case "Exact":
                return ExactSimilarity(left, right);
            case "NumericLinear":
                if (!left.TryGetDecimal(out var leftNumber) ||
                    !right.TryGetDecimal(out var rightNumber) ||
                    dimension.NormalizationMinimum is not decimal minimum ||
                    dimension.NormalizationMaximum is not decimal maximum ||
                    maximum <= minimum)
                {
                    return null;
                }

                var span = maximum - minimum;
                return Math.Clamp(1m - Math.Abs(leftNumber - rightNumber) / span, 0m, 1m);
            case "SetJaccard":
                if (left.ValueKind != JsonValueKind.Array || right.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var leftSet = ReadStringSet(left);
                var rightSet = ReadStringSet(right);
                if (leftSet is null || rightSet is null || leftSet.Count == 0 || rightSet.Count == 0)
                {
                    return null;
                }

                return (decimal)leftSet.Intersect(rightSet, StringComparer.Ordinal).Count() /
                       leftSet.Union(rightSet, StringComparer.Ordinal).Count();
            default:
                return null;
        }
    }

    private static decimal? ExactSimilarity(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return null;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString() ? 1m : 0m,
            JsonValueKind.Number => left.TryGetDecimal(out var leftNumber) &&
                                    right.TryGetDecimal(out var rightNumber)
                ? leftNumber == rightNumber ? 1m : 0m
                : null,
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean() ? 1m : 0m,
            _ => null
        };
    }

    private static HashSet<string>? ReadStringSet(JsonElement value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                return null;
            }

            result.Add(item.GetString()!);
        }

        return result;
    }

    private static HistoryDimensionResult Excluded(HistorySimilarityDimension dimension, string reason) =>
        new(dimension.Id, dimension.FeaturePath, dimension.Weight, null, reason);
}
