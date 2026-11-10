using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Diagrams;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Diagrams;

public sealed record SaveArchitecturalDiagramSnapshotCommand(
    Guid ProjectId,
    ArchitecturalDiagramType DiagramType,
    string ScopeId,
    string ExpectedFingerprint)
    : IRequest<Result<ArchitecturalDiagram>>;

public sealed class SaveArchitecturalDiagramSnapshotValidator : AbstractValidator<SaveArchitecturalDiagramSnapshotCommand>
{
    public SaveArchitecturalDiagramSnapshotValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.DiagramType).IsInEnum();
        RuleFor(command => command.ScopeId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ExpectedFingerprint).Matches("^sha256:[a-f0-9]{64}$");
    }
}

public sealed class SaveArchitecturalDiagramSnapshotHandler(
    IProjectRepository projectRepository,
    IDiagramRepository diagramRepository,
    IValidator<SaveArchitecturalDiagramSnapshotCommand> validator,
    ArchitecturalDiagramProjector projector,
    TimeProvider timeProvider)
    : IRequestHandler<SaveArchitecturalDiagramSnapshotCommand, Result<ArchitecturalDiagram>>
{
    public async Task<Result<ArchitecturalDiagram>> Handle(
        SaveArchitecturalDiagramSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ArchitecturalDiagram>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await projectRepository.LoadAsync(request.ProjectId, cancellationToken);
        if (!loaded.TryGetValue(out var project))
        {
            return Result<ArchitecturalDiagram>.Failure(loaded.Errors);
        }

        var projection = projector.Project(project, request.DiagramType, request.ScopeId, timeProvider.GetUtcNow());
        if (!projection.TryGetValue(out var diagram))
        {
            return Result<ArchitecturalDiagram>.Failure(projection.Errors);
        }

        if (!string.Equals(diagram.SourceFingerprint, request.ExpectedFingerprint, StringComparison.Ordinal))
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.preview.stale", "The project changed after this preview was generated. Generate a current preview before saving."));
        }

        return await diagramRepository.SaveAsync(diagram, cancellationToken);
    }
}
