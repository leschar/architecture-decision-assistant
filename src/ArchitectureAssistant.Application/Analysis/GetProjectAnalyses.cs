using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Analysis;

public sealed record GetProjectAnalysesQuery(Guid ProjectId)
    : IRequest<Result<IReadOnlyList<AnalysisHistoryItem>>>;

public sealed class GetProjectAnalysesValidator : AbstractValidator<GetProjectAnalysesQuery>
{
    public GetProjectAnalysesValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
    }
}

public sealed class GetProjectAnalysesHandler(
    IProjectRepository projects,
    IKnowledgeCatalog catalog,
    IAnalysisRepository analyses,
    IValidator<GetProjectAnalysesQuery> validator)
    : IRequestHandler<GetProjectAnalysesQuery, Result<IReadOnlyList<AnalysisHistoryItem>>>
{
    public async Task<Result<IReadOnlyList<AnalysisHistoryItem>>> Handle(
        GetProjectAnalysesQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<IReadOnlyList<AnalysisHistoryItem>>.Failure(validation.Errors.Select(error =>
                new DomainError("analysis.request.invalid", error.ErrorMessage)));
        }

        var projectResult = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!projectResult.TryGetValue(out var project))
        {
            return Result<IReadOnlyList<AnalysisHistoryItem>>.Failure(projectResult.Errors);
        }

        var catalogResult = await catalog.GetSnapshotAsync(cancellationToken);
        if (!catalogResult.TryGetValue(out var catalogSnapshot))
        {
            return Result<IReadOnlyList<AnalysisHistoryItem>>.Failure(catalogResult.Errors);
        }

        var historyResult = await analyses.ListByProjectAsync(request.ProjectId, cancellationToken);
        if (!historyResult.TryGetValue(out var history))
        {
            return Result<IReadOnlyList<AnalysisHistoryItem>>.Failure(historyResult.Errors);
        }

        var projectHash = BasicAnalysisEngine.ComputeProjectHash(project);
        var ruleSetHash = AnalysisVersioning.ComputeRuleSetHash(catalogSnapshot);
        var items = history
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ThenBy(snapshot => snapshot.AnalysisId)
            .Select(snapshot =>
            {
                var reasons = new List<AnalysisStalenessReason>();
                if (snapshot.ProjectRevision != project.Revision)
                {
                    reasons.Add(AnalysisStalenessReason.ProjectRevisionChanged);
                }

                if (!string.Equals(snapshot.ProjectHash, projectHash, StringComparison.Ordinal))
                {
                    reasons.Add(AnalysisStalenessReason.ProjectContentChanged);
                }

                if (!string.Equals(snapshot.CatalogHash, catalogSnapshot.ContentHash, StringComparison.Ordinal))
                {
                    reasons.Add(AnalysisStalenessReason.CatalogChanged);
                }

                if (!string.Equals(snapshot.RuleSetHash, ruleSetHash, StringComparison.Ordinal))
                {
                    reasons.Add(AnalysisStalenessReason.RuleSetChanged);
                }

                if (!string.Equals(
                        snapshot.EvaluatorVersion,
                        BasicAnalysisEngine.CurrentVersion,
                        StringComparison.Ordinal))
                {
                    reasons.Add(AnalysisStalenessReason.EvaluatorVersionChanged);
                }

                return new AnalysisHistoryItem(snapshot, reasons.AsReadOnly());
            })
            .ToArray();

        return Result<IReadOnlyList<AnalysisHistoryItem>>.Success(items);
    }
}
