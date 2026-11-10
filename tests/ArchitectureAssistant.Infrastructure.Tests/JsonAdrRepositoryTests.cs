using ArchitectureAssistant.Domain.Adrs;
using ArchitectureAssistant.Domain.Projects;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArchitectureAssistant.Infrastructure.Tests;

public sealed class JsonAdrRepositoryTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(),
        "ArchitectureDecisionAssistant.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly JsonAdrRepository _repository;

    public JsonAdrRepositoryTests() =>
        _repository = new JsonAdrRepository(_workspace, NullLogger<JsonAdrRepository>.Instance);

    [Fact]
    public async Task SaveAsync_AppendsValidatedRevisionsAndLoadsLatest()
    {
        var (project, decision) = CreateProjectWithDecision();
        var composed = AdrSnapshot.Compose(project, decision, "adr-1", DateTimeOffset.UtcNow);
        Assert.True(composed.TryGetValue(out var draft));

        var saved = await _repository.SaveAsync(draft, null, CancellationToken.None);
        Assert.True(saved.IsSuccess);
        var revision = draft.ReviseSection(
            AdrSectionKind.Problem,
            "The system needs a documented choice.",
            "architect",
            draft.UpdatedAt.AddMinutes(1));
        Assert.True(revision.TryGetValue(out var revised));

        var savedRevision = await _repository.SaveAsync(revised, draft.Revision, CancellationToken.None);
        var loaded = await _repository.LoadLatestAsync(project.Id, draft.AdrId, CancellationToken.None);

        Assert.True(savedRevision.IsSuccess);
        Assert.True(loaded.TryGetValue(out var latest));
        Assert.Equal(2, latest.Revision);
        Assert.Equal("The system needs a documented choice.", latest.Sections.Problem.Content);
        Assert.Equal(2, Directory.GetFiles(
            Path.Combine(_workspace, "adrs", project.Id.ToString("N"), "adr-1"),
            "*.json").Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private static (Project Project, ArchitecturalDecision Decision) CreateProjectWithDecision()
    {
        var now = DateTimeOffset.UtcNow;
        var created = Project.Create(Guid.NewGuid(), "Orders", "Process orders", now);
        Assert.True(created.TryGetValue(out var project));
        var decisionResult = ArchitecturalDecision.Create(
            Guid.NewGuid(),
            "modular-monolith",
            "Architecture",
            ["microservices"],
            DecisionStatus.Proposed,
            "The team selected this option.",
            [],
            "architect",
            now);
        Assert.True(decisionResult.TryGetValue(out var decision));
        var added = project.AddDecision(decision, now);
        Assert.True(added.TryGetValue(out var projectWithDecision));
        return (projectWithDecision, decision);
    }
}
