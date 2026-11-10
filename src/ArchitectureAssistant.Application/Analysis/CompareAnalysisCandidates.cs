using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Analysis;

public sealed record CompareAnalysisCandidatesQuery(
    Guid ProjectId,
    Guid AnalysisId,
    string FirstCandidateId,
    string SecondCandidateId)
    : IRequest<Result<AnalysisCandidateComparison>>;

public sealed class CompareAnalysisCandidatesValidator
    : AbstractValidator<CompareAnalysisCandidatesQuery>
{
    public CompareAnalysisCandidatesValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.AnalysisId).NotEmpty();
        RuleFor(query => query.FirstCandidateId).NotEmpty();
        RuleFor(query => query.SecondCandidateId).NotEmpty();
        RuleFor(query => query.SecondCandidateId)
            .NotEqual(query => query.FirstCandidateId)
            .WithMessage("Two different candidates are required for comparison.");
    }
}

public sealed class CompareAnalysisCandidatesHandler(
    IAnalysisRepository analyses,
    AnalysisCandidateComparer comparer,
    IValidator<CompareAnalysisCandidatesQuery> validator)
    : IRequestHandler<CompareAnalysisCandidatesQuery, Result<AnalysisCandidateComparison>>
{
    public async Task<Result<AnalysisCandidateComparison>> Handle(
        CompareAnalysisCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AnalysisCandidateComparison>.Failure(validation.Errors.Select(error =>
                new DomainError("analysis.comparison.request.invalid", error.ErrorMessage)));
        }

        var historyResult = await analyses.ListByProjectAsync(request.ProjectId, cancellationToken);
        if (!historyResult.TryGetValue(out var history))
        {
            return Result<AnalysisCandidateComparison>.Failure(historyResult.Errors);
        }

        var matchingSnapshots = history
            .Where(snapshot => snapshot.AnalysisId == request.AnalysisId)
            .Take(2)
            .ToArray();
        if (matchingSnapshots.Length == 0)
        {
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.analysis_not_found",
                    $"Analysis '{request.AnalysisId}' was not found for project '{request.ProjectId}'."));
        }

        if (matchingSnapshots.Length > 1)
        {
            return Result<AnalysisCandidateComparison>.Failure(
                new DomainError(
                    "analysis.comparison.analysis_not_unique",
                    $"Analysis '{request.AnalysisId}' is not unique for project '{request.ProjectId}'."));
        }

        return comparer.Compare(
            matchingSnapshots[0],
            request.FirstCandidateId,
            request.SecondCandidateId);
    }
}
