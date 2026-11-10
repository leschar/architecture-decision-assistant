using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string workspaceRoot)
    {
        services.AddSingleton<IProjectRepository>(provider =>
            new JsonProjectRepository(
                workspaceRoot,
                provider.GetRequiredService<ILogger<JsonProjectRepository>>()));
        services.AddSingleton<IAnalysisRepository>(provider =>
            new JsonAnalysisRepository(
                workspaceRoot,
                provider.GetRequiredService<ILogger<JsonAnalysisRepository>>()));
        services.AddSingleton<IDiagramRepository>(provider =>
            new JsonDiagramRepository(
                workspaceRoot,
                provider.GetRequiredService<ILogger<JsonDiagramRepository>>()));
        services.AddSingleton<IAdrRepository>(provider =>
            new JsonAdrRepository(
                workspaceRoot,
                provider.GetRequiredService<ILogger<JsonAdrRepository>>()));
        services.AddSingleton<IHistoryRepository>(provider =>
            new JsonHistoryRepository(
                workspaceRoot,
                provider.GetRequiredService<ILogger<JsonHistoryRepository>>()));
        services.AddSingleton<IKnowledgeCatalog>(provider =>
            new JsonKnowledgeCatalog(
                workspaceRoot,
                provider.GetRequiredService<ILogger<JsonKnowledgeCatalog>>()));
        return services;
    }
}
