using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using FluentValidation;
using MediatR;

namespace ArchitectureAssistant.Application.Analysis;

public sealed record AnalyzeProjectCommand(Guid ProjectId) : IRequest<Result<AnalysisSnapshot>>;

public sealed class AnalyzeProjectValidator : AbstractValidator<AnalyzeProjectCommand>
{
    public AnalyzeProjectValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
    }
}

public sealed class AnalyzeProjectHandler(
    IProjectRepository projects,
    IKnowledgeCatalog catalog,
    IAnalysisRepository analyses,
    BasicAnalysisEngine engine,
    KnowledgeGraphProjector graphProjector,
    IValidator<AnalyzeProjectCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<AnalyzeProjectCommand, Result<AnalysisSnapshot>>
{
    public async Task<Result<AnalysisSnapshot>> Handle(
        AnalyzeProjectCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AnalysisSnapshot>.Failure(validation.Errors.Select(error =>
                new DomainError("analysis.request.invalid", error.ErrorMessage)));
        }

        var projectResult = await projects.LoadAsync(request.ProjectId, cancellationToken);
        if (!projectResult.TryGetValue(out var project))
        {
            return Result<AnalysisSnapshot>.Failure(projectResult.Errors);
        }

        var catalogResult = await catalog.GetSnapshotAsync(cancellationToken);
        if (!catalogResult.TryGetValue(out var catalogSnapshot))
        {
            return Result<AnalysisSnapshot>.Failure(catalogResult.Errors);
        }

        var candidates = catalogSnapshot.Items
            .Where(item => item.Kind is "Architecture" or "Pattern" or "Technology")
            .Select(item => new BasicAnalysisCandidate(
                item.Id,
                item.Kind,
                item.Name,
                item.Version,
                item.Description,
                item.Source,
                item.Tags,
                item.Category,
                item.Complexity,
                item.Scalability,
                item.OperationalCost,
                item.Benefits,
                item.Limitations))
            .ToArray();
        var rules = catalogSnapshot.Rules
            .Select(rule => new BasicAnalysisRule(
                rule.Id,
                rule.Version,
                rule.Name,
                rule.Source,
                rule.KnowledgeSource,
                rule.ContentHash,
                rule.Enabled,
                rule.Weight,
                rule.Kinds,
                rule.CandidateIds,
                rule.CandidateTags,
                rule.ApplicabilityCondition,
                rule.Conditions,
                rule.Effects.Select(effect => new BasicAnalysisEffect(
                    effect.Type,
                    effect.HardConstraint,
                    effect.Points,
                    effect.Severity,
                    effect.Rationale)).ToArray()))
            .ToArray();

        var graphResult = graphProjector.Project(
            catalogSnapshot.ContentHash,
            KnowledgeGraphCatalogMapper.Map(catalogSnapshot.Items));
        if (!graphResult.TryGetValue(out var graphProjection))
        {
            return Result<AnalysisSnapshot>.Failure(graphResult.Errors);
        }

        var analysisResult = engine.Analyze(
            Guid.NewGuid(),
            project,
            catalogSnapshot.ContentHash,
            AnalysisVersioning.ComputeRuleSetHash(catalogSnapshot),
            candidates,
            rules,
            timeProvider.GetUtcNow(),
            graphProjection);
        if (!analysisResult.TryGetValue(out var snapshot))
        {
            return Result<AnalysisSnapshot>.Failure(analysisResult.Errors);
        }

        return await analyses.SaveAsync(snapshot, cancellationToken);
    }
}
