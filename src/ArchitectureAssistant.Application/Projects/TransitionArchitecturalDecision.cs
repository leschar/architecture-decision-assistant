using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record TransitionArchitecturalDecisionCommand(
    Guid ProjectId,
    Guid DecisionId,
    DecisionStatus Status,
    string ChangedBy)
    : IRequest<Result<ProjectView>>;

public sealed class TransitionArchitecturalDecisionValidator
    : AbstractValidator<TransitionArchitecturalDecisionCommand>
{
    public TransitionArchitecturalDecisionValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.DecisionId).NotEmpty();
        RuleFor(command => command.Status)
            .Must(status => status is DecisionStatus.Accepted or DecisionStatus.Rejected);
        RuleFor(command => command.ChangedBy).NotEmpty().MaximumLength(120);
    }
}

public sealed class TransitionArchitecturalDecisionHandler(
    IProjectRepository projects,
    IValidator<TransitionArchitecturalDecisionCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<TransitionArchitecturalDecisionCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        TransitionArchitecturalDecisionCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ProjectView>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var projectResult = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!projectResult.TryGetValue(out var project))
        {
            return Result<ProjectView>.Failure(projectResult.Errors);
        }

        var updatedResult = project.TransitionDecision(
            request.DecisionId,
            request.Status,
            request.ChangedBy,
            timeProvider.GetUtcNow());
        if (!updatedResult.TryGetValue(out var revisedProject))
        {
            return Result<ProjectView>.Failure(updatedResult.Errors);
        }

        var savedResult = await projects.SaveAsync(revisedProject, project.Revision, cancellationToken);
        return savedResult.TryGetValue(out var savedProject)
            ? Result<ProjectView>.Success(ProjectView.From(savedProject))
            : Result<ProjectView>.Failure(savedResult.Errors);
    }
}
