using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Diagrams;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Diagrams;

public sealed record GetArchitecturalDiagramQuery(
    Guid ProjectId,
    ArchitecturalDiagramType DiagramType,
    string ScopeId)
    : IRequest<Result<ArchitecturalDiagram>>;

public sealed class GetArchitecturalDiagramValidator : AbstractValidator<GetArchitecturalDiagramQuery>
{
    public GetArchitecturalDiagramValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.DiagramType).IsInEnum();
        RuleFor(query => query.ScopeId).NotEmpty().MaximumLength(120);
    }
}

public sealed class GetArchitecturalDiagramHandler(
    IProjectRepository repository,
    IValidator<GetArchitecturalDiagramQuery> validator,
    ArchitecturalDiagramProjector projector,
    TimeProvider timeProvider)
    : IRequestHandler<GetArchitecturalDiagramQuery, Result<ArchitecturalDiagram>>
{
    public async Task<Result<ArchitecturalDiagram>> Handle(
        GetArchitecturalDiagramQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ArchitecturalDiagram>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await repository.LoadAsync(request.ProjectId, cancellationToken);
        return loaded.TryGetValue(out var project)
            ? projector.Project(project, request.DiagramType, request.ScopeId, timeProvider.GetUtcNow())
            : Result<ArchitecturalDiagram>.Failure(loaded.Errors);
    }
}
