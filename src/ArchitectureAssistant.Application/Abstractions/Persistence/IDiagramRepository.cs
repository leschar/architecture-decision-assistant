using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Diagrams;

namespace ArchitectureAssistant.Application.Abstractions.Persistence;

public interface IDiagramRepository
{
    Task<Result<ArchitecturalDiagram>> SaveAsync(
        ArchitecturalDiagram diagram,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ArchitecturalDiagram>>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<Result<ArchitecturalDiagramLayout>> SaveLayoutAsync(
        ArchitecturalDiagram diagram,
        ArchitecturalDiagramLayout layout,
        CancellationToken cancellationToken);

    Task<Result<DiagramLayoutLoadResult>> LoadLayoutAsync(
        ArchitecturalDiagram diagram,
        CancellationToken cancellationToken);

    Task<Result<ArchitecturalDiagram>> LoadSnapshotAsync(
        Guid projectId,
        string diagramId,
        CancellationToken cancellationToken);
}
