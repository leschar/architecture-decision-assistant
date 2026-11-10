using ArchitectureAssistant.Domain.Adrs;
using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Application.Abstractions.Persistence;

public interface IAdrRepository
{
    Task<Result<AdrSnapshot>> SaveAsync(
        AdrSnapshot snapshot,
        long? expectedRevision,
        CancellationToken cancellationToken);

    Task<Result<AdrSnapshot>> LoadLatestAsync(
        Guid projectId,
        string adrId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<AdrSnapshot>>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
