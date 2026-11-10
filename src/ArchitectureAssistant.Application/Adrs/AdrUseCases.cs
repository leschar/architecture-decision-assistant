using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Adrs;
using ArchitectureAssistant.Domain.Common;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Adrs;

public sealed record CreateAdrDraftCommand(Guid ProjectId, Guid DecisionId)
    : IRequest<Result<AdrSnapshot>>;

public sealed class CreateAdrDraftValidator : AbstractValidator<CreateAdrDraftCommand>
{
    public CreateAdrDraftValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.DecisionId).NotEmpty();
    }
}

public sealed class CreateAdrDraftHandler(
    IProjectRepository projects,
    IAdrRepository adrs,
    IValidator<CreateAdrDraftCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateAdrDraftCommand, Result<AdrSnapshot>>
{
    public async Task<Result<AdrSnapshot>> Handle(
        CreateAdrDraftCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdrSnapshot>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!loaded.TryGetValue(out var project))
        {
            return Result<AdrSnapshot>.Failure(loaded.Errors);
        }

        var matches = project.Decisions.Where(decision => decision.Id == request.DecisionId).Take(2).ToArray();
        if (matches.Length != 1)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.decision.not_found", "The ADR source decision was not found uniquely in this project."));
        }

        var existingAdrs = await adrs.ListAsync(request.ProjectId, cancellationToken);
        if (!existingAdrs.TryGetValue(out var projectAdrs))
        {
            return Result<AdrSnapshot>.Failure(existingAdrs.Errors);
        }

        if (projectAdrs.Any(adr => adr.DecisionId == request.DecisionId))
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.decision.already_has_adr", "This decision already has an ADR. Revise that ADR instead of creating a duplicate."));
        }

        var composition = AdrSnapshot.Compose(project, matches[0], Guid.NewGuid().ToString("N"), timeProvider.GetUtcNow());
        if (!composition.TryGetValue(out var draft))
        {
            return Result<AdrSnapshot>.Failure(composition.Errors);
        }

        return await adrs.SaveAsync(draft, null, cancellationToken);
    }
}

public sealed record ListAdrsQuery(Guid ProjectId) : IRequest<Result<IReadOnlyList<AdrSnapshot>>>;

public sealed class ListAdrsValidator : AbstractValidator<ListAdrsQuery>
{
    public ListAdrsValidator() => RuleFor(query => query.ProjectId).NotEmpty();
}

public sealed class ListAdrsHandler(
    IAdrRepository adrs,
    IValidator<ListAdrsQuery> validator)
    : IRequestHandler<ListAdrsQuery, Result<IReadOnlyList<AdrSnapshot>>>
{
    public async Task<Result<IReadOnlyList<AdrSnapshot>>> Handle(
        ListAdrsQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid
            ? await adrs.ListAsync(request.ProjectId, cancellationToken)
            : Result<IReadOnlyList<AdrSnapshot>>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
    }
}

public sealed record ReviseAdrSectionCommand(
    Guid ProjectId,
    string AdrId,
    long ExpectedRevision,
    AdrSectionKind Section,
    string Content,
    string Author)
    : IRequest<Result<AdrSnapshot>>;

public sealed class ReviseAdrSectionValidator : AbstractValidator<ReviseAdrSectionCommand>
{
    public ReviseAdrSectionValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.AdrId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ExpectedRevision).GreaterThan(0);
        RuleFor(command => command.Section).IsInEnum();
        RuleFor(command => command.Content).NotNull().MaximumLength(10000);
        RuleFor(command => command.Author).NotEmpty().MaximumLength(120);
    }
}

public sealed class ReviseAdrSectionHandler(
    IAdrRepository adrs,
    IValidator<ReviseAdrSectionCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<ReviseAdrSectionCommand, Result<AdrSnapshot>>
{
    public async Task<Result<AdrSnapshot>> Handle(
        ReviseAdrSectionCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdrSnapshot>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await adrs.LoadLatestAsync(request.ProjectId, request.AdrId, cancellationToken);
        if (!loaded.TryGetValue(out var current))
        {
            return Result<AdrSnapshot>.Failure(loaded.Errors);
        }

        if (current.Revision != request.ExpectedRevision)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.conflict.revision", "The ADR changed after it was opened. Reload it before editing."));
        }

        var revised = current.ReviseSection(request.Section, request.Content, request.Author, timeProvider.GetUtcNow());
        return revised.TryGetValue(out var next)
            ? await adrs.SaveAsync(next, current.Revision, cancellationToken)
            : Result<AdrSnapshot>.Failure(revised.Errors);
    }
}

public sealed record AcceptAdrCommand(
    Guid ProjectId,
    string AdrId,
    long ExpectedRevision,
    string Reviewer,
    string? Note)
    : IRequest<Result<AdrSnapshot>>;

public sealed class AcceptAdrValidator : AbstractValidator<AcceptAdrCommand>
{
    public AcceptAdrValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.AdrId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ExpectedRevision).GreaterThan(0);
        RuleFor(command => command.Reviewer).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Note).MaximumLength(2000);
    }
}

public sealed class AcceptAdrHandler(
    IAdrRepository adrs,
    IValidator<AcceptAdrCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<AcceptAdrCommand, Result<AdrSnapshot>>
{
    public async Task<Result<AdrSnapshot>> Handle(
        AcceptAdrCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdrSnapshot>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await adrs.LoadLatestAsync(request.ProjectId, request.AdrId, cancellationToken);
        if (!loaded.TryGetValue(out var current))
        {
            return Result<AdrSnapshot>.Failure(loaded.Errors);
        }

        if (current.Revision != request.ExpectedRevision)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.conflict.revision", "The ADR changed after it was opened. Reload it before accepting."));
        }

        var accepted = current.Accept(request.Reviewer, timeProvider.GetUtcNow(), request.Note);
        return accepted.TryGetValue(out var next)
            ? await adrs.SaveAsync(next, current.Revision, cancellationToken)
            : Result<AdrSnapshot>.Failure(accepted.Errors);
    }
}

public sealed record SupersedeAdrCommand(
    Guid ProjectId,
    string AdrId,
    long ExpectedRevision,
    string ReplacementAdrId)
    : IRequest<Result<AdrSnapshot>>;

public sealed class SupersedeAdrValidator : AbstractValidator<SupersedeAdrCommand>
{
    public SupersedeAdrValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.AdrId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ExpectedRevision).GreaterThan(0);
        RuleFor(command => command.ReplacementAdrId)
            .NotEmpty()
            .MaximumLength(120)
            .Must((command, replacementAdrId) =>
                !string.Equals(command.AdrId, replacementAdrId, StringComparison.Ordinal))
            .WithMessage("An ADR cannot supersede itself.");
    }
}

public sealed class SupersedeAdrHandler(
    IAdrRepository adrs,
    IProjectRepository projects,
    IValidator<SupersedeAdrCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<SupersedeAdrCommand, Result<AdrSnapshot>>
{
    public async Task<Result<AdrSnapshot>> Handle(
        SupersedeAdrCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdrSnapshot>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var currentResult = await adrs.LoadLatestAsync(request.ProjectId, request.AdrId, cancellationToken);
        if (!currentResult.TryGetValue(out var current))
        {
            return Result<AdrSnapshot>.Failure(currentResult.Errors);
        }

        if (current.Revision != request.ExpectedRevision)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.conflict.revision", "The ADR changed after it was opened. Reload it before superseding."));
        }

        var projectResult = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!projectResult.TryGetValue(out var project))
        {
            return Result<AdrSnapshot>.Failure(projectResult.Errors);
        }

        var replacementResult = await adrs.LoadLatestAsync(
            request.ProjectId,
            request.ReplacementAdrId,
            cancellationToken);
        if (!replacementResult.TryGetValue(out var replacement))
        {
            return Result<AdrSnapshot>.Failure(replacementResult.Errors);
        }

        if (replacement.Status == AdrStatus.Superseded)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.supersession.target_invalid", "The replacement ADR is already superseded."));
        }

        var sourceDecision = project.Decisions.SingleOrDefault(decision => decision.Id == current.DecisionId);
        var replacementDecision = project.Decisions.SingleOrDefault(decision => decision.Id == replacement.DecisionId);
        if (sourceDecision is null ||
            replacementDecision is null ||
            sourceDecision.SupersededByDecisionId != replacementDecision.Id ||
            replacementDecision.SupersedesDecisionId != sourceDecision.Id)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError(
                    "adr.supersession.decision_mismatch",
                    "The replacement ADR must document the decision that explicitly superseded the original decision."));
        }

        var superseded = current.SupersedeBy(replacement.AdrId, timeProvider.GetUtcNow());
        return superseded.TryGetValue(out var next)
            ? await adrs.SaveAsync(next, current.Revision, cancellationToken)
            : Result<AdrSnapshot>.Failure(superseded.Errors);
    }
}

public sealed record ExportAdrMarkdownQuery(Guid ProjectId, string AdrId)
    : IRequest<Result<string>>;

public sealed class ExportAdrMarkdownValidator : AbstractValidator<ExportAdrMarkdownQuery>
{
    public ExportAdrMarkdownValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.AdrId).NotEmpty().MaximumLength(120);
    }
}

public sealed class ExportAdrMarkdownHandler(
    IAdrRepository adrs,
    IValidator<ExportAdrMarkdownQuery> validator)
    : IRequestHandler<ExportAdrMarkdownQuery, Result<string>>
{
    public async Task<Result<string>> Handle(
        ExportAdrMarkdownQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<string>.Failure(validation.Errors.Select(error =>
                new DomainError(error.ErrorCode, error.ErrorMessage)));
        }

        var loaded = await adrs.LoadLatestAsync(request.ProjectId, request.AdrId, cancellationToken);
        return loaded.TryGetValue(out var snapshot)
            ? Result<string>.Success(AdrMarkdownExporter.Export(snapshot))
            : Result<string>.Failure(loaded.Errors);
    }
}

public static class AdrMarkdownExporter
{
    public static string Export(AdrSnapshot adr)
    {
        ArgumentNullException.ThrowIfNull(adr);
        var builder = new System.Text.StringBuilder()
            .Append("# ").AppendLine(adr.Title)
            .AppendLine()
            .Append("**Status:** ").AppendLine(adr.Status.ToString())
            .Append("**ADR:** ").Append(adr.AdrId).Append(" · revision ").AppendLine(adr.Revision.ToString())
            .Append("**Project:** ").Append(adr.ProjectId).Append(" · revision ").AppendLine(adr.SourceSnapshot.ProjectRevision)
            .Append("**Decision:** ").Append(adr.DecisionId).Append(" · revision ").AppendLine(adr.SourceSnapshot.DecisionRevision);

        AppendTextSection(builder, "Contexto", adr.Sections.Context);
        AppendTextSection(builder, "Problema", adr.Sections.Problem);
        builder.AppendLine().AppendLine("## Alternativas");
        if (adr.Sections.Alternatives.Items.Count == 0)
        {
            builder.AppendLine("_NotProvided — nenhuma alternativa foi registrada._");
        }
        else
        {
            foreach (var alternative in adr.Sections.Alternatives.Items)
            {
                builder.Append("- ").Append(alternative.Name).Append(" (`")
                    .Append(alternative.AlternativeId).AppendLine("`)");
                AppendSources(builder, alternative.SourceReferences);
            }
        }

        AppendTextSection(builder, "Decisão", adr.Sections.Decision);
        AppendTextSection(builder, "Consequências", adr.Sections.Consequences);
        AppendTextSection(builder, "Riscos", adr.Sections.Risks);
        AppendTextSection(builder, "Benefícios", adr.Sections.Benefits);
        AppendTextSection(builder, "Justificativa", adr.Sections.Justification);
        AppendTextSection(builder, "Trade-offs", adr.Sections.TradeOffs);
        return builder.ToString();
    }

    private static void AppendTextSection(
        System.Text.StringBuilder builder,
        string title,
        AdrTextSection section)
    {
        builder.AppendLine().Append("## ").AppendLine(title);
        builder.AppendLine(section.Completeness == AdrCompleteness.NotProvided
            ? "_NotProvided — conteúdo não informado._"
            : section.Content);
        AppendSources(builder, section.SourceReferences);
    }

    private static void AppendSources(
        System.Text.StringBuilder builder,
        IReadOnlyList<AdrSourceReference> sources)
    {
        if (sources.Count > 0)
        {
            builder.Append("_Fontes: ");
            builder.AppendJoin(", ", sources.Select(source =>
                $"{source.SourceType}:{source.SourceId}@{source.SourceVersion ?? "sem-versão"}"));
            builder.AppendLine("_");
        }
    }
}
