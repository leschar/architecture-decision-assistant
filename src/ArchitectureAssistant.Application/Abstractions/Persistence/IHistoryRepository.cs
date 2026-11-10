using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.History;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Application.Abstractions.Persistence;

public sealed record HistoryPolicyLoadResult(
    HistorySimilarityPolicy? Policy,
    IReadOnlyList<HistoryDiagnostic> Diagnostics);

public interface IHistoryRepository
{
    Task<Result<HistoricalProjectRecord>> ArchiveAsync(
        Project project,
        string archiveId,
        string confirmedBy,
        string? note,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken);

    Task<Result<HistoryListResult>> ListAsync(CancellationToken cancellationToken);

    Task<Result<Project>> LoadArchivedProjectAsync(
        string archiveId,
        CancellationToken cancellationToken);

    Task<Result<bool>> RemoveAsync(
        string archiveId,
        bool confirmed,
        CancellationToken cancellationToken);

    Task<Result<HistoryListResult>> RebuildIndexAsync(CancellationToken cancellationToken);

    Task<Result<HistoryPolicyLoadResult>> LoadSimilarityPolicyAsync(CancellationToken cancellationToken);
}
