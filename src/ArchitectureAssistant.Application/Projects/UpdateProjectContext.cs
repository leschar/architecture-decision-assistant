using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record UpdateProjectContextCommand(
    Guid ProjectId,
    string? BusinessDomain,
    int? TeamSize,
    ProjectCriticality? Criticality,
    string Constraints,
    ScalabilityTarget? ScalabilityTarget = null)
    : IRequest<Result<ProjectView>>;

public sealed class UpdateProjectContextValidator : AbstractValidator<UpdateProjectContextCommand>
{
    public UpdateProjectContextValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BusinessDomain).MaximumLength(120);
        RuleFor(command => command.TeamSize).GreaterThan(0).When(command => command.TeamSize.HasValue);
        RuleFor(command => command.Criticality).IsInEnum().When(command => command.Criticality.HasValue);
        RuleFor(command => command.ScalabilityTarget).IsInEnum().When(command => command.ScalabilityTarget.HasValue);
        RuleFor(command => command.Constraints).MaximumLength(5000);
    }
}

public sealed class UpdateProjectContextHandler(
    IProjectRepository repository,
    IValidator<UpdateProjectContextCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectContextCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        UpdateProjectContextCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ProjectView>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await repository.LoadAsync(request.ProjectId, cancellationToken);
        if (!loaded.TryGetValue(out var project))
        {
            return Result<ProjectView>.Failure(loaded.Errors);
        }

        var context = project.Context.Update(
            request.BusinessDomain,
            request.TeamSize,
            request.Criticality,
            request.Constraints.Split(
                ['\r', '\n'],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            request.ScalabilityTarget);
        if (!context.TryGetValue(out var updatedContext))
        {
            return Result<ProjectView>.Failure(context.Errors);
        }

        var updated = project.UpdateContext(updatedContext, timeProvider.GetUtcNow());
        if (!updated.TryGetValue(out var revisedProject))
        {
            return Result<ProjectView>.Failure(updated.Errors);
        }

        var saved = await repository.SaveAsync(revisedProject, project.Revision, cancellationToken);
        return saved.TryGetValue(out var savedProject)
            ? Result<ProjectView>.Success(ProjectView.From(savedProject))
            : Result<ProjectView>.Failure(saved.Errors);
    }
}
