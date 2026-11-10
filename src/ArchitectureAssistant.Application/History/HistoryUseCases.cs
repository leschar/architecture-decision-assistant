using System.Text.Json;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Projects;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.History;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.History;

public sealed record ArchiveProjectHistoryCommand(
    Guid ProjectId,
    bool Confirmed,
    string ConfirmedBy,
    string? Note)
    : IRequest<Result<HistoricalProjectRecord>>;

public sealed class ArchiveProjectHistoryValidator : AbstractValidator<ArchiveProjectHistoryCommand>
{
    public ArchiveProjectHistoryValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Confirmed).Equal(true)
            .WithErrorCode("history.archive.confirmation_required")
            .WithMessage("Explicit confirmation is required before archiving the project.");
        RuleFor(command => command.ConfirmedBy).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Note).MaximumLength(500);
    }
}

public sealed class ArchiveProjectHistoryHandler(
    IProjectRepository projects,
    IHistoryRepository history,
    IValidator<ArchiveProjectHistoryCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveProjectHistoryCommand, Result<HistoricalProjectRecord>>
{
    public async Task<Result<HistoricalProjectRecord>> Handle(
        ArchiveProjectHistoryCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<HistoricalProjectRecord>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await projects.LoadAsync(request.ProjectId, cancellationToken);
        return loaded.TryGetValue(out var project)
            ? await history.ArchiveAsync(
                project,
                Guid.NewGuid().ToString("N"),
                request.ConfirmedBy,
                request.Note,
                timeProvider.GetUtcNow(),
                cancellationToken)
            : Result<HistoricalProjectRecord>.Failure(loaded.Errors);
    }
}

public sealed record SearchHistoricalProjectsQuery(string? SearchText, Guid? CompareToProjectId)
    : IRequest<Result<HistorySearchResult>>;

public sealed class SearchHistoricalProjectsValidator : AbstractValidator<SearchHistoricalProjectsQuery>
{
    public SearchHistoricalProjectsValidator()
    {
        RuleFor(query => query.SearchText).MaximumLength(200);
        RuleFor(query => query.CompareToProjectId).NotEqual(Guid.Empty).When(query => query.CompareToProjectId.HasValue);
    }
}

public sealed record HistorySearchItem(
    HistoricalProjectRecord Record,
    HistoryComparisonResult? Comparison);

public sealed record HistorySearchResult(
    IReadOnlyList<HistorySearchItem> Projects,
    IReadOnlyList<HistoryDiagnostic> Diagnostics,
    bool SimilarityPolicyAvailable);

public sealed class SearchHistoricalProjectsHandler(
    IHistoryRepository history,
    IProjectRepository projects,
    IValidator<SearchHistoricalProjectsQuery> validator)
    : IRequestHandler<SearchHistoricalProjectsQuery, Result<HistorySearchResult>>
{
    public async Task<Result<HistorySearchResult>> Handle(
        SearchHistoricalProjectsQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<HistorySearchResult>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var listed = await history.ListAsync(cancellationToken);
        if (!listed.TryGetValue(out var result))
        {
            return Result<HistorySearchResult>.Failure(listed.Errors);
        }

        var diagnostics = result.Diagnostics.ToList();
        var records = result.Records.Where(record => HistorySearchFilter.Matches(record, request.SearchText)).ToArray();
        HistorySimilarityPolicy? policy = null;
        HistoricalProjectRecord? currentRecord = null;
        if (request.CompareToProjectId is Guid projectId)
        {
            var policyResult = await history.LoadSimilarityPolicyAsync(cancellationToken);
            if (!policyResult.TryGetValue(out var loadedPolicy))
            {
                return Result<HistorySearchResult>.Failure(policyResult.Errors);
            }

            policy = loadedPolicy.Policy;
            diagnostics.AddRange(loadedPolicy.Diagnostics);
            var current = await projects.LoadAsync(projectId, cancellationToken);
            if (!current.TryGetValue(out var project))
            {
                return Result<HistorySearchResult>.Failure(current.Errors);
            }

            currentRecord = HistoricalProjectRecordFactory.Create(
                project,
                "current",
                "current workspace",
                project.UpdatedAt,
                "current.json",
                "sha256:" + new string('0', 64));
        }

        var items = records.Select(record => new HistorySearchItem(
                record,
                policy is not null && currentRecord is not null
                    ? HistorySimilarityComparer.Compare(currentRecord, record, policy)
                    : null))
            .OrderBy(item => item.Comparison is null ? 2 : item.Comparison.Status == HistoryComparisonStatus.Comparable ? 0 : 1)
            .ThenByDescending(item => item.Comparison?.Status == HistoryComparisonStatus.Comparable
                ? item.Comparison.Similarity
                : null)
            .ThenByDescending(item => item.Record.ArchivedAt)
            .ThenBy(item => item.Record.ArchiveId, StringComparer.Ordinal)
            .ToArray();

        return Result<HistorySearchResult>.Success(new HistorySearchResult(
            items,
            diagnostics,
            policy is not null));
    }

}

internal static class HistorySearchFilter
{
    public static bool Matches(HistoricalProjectRecord record, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var term = searchText.Trim();
        if (record.ArchiveId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            record.ProjectId.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return record.Features
            .Where(feature => feature.Path is "project.name" or "project.businessDomain")
            .Any(feature => feature.Value is JsonElement value &&
                            value.ValueKind == JsonValueKind.String &&
                            value.GetString()!.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record RemoveHistoricalProjectCommand(string ArchiveId, bool Confirmed)
    : IRequest<Result<bool>>;

public sealed class RemoveHistoricalProjectValidator : AbstractValidator<RemoveHistoricalProjectCommand>
{
    public RemoveHistoricalProjectValidator()
    {
        RuleFor(command => command.ArchiveId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Confirmed).Equal(true)
            .WithErrorCode("history.remove.confirmation_required")
            .WithMessage("Explicit confirmation is required before removing a historical project.");
    }
}

public sealed class RemoveHistoricalProjectHandler(
    IHistoryRepository history,
    IValidator<RemoveHistoricalProjectCommand> validator)
    : IRequestHandler<RemoveHistoricalProjectCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveHistoricalProjectCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<bool>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        return await history.RemoveAsync(request.ArchiveId, request.Confirmed, cancellationToken);
    }
}

public sealed record RebuildHistoryIndexCommand : IRequest<Result<HistoryListResult>>;

public sealed class RebuildHistoryIndexHandler(IHistoryRepository history)
    : IRequestHandler<RebuildHistoryIndexCommand, Result<HistoryListResult>>
{
    public Task<Result<HistoryListResult>> Handle(
        RebuildHistoryIndexCommand request,
        CancellationToken cancellationToken) =>
        history.RebuildIndexAsync(cancellationToken);
}

public sealed record LoadHistoricalProjectQuery(string ArchiveId)
    : IRequest<Result<ProjectView>>;

public sealed class LoadHistoricalProjectValidator : AbstractValidator<LoadHistoricalProjectQuery>
{
    public LoadHistoricalProjectValidator() =>
        RuleFor(query => query.ArchiveId).NotEmpty().MaximumLength(120);
}

public sealed class LoadHistoricalProjectHandler(
    IHistoryRepository history,
    IValidator<LoadHistoricalProjectQuery> validator)
    : IRequestHandler<LoadHistoricalProjectQuery, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        LoadHistoricalProjectQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ProjectView>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await history.LoadArchivedProjectAsync(request.ArchiveId, cancellationToken);
        return loaded.TryGetValue(out var project)
            ? Result<ProjectView>.Success(ProjectView.From(project))
            : Result<ProjectView>.Failure(loaded.Errors);
    }
}

public sealed record ReuseHistoricalDecisionCommand(
    Guid ProjectId,
    string ArchiveId,
    Guid DecisionId,
    string RecordedBy)
    : IRequest<Result<ProjectView>>;

public sealed class ReuseHistoricalDecisionValidator : AbstractValidator<ReuseHistoricalDecisionCommand>
{
    public ReuseHistoricalDecisionValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ArchiveId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.DecisionId).NotEmpty();
        RuleFor(command => command.RecordedBy).NotEmpty().MaximumLength(120);
    }
}

public sealed class ReuseHistoricalDecisionHandler(
    IHistoryRepository history,
    IProjectRepository projects,
    IValidator<ReuseHistoricalDecisionCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<ReuseHistoricalDecisionCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        ReuseHistoricalDecisionCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ProjectView>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var archivedProjectResult = await history.LoadArchivedProjectAsync(request.ArchiveId, cancellationToken);
        if (!archivedProjectResult.TryGetValue(out var archivedProject))
        {
            return Result<ProjectView>.Failure(archivedProjectResult.Errors);
        }

        var decisionMatches = archivedProject.Decisions.Where(decision => decision.Id == request.DecisionId).Take(2).ToArray();
        if (decisionMatches.Length != 1)
        {
            return Result<ProjectView>.Failure(
                new DomainError("history.decision.not_found", "The requested decision is not present in the verified historical snapshot."));
        }

        var records = await history.ListAsync(cancellationToken);
        if (!records.TryGetValue(out var historyList))
        {
            return Result<ProjectView>.Failure(records.Errors);
        }

        var archiveMatches = historyList.Records.Where(record => record.ArchiveId == request.ArchiveId).Take(2).ToArray();
        if (archiveMatches.Length != 1 ||
            !Guid.TryParse(archiveMatches[0].ProjectId, out var sourceProjectId))
        {
            return Result<ProjectView>.Failure(
                new DomainError("history.archive.invalid", "The source project record is invalid or missing."));
        }

        var sourceDecision = decisionMatches[0];
        var now = timeProvider.GetUtcNow();
        var proposed = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            sourceDecision.SelectedAlternativeId,
            sourceDecision.SelectedAlternativeKind,
            sourceDecision.ConsideredAlternatives,
            DecisionStatus.Proposed,
            sourceDecision.Justification,
            sourceDecision.Consequences,
            request.RecordedBy,
            now,
            selectedAlternativeVersion: sourceDecision.SelectedAlternativeVersion,
            historicalSource: new HistoricalDecisionSource(
                request.ArchiveId,
                sourceProjectId,
                sourceDecision.Id,
                archivedProject.Revision.ToString(),
                archiveMatches[0].ContentFingerprint));
        if (!proposed.TryGetValue(out var proposal))
        {
            return Result<ProjectView>.Failure(proposed.Errors);
        }

        var current = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!current.TryGetValue(out var project))
        {
            return Result<ProjectView>.Failure(current.Errors);
        }

        var updated = project.AddDecision(proposal, now);
        if (!updated.TryGetValue(out var revisedProject))
        {
            return Result<ProjectView>.Failure(updated.Errors);
        }

        var saved = await projects.SaveAsync(revisedProject, project.Revision, cancellationToken);
        return saved.TryGetValue(out var savedProject)
            ? Result<ProjectView>.Success(ProjectView.From(savedProject))
            : Result<ProjectView>.Failure(saved.Errors);
    }
}

public sealed record GetHistoryStatisticsQuery(string? SearchText)
    : IRequest<Result<HistoryStatistics>>;

public sealed record HistoryDistribution(string Value, int Count);

public sealed record HistoryStatistics(
    int Population,
    int ProjectsWithDomain,
    int ProjectsWithAcceptedDecision,
    IReadOnlyList<HistoryDistribution> ProjectsByDomain,
    IReadOnlyList<HistoryDistribution> AcceptedAlternatives,
    IReadOnlyList<HistoryDiagnostic> Diagnostics);

public sealed class GetHistoryStatisticsHandler(
    IHistoryRepository history,
    IValidator<SearchHistoricalProjectsQuery> validator)
    : IRequestHandler<GetHistoryStatisticsQuery, Result<HistoryStatistics>>
{
    public async Task<Result<HistoryStatistics>> Handle(
        GetHistoryStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(
            new SearchHistoricalProjectsQuery(request.SearchText, null),
            cancellationToken);
        if (!validation.IsValid)
        {
            return Result<HistoryStatistics>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var listed = await history.ListAsync(cancellationToken);
        if (!listed.TryGetValue(out var result))
        {
            return Result<HistoryStatistics>.Failure(listed.Errors);
        }

        var records = result.Records
            .Where(record => HistorySearchFilter.Matches(record, request.SearchText))
            .ToArray();
        var domains = records.Select(record => GetString(record, "project.businessDomain"))
            .Where(value => value is not null)
            .GroupBy(value => value!, StringComparer.Ordinal)
            .Select(group => new HistoryDistribution(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();
        var acceptedAlternatives = records.SelectMany(record => GetStringArray(record, "project.selectedAlternativeIds"))
            .GroupBy(value => value, StringComparer.Ordinal)
            .Select(group => new HistoryDistribution(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();

        return Result<HistoryStatistics>.Success(new HistoryStatistics(
            records.Length,
            records.Count(record => GetString(record, "project.businessDomain") is not null),
            records.Count(record => GetStringArray(record, "project.selectedAlternativeIds").Any()),
            domains,
            acceptedAlternatives,
            result.Diagnostics));
    }

    private static string? GetString(HistoricalProjectRecord record, string path) =>
        record.Features.FirstOrDefault(feature => feature.Path == path)?.Value is JsonElement value &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<string> GetStringArray(HistoricalProjectRecord record, string path)
    {
        if (record.Features.FirstOrDefault(feature => feature.Path == path)?.Value is not JsonElement value ||
            value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }
}

public sealed record GetHistoryPolicyReadinessQuery : IRequest<Result<HistoryPolicyReadiness>>;

public sealed class GetHistoryPolicyReadinessHandler(IHistoryRepository history)
    : IRequestHandler<GetHistoryPolicyReadinessQuery, Result<HistoryPolicyReadiness>>
{
    public async Task<Result<HistoryPolicyReadiness>> Handle(
        GetHistoryPolicyReadinessQuery request,
        CancellationToken cancellationToken)
    {
        var listed = await history.ListAsync(cancellationToken);
        if (!listed.TryGetValue(out var result))
        {
            return Result<HistoryPolicyReadiness>.Failure(listed.Errors);
        }

        return Result<HistoryPolicyReadiness>.Success(
            HistorySimilarityPolicyRules.AssessReadiness(result.Records, result.Diagnostics));
    }
}
