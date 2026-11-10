using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Application.Knowledge;

public interface IKnowledgeCatalog
{
    Task<Result<KnowledgeCatalogSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken);
}
