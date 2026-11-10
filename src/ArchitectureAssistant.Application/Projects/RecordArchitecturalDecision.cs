using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record RecordArchitecturalDecisionCommand(
    Guid ProjectId,
    string SelectedAlternativeId,
    string SelectedAlternativeKind,
    IReadOnlyList<string> ConsideredAlternativeIds,
    string Justification,
    IReadOnlyList<string> Consequences,
    string RecordedBy,
    Guid? AnalysisId = null,
    Guid? RevisesDecisionId = null)
    : IRequest<Result<ProjectView>>;

public sealed class RecordArchitecturalDecisionValidator
    : AbstractValidator<RecordArchitecturalDecisionCommand>
{
    public RecordArchitecturalDecisionValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.SelectedAlternativeId).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SelectedAlternativeKind)
            .Must(kind => kind is "Architecture" or "Pattern" or "Technology");
        RuleFor(command => command.ConsideredAlternativeIds)
            .NotNull()
            .Must(alternatives => alternatives is not null && alternatives.Count <= 100);
        RuleForEach(command => command.ConsideredAlternativeIds)
            .Must(alternative => !string.IsNullOrWhiteSpace(alternative))
            .MaximumLength(200);
        RuleFor(command => command.Justification).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.Consequences)
            .NotNull()
            .Must(consequences => consequences is not null && consequences.Count <= 100);
        RuleForEach(command => command.Consequences)
            .Must(consequence => !string.IsNullOrWhiteSpace(consequence))
            .MaximumLength(2000);
        RuleFor(command => command.RecordedBy).NotEmpty().MaximumLength(120);
        RuleFor(command => command.AnalysisId).NotEqual(Guid.Empty).When(command => command.AnalysisId.HasValue);
        RuleFor(command => command.RevisesDecisionId)
            .NotEqual(Guid.Empty)
            .When(command => command.RevisesDecisionId.HasValue);
    }
}

public sealed class RecordArchitecturalDecisionHandler(
    IProjectRepository projects,
    IAnalysisRepository analyses,
    IKnowledgeCatalog catalog,
    IValidator<RecordArchitecturalDecisionCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<RecordArchitecturalDecisionCommand, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        RecordArchitecturalDecisionCommand request,
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

        var candidatesResult = await GetCandidatesAsync(request, cancellationToken);
        if (!candidatesResult.TryGetValue(out var candidates))
        {
            return Result<ProjectView>.Failure(candidatesResult.Errors);
        }

        var selectedId = request.SelectedAlternativeId.Trim();
        var selectedMatches = candidates.Where(candidate => candidate.Id == selectedId).Take(2).ToArray();
        var consideredIds = request.ConsideredAlternativeIds
            .Select(alternativeId => alternativeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selectedMatches.Length != 1 ||
            consideredIds.Length != request.ConsideredAlternativeIds.Count ||
            consideredIds.Any(string.IsNullOrWhiteSpace) ||
            consideredIds.Any(alternativeId => alternativeId.Length > 200))
        {
            return Result<ProjectView>.Failure(
                new DomainError("decision.alternatives.invalid", "Decision alternatives must be valid, unique candidate IDs."));
        }

        var selected = selectedMatches[0];
        if (
            selected.Kind != request.SelectedAlternativeKind ||
            consideredIds.Contains(selectedId, StringComparer.Ordinal))
        {
            return Result<ProjectView>.Failure(
                new DomainError("decision.alternatives.invalid", "The selected and considered alternatives must be distinct candidates of the same kind."));
        }

        var consideredCandidates = new List<DecisionCandidate>();
        foreach (var alternativeId in consideredIds)
        {
            var matches = candidates.Where(candidate => candidate.Id == alternativeId).Take(2).ToArray();
            if (matches.Length != 1 || matches[0].Kind != selected.Kind)
            {
                return Result<ProjectView>.Failure(
                    new DomainError("decision.alternatives.invalid", $"Alternative '{alternativeId}' is missing or is not comparable with the selected candidate."));
            }

            consideredCandidates.Add(matches[0]);
        }

        var now = timeProvider.GetUtcNow();
        var decisionResult = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            selected.Id,
            selected.Kind,
            consideredCandidates.Select(candidate => candidate.Id),
            DecisionStatus.Proposed,
            request.Justification,
            request.Consequences,
            request.RecordedBy,
            now,
            request.AnalysisId,
            selected.Version,
            request.RevisesDecisionId);
        if (!decisionResult.TryGetValue(out var decision))
        {
            return Result<ProjectView>.Failure(decisionResult.Errors);
        }

        var updatedResult = request.RevisesDecisionId is Guid previousDecisionId
            ? project.ReviseDecision(previousDecisionId, decision, request.RecordedBy, now)
            : project.AddDecision(decision, now);
        if (!updatedResult.TryGetValue(out var revisedProject))
        {
            return Result<ProjectView>.Failure(updatedResult.Errors);
        }

        var savedResult = await projects.SaveAsync(revisedProject, project.Revision, cancellationToken);
        return savedResult.TryGetValue(out var savedProject)
            ? Result<ProjectView>.Success(ProjectView.From(savedProject))
            : Result<ProjectView>.Failure(savedResult.Errors);
    }

    private async Task<Result<IReadOnlyList<DecisionCandidate>>> GetCandidatesAsync(
        RecordArchitecturalDecisionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AnalysisId is Guid analysisId)
        {
            var historyResult = await analyses.ListByProjectAsync(request.ProjectId, cancellationToken);
            if (!historyResult.TryGetValue(out var history))
            {
                return Result<IReadOnlyList<DecisionCandidate>>.Failure(historyResult.Errors);
            }

            var snapshots = history.Where(snapshot => snapshot.AnalysisId == analysisId).Take(2).ToArray();
            if (snapshots.Length != 1 || snapshots[0].ProjectId != request.ProjectId)
            {
                return Result<IReadOnlyList<DecisionCandidate>>.Failure(
                    new DomainError("decision.analysis.invalid", "The referenced analysis must exist uniquely in this project."));
            }

            return Result<IReadOnlyList<DecisionCandidate>>.Success(
                snapshots[0].CandidateGroups
                    .SelectMany(group => group.Candidates)
                    .Select(candidate => new DecisionCandidate(
                        candidate.CandidateId,
                        candidate.CandidateKind,
                        candidate.CandidateVersion))
                    .ToArray());
        }

        var catalogResult = await catalog.GetSnapshotAsync(cancellationToken);
        if (!catalogResult.TryGetValue(out var knowledge))
        {
            return Result<IReadOnlyList<DecisionCandidate>>.Failure(catalogResult.Errors);
        }

        return Result<IReadOnlyList<DecisionCandidate>>.Success(
            knowledge.Items
                .Where(item => item.Kind is "Architecture" or "Pattern" or "Technology")
                .Select(item => new DecisionCandidate(item.Id, item.Kind, item.Version))
                .ToArray());
    }

    private sealed record DecisionCandidate(string Id, string Kind, string Version);
}
