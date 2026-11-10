using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Diagrams;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Diagrams;

public sealed record ListArchitecturalDiagramSnapshotsQuery(Guid ProjectId)
    : IRequest<Result<IReadOnlyList<ArchitecturalDiagram>>>;

public sealed class ListArchitecturalDiagramSnapshotsValidator
    : AbstractValidator<ListArchitecturalDiagramSnapshotsQuery>
{
    public ListArchitecturalDiagramSnapshotsValidator() =>
        RuleFor(query => query.ProjectId).NotEmpty();
}

public sealed class ListArchitecturalDiagramSnapshotsHandler(
    IDiagramRepository diagrams,
    IValidator<ListArchitecturalDiagramSnapshotsQuery> validator)
    : IRequestHandler<ListArchitecturalDiagramSnapshotsQuery, Result<IReadOnlyList<ArchitecturalDiagram>>>
{
    public async Task<Result<IReadOnlyList<ArchitecturalDiagram>>> Handle(
        ListArchitecturalDiagramSnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<IReadOnlyList<ArchitecturalDiagram>>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        return await diagrams.ListAsync(request.ProjectId, cancellationToken);
    }
}

public sealed record LoadArchitecturalDiagramSnapshotQuery(Guid ProjectId, string DiagramId)
    : IRequest<Result<ArchitecturalDiagram>>;

public sealed class LoadArchitecturalDiagramSnapshotValidator
    : AbstractValidator<LoadArchitecturalDiagramSnapshotQuery>
{
    public LoadArchitecturalDiagramSnapshotValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.DiagramId).NotEmpty().MaximumLength(200);
    }
}

public sealed class LoadArchitecturalDiagramSnapshotHandler(
    IDiagramRepository diagrams,
    IValidator<LoadArchitecturalDiagramSnapshotQuery> validator)
    : IRequestHandler<LoadArchitecturalDiagramSnapshotQuery, Result<ArchitecturalDiagram>>
{
    public async Task<Result<ArchitecturalDiagram>> Handle(
        LoadArchitecturalDiagramSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ArchitecturalDiagram>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        return await diagrams.LoadSnapshotAsync(request.ProjectId, request.DiagramId, cancellationToken);
    }
}

public sealed record GetArchitecturalDiagramLayoutQuery(
    Guid ProjectId,
    ArchitecturalDiagramType DiagramType,
    string ScopeId)
    : IRequest<Result<DiagramLayoutLoadResult>>;

public sealed class GetArchitecturalDiagramLayoutValidator
    : AbstractValidator<GetArchitecturalDiagramLayoutQuery>
{
    public GetArchitecturalDiagramLayoutValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.DiagramType).IsInEnum();
        RuleFor(query => query.ScopeId).NotEmpty().MaximumLength(120);
    }
}

public sealed class GetArchitecturalDiagramLayoutHandler(
    IProjectRepository projects,
    IDiagramRepository diagrams,
    IValidator<GetArchitecturalDiagramLayoutQuery> validator,
    ArchitecturalDiagramProjector projector,
    TimeProvider timeProvider)
    : IRequestHandler<GetArchitecturalDiagramLayoutQuery, Result<DiagramLayoutLoadResult>>
{
    public async Task<Result<DiagramLayoutLoadResult>> Handle(
        GetArchitecturalDiagramLayoutQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DiagramLayoutLoadResult>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!loaded.TryGetValue(out var project))
        {
            return Result<DiagramLayoutLoadResult>.Failure(loaded.Errors);
        }

        var projection = projector.Project(project, request.DiagramType, request.ScopeId, timeProvider.GetUtcNow());
        return projection.TryGetValue(out var diagram)
            ? await diagrams.LoadLayoutAsync(diagram, cancellationToken)
            : Result<DiagramLayoutLoadResult>.Failure(projection.Errors);
    }
}

public sealed record SaveArchitecturalDiagramLayoutCommand(
    Guid ProjectId,
    ArchitecturalDiagramType DiagramType,
    string ScopeId,
    string SourceFingerprint,
    IReadOnlyList<DiagramElementPosition> Positions,
    IReadOnlyList<string> CollapsedBoundaryIds)
    : IRequest<Result<ArchitecturalDiagramLayout>>;

public sealed class SaveArchitecturalDiagramLayoutValidator
    : AbstractValidator<SaveArchitecturalDiagramLayoutCommand>
{
    public SaveArchitecturalDiagramLayoutValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.DiagramType).IsInEnum();
        RuleFor(command => command.ScopeId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.SourceFingerprint)
            .Matches("^sha256:[a-f0-9]{64}$");
        RuleFor(command => command.Positions).NotNull().Must(positions => positions is not null && positions.Count <= 1000);
        RuleFor(command => command.CollapsedBoundaryIds)
            .NotNull().Must(ids => ids is not null && ids.Count <= 1000);
    }
}

public sealed class SaveArchitecturalDiagramLayoutHandler(
    IProjectRepository projects,
    IDiagramRepository diagrams,
    IValidator<SaveArchitecturalDiagramLayoutCommand> validator,
    ArchitecturalDiagramProjector projector,
    TimeProvider timeProvider)
    : IRequestHandler<SaveArchitecturalDiagramLayoutCommand, Result<ArchitecturalDiagramLayout>>
{
    public async Task<Result<ArchitecturalDiagramLayout>> Handle(
        SaveArchitecturalDiagramLayoutCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ArchitecturalDiagramLayout>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!loaded.TryGetValue(out var project))
        {
            return Result<ArchitecturalDiagramLayout>.Failure(loaded.Errors);
        }

        var projection = projector.Project(project, request.DiagramType, request.ScopeId, timeProvider.GetUtcNow());
        if (!projection.TryGetValue(out var diagram))
        {
            return Result<ArchitecturalDiagramLayout>.Failure(projection.Errors);
        }

        if (!string.Equals(diagram.SourceFingerprint, request.SourceFingerprint, StringComparison.Ordinal))
        {
            return Result<ArchitecturalDiagramLayout>.Failure(
                new DomainError("diagram.preview.stale", "The project changed after this preview was generated. Generate a current preview before saving layout."));
        }

        return await diagrams.SaveLayoutAsync(
            diagram,
            new ArchitecturalDiagramLayout(
                "1.0",
                diagram.DiagramId,
                diagram.SourceFingerprint,
                request.Positions,
                request.CollapsedBoundaryIds),
            cancellationToken);
    }
}
