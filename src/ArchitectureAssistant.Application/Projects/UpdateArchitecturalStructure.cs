using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record UpdateArchitecturalStructureCommand(
    Guid ProjectId,
    IReadOnlyList<StructuralElement> Elements,
    IReadOnlyList<StructuralConnection> Connections)
    : IRequest<Result<ProjectView>>;

public sealed class UpdateArchitecturalStructureValidator : AbstractValidator<UpdateArchitecturalStructureCommand>
{
    public UpdateArchitecturalStructureValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Elements).NotNull();
        RuleFor(command => command.Connections).NotNull();
        RuleForEach(command => command.Elements).SetValidator(new StructuralElementValidator());
        RuleForEach(command => command.Connections).SetValidator(new StructuralConnectionValidator());
    }

    private sealed class StructuralElementValidator : AbstractValidator<StructuralElement>
    {
        public StructuralElementValidator()
        {
            RuleFor(element => element.Id).NotEmpty().MaximumLength(120);
            RuleFor(element => element.Name).NotEmpty().MaximumLength(120);
            RuleFor(element => element.Type).IsInEnum();
            RuleFor(element => element.ParentId).MaximumLength(120);
            RuleFor(element => element.Description).MaximumLength(2000);
            RuleFor(element => element.Responsibility).MaximumLength(1000);
            RuleFor(element => element.Technology).MaximumLength(120);
            RuleFor(element => element.Status).IsInEnum();
            RuleFor(element => element.Provenance).NotNull().SetValidator(new StructuralProvenanceValidator());
        }
    }

    private sealed class StructuralConnectionValidator : AbstractValidator<StructuralConnection>
    {
        public StructuralConnectionValidator()
        {
            RuleFor(connection => connection.Id).NotEmpty().MaximumLength(120);
            RuleFor(connection => connection.SourceId).NotEmpty().MaximumLength(120);
            RuleFor(connection => connection.TargetId).NotEmpty().MaximumLength(120);
            RuleFor(connection => connection.Description).NotEmpty().MaximumLength(2000);
            RuleFor(connection => connection.InteractionType).IsInEnum();
            RuleFor(connection => connection.Protocol).MaximumLength(120);
            RuleFor(connection => connection.MessageName).MaximumLength(200);
            RuleFor(connection => connection.Status).IsInEnum();
            RuleFor(connection => connection.Provenance).NotNull().SetValidator(new StructuralProvenanceValidator());
            RuleFor(connection => connection.MessageName)
                .NotEmpty()
                .When(connection => connection.InteractionType == StructuralInteractionType.AsynchronousEvent);
        }
    }

    private sealed class StructuralProvenanceValidator : AbstractValidator<StructuralProvenance>
    {
        public StructuralProvenanceValidator()
        {
            RuleFor(provenance => provenance.SourceKind).IsInEnum();
            RuleFor(provenance => provenance.SourceId).MaximumLength(120);
            RuleFor(provenance => provenance.SourceVersion).MaximumLength(100);
            RuleFor(provenance => provenance)
                .Must(provenance => provenance.SourceKind == StructuralSourceKind.Manual
                    ? provenance.SourceId is null && provenance.SourceVersion is null
                    : !string.IsNullOrWhiteSpace(provenance.SourceId))
                .WithMessage("Non-manual structural provenance requires a source ID.");
        }
    }
}

public sealed class UpdateArchitecturalStructureHandler(
    IProjectRepository repository,
    IValidator<UpdateArchitecturalStructureCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateArchitecturalStructureCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        UpdateArchitecturalStructureCommand request,
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

        var structure = ArchitecturalStructure.Create(request.Elements, request.Connections);
        if (!structure.TryGetValue(out var validatedStructure))
        {
            return Result<ProjectView>.Failure(structure.Errors);
        }

        var updated = project.UpdateStructure(validatedStructure, timeProvider.GetUtcNow());
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
