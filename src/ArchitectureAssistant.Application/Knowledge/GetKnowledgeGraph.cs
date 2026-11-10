using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using MediatR;

namespace ArchitectureAssistant.Application.Knowledge;

public sealed record GetKnowledgeGraphQuery : IRequest<Result<KnowledgeGraphProjection>>;

public sealed class GetKnowledgeGraphHandler(
    IKnowledgeCatalog catalog,
    KnowledgeGraphProjector projector)
    : IRequestHandler<GetKnowledgeGraphQuery, Result<KnowledgeGraphProjection>>
{
    public async Task<Result<KnowledgeGraphProjection>> Handle(
        GetKnowledgeGraphQuery request,
        CancellationToken cancellationToken)
    {
        var catalogResult = await catalog.GetSnapshotAsync(cancellationToken);
        if (!catalogResult.TryGetValue(out var snapshot))
        {
            return Result<KnowledgeGraphProjection>.Failure(catalogResult.Errors);
        }

        return projector.Project(
            snapshot.ContentHash,
            KnowledgeGraphCatalogMapper.Map(snapshot.Items));
    }
}
