using ArchitectureAssistant.Domain.Common;
using MediatR;

namespace ArchitectureAssistant.Application.Knowledge;

public sealed record GetKnowledgeCatalogQuery : IRequest<Result<KnowledgeCatalogSnapshot>>;

public sealed class GetKnowledgeCatalogHandler(IKnowledgeCatalog catalog)
    : IRequestHandler<GetKnowledgeCatalogQuery, Result<KnowledgeCatalogSnapshot>>
{
    public Task<Result<KnowledgeCatalogSnapshot>> Handle(
        GetKnowledgeCatalogQuery request,
        CancellationToken cancellationToken) =>
        catalog.GetSnapshotAsync(cancellationToken);
}
