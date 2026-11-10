using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Application.Abstractions.Persistence;

public interface IAnalysisRepository
{
    Task<Result<AnalysisSnapshot>> SaveAsync(
        AnalysisSnapshot snapshot,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<AnalysisSnapshot>>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
