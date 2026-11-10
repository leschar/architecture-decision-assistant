using ArchitectureAssistant.Domain.History;
using ArchitectureAssistant.Domain.Projects;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace ArchitectureAssistant.Infrastructure.Tests;

public sealed class JsonHistoryRepositoryTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(),
        "ArchitectureDecisionAssistant.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly JsonHistoryRepository _repository;

    public JsonHistoryRepositoryTests() =>
        _repository = new JsonHistoryRepository(_workspace, NullLogger<JsonHistoryRepository>.Instance);

    [Fact]
    public async Task ArchiveAsync_CreatesVerifiedImmutableSnapshotAndRequiresRemovalConfirmation()
    {
        var now = DateTimeOffset.UtcNow;
        var created = Project.Create(Guid.NewGuid(), "Orders", "Process orders", now);
        Assert.True(created.TryGetValue(out var project));
        var archiveId = Guid.NewGuid().ToString("N");

        var archived = await _repository.ArchiveAsync(
            project,
            archiveId,
            "architect",
            "Reviewed before archiving",
            now,
            CancellationToken.None);

        Assert.True(archived.TryGetValue(out var record));
        var snapshotPath = Path.Combine(_workspace, "history", "snapshots", $"{archiveId}.json");
        var originalSnapshot = await File.ReadAllBytesAsync(snapshotPath);
        var fingerprint = $"sha256:{Convert.ToHexString(SHA256.HashData(originalSnapshot)).ToLowerInvariant()}";
        Assert.Equal(fingerprint, record.ContentFingerprint);
        var loadedProject = await _repository.LoadArchivedProjectAsync(archiveId, CancellationToken.None);
        Assert.True(loadedProject.TryGetValue(out var restored));
        Assert.Equal(project.Id, restored.Id);
        Assert.Equal(project.Revision, restored.Revision);
        var updatedContext = project.Context.Update(
            "Retail",
            12,
            ProjectCriticality.High,
            [],
            ScalabilityTarget.High);
        Assert.True(updatedContext.TryGetValue(out var context));
        Assert.True(project.UpdateContext(context, now.AddMinutes(1)).IsSuccess);
        Assert.Equal(originalSnapshot, await File.ReadAllBytesAsync(snapshotPath));

        var deniedRemoval = await _repository.RemoveAsync(archiveId, false, CancellationToken.None);
        Assert.False(deniedRemoval.IsSuccess);
        Assert.True(File.Exists(snapshotPath));

        var removed = await _repository.RemoveAsync(archiveId, true, CancellationToken.None);
        Assert.True(removed.IsSuccess);
        Assert.False(File.Exists(snapshotPath));
    }

    [Fact]
    public async Task RebuildIndexAsync_ReconstructsIndexFromValidRecordsAfterCorruption()
    {
        var now = DateTimeOffset.UtcNow;
        var created = Project.Create(Guid.NewGuid(), "Orders", "Process orders", now);
        Assert.True(created.TryGetValue(out var project));
        var archived = await _repository.ArchiveAsync(
            project,
            Guid.NewGuid().ToString("N"),
            "architect",
            null,
            now,
            CancellationToken.None);
        Assert.True(archived.IsSuccess);
        var indexPath = Path.Combine(_workspace, "history", "index.json");
        await File.WriteAllTextAsync(indexPath, "{ invalid");

        var rebuilt = await _repository.RebuildIndexAsync(CancellationToken.None);

        Assert.True(rebuilt.TryGetValue(out var result));
        Assert.True(result.IndexRebuilt);
        Assert.Single(result.Records);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "history.index.rebuilt");
    }

    [Fact]
    public async Task LoadSimilarityPolicyAsync_RejectsDuplicateFeaturePaths()
    {
        var policyDirectory = Path.Combine(_workspace, "policies", "history-similarity");
        Directory.CreateDirectory(policyDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(policyDirectory, "local-history.json"),
            """
            {
              "schemaVersion": "1.0",
              "policyId": "local-history",
              "version": "1.0.0",
              "minimumCoverage": 0.5,
              "dimensions": [
                { "id": "domain", "featurePath": "project.businessDomain", "weight": 1, "comparison": "Exact" },
                { "id": "domain-copy", "featurePath": "project.businessDomain", "weight": 1, "comparison": "Exact" }
              ]
            }
            """);

        var result = await _repository.LoadSimilarityPolicyAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "history.policy.invalid");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }
}
