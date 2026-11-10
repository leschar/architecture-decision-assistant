using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record AddRequirementCommand(
    Guid ProjectId,
    string Description,
    RequirementClassification Classification,
    RequirementPriority? Priority)
    : IRequest<Result<ProjectView>>;

public sealed class AddRequirementValidator : AbstractValidator<AddRequirementCommand>
{
    public AddRequirementValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(2000);
        RuleFor(command => command.Classification).IsInEnum();
        RuleFor(command => command.Priority).IsInEnum();
    }
}

public sealed class AddRequirementHandler(
    IProjectRepository repository,
    IValidator<AddRequirementCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<AddRequirementCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        AddRequirementCommand request,
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

        var now = timeProvider.GetUtcNow();
        var createdRequirement = Requirement.Create(
            Guid.NewGuid(),
            request.Description,
            request.Classification,
            request.Priority,
            RequirementStatus.Confirmed,
            now);
        if (!createdRequirement.TryGetValue(out var requirement))
        {
            return Result<ProjectView>.Failure(createdRequirement.Errors);
        }

        var updated = project.AddRequirement(requirement, now);
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
