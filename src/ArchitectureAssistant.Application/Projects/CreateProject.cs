using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record CreateProjectCommand(string Name, string Purpose)
    : IRequest<Result<ProjectView>>;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(120);
        RuleFor(command => command.Purpose)
            .NotEmpty()
            .MaximumLength(2000);
    }
}

public sealed class CreateProjectHandler(
    IProjectRepository repository,
    IValidator<CreateProjectCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ProjectView>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var created = Project.Create(Guid.NewGuid(), request.Name, request.Purpose, timeProvider.GetUtcNow());
        if (!created.TryGetValue(out var project))
        {
            return Result<ProjectView>.Failure(created.Errors);
        }

        var saved = await repository.SaveAsync(project, null, cancellationToken);
        return saved.TryGetValue(out var savedProject)
            ? Result<ProjectView>.Success(ProjectView.From(savedProject))
            : Result<ProjectView>.Failure(saved.Errors);
    }
}
