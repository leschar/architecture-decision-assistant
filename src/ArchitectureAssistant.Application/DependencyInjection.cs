using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Diagrams;
using ArchitectureAssistant.Domain.KnowledgeGraph;

namespace ArchitectureAssistant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BasicAnalysisEngine>();
        services.AddSingleton<KnowledgeGraphProjector>();
        services.AddSingleton<AnalysisCandidateComparer>();
        services.AddSingleton<ArchitecturalDiagramProjector>();
        return services;
    }
}
