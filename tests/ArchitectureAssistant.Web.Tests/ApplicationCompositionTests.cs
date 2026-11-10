using ArchitectureAssistant.Application;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Analysis;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Application.Projects;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.KnowledgeGraph;
using ArchitectureAssistant.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureAssistant.Web.Tests;

public sealed class ApplicationCompositionTests
{
    [Fact]
    public void ApplicationRegistration_ResolvesProjectHandlersAndValidators()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ISender>());
        Assert.NotNull(provider.GetRequiredService<IRequestHandler<GetKnowledgeCatalogQuery, Result<KnowledgeCatalogSnapshot>>>());
        Assert.NotNull(provider.GetRequiredService<IKnowledgeCatalog>());
        Assert.NotNull(provider.GetRequiredService<IRequestHandler<CreateProjectCommand, Result<ProjectView>>>());
        Assert.NotNull(provider.GetRequiredService<IRequestHandler<UpdateProjectContextCommand, Result<ProjectView>>>());
        Assert.NotNull(provider.GetRequiredService<IAnalysisRepository>());
        Assert.NotNull(provider.GetRequiredService<IRequestHandler<AnalyzeProjectCommand, Result<ArchitectureAssistant.Domain.Analysis.AnalysisSnapshot>>>());
        Assert.NotNull(provider.GetRequiredService<IRequestHandler<GetProjectAnalysesQuery, Result<IReadOnlyList<AnalysisHistoryItem>>>>());
        Assert.NotNull(provider.GetRequiredService<IRequestHandler<CompareAnalysisCandidatesQuery, Result<AnalysisCandidateComparison>>>());
    }
}
