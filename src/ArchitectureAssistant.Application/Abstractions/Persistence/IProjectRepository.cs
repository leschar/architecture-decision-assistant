using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<Result<IReadOnlyList<Project>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<Project>> LoadAsync(Guid projectId, CancellationToken cancellationToken);

    Task<Result<Project>> SaveAsync(
        Project project,
        long? expectedRevision,
        CancellationToken cancellationToken);
}
