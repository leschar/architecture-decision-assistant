using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.History;
using ArchitectureAssistant.Domain.Projects;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

public sealed class JsonHistoryRepository : IHistoryRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly string _workspaceRoot;
    private readonly string _historyDirectory;
    private readonly string _recordsDirectory;
    private readonly string _snapshotsDirectory;
    private readonly string _indexPath;
    private readonly JsonSchema _recordSchema;
    private readonly JsonSchema _indexSchema;
    private readonly JsonSchema _policySchema;
    private readonly JsonSchema _projectSchema;
    private readonly ILogger<JsonHistoryRepository> _logger;

    public JsonHistoryRepository(string workspaceRoot, ILogger<JsonHistoryRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(logger);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _historyDirectory = Path.Combine(_workspaceRoot, "history");
        _recordsDirectory = Path.Combine(_historyDirectory, "records");
        _snapshotsDirectory = Path.Combine(_historyDirectory, "snapshots");
        _indexPath = Path.Combine(_historyDirectory, "index.json");
        _logger = logger;
        _recordSchema = LoadSchema("historical-project.schema.json");
        _indexSchema = LoadSchema("history-index.schema.json");
        _policySchema = LoadSchema("history-similarity-policy.schema.json");
        _projectSchema = LoadSchema("project.schema.json");
    }

    public async Task<Result<HistoricalProjectRecord>> ArchiveAsync(
        Project project,
        string archiveId,
        string confirmedBy,
        string? note,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!IsValidArchiveId(archiveId) ||
            string.IsNullOrWhiteSpace(confirmedBy) ||
            confirmedBy.Trim().Length > 120 ||
            confirmedAt == default)
        {
            return Result<HistoricalProjectRecord>.Failure(
                new DomainError("history.archive.invalid", "Archive confirmation or identity is invalid."));
        }

        try
        {
            Directory.CreateDirectory(_recordsDirectory);
            Directory.CreateDirectory(_snapshotsDirectory);
            var snapshotPath = GetSnapshotPath(archiveId);
            var recordPath = GetRecordPath(archiveId);
            if (File.Exists(snapshotPath) || File.Exists(recordPath))
            {
                return Result<HistoricalProjectRecord>.Failure(
                    new DomainError("history.archive.collision", "An archive with this ID already exists."));
            }

            var projectDocument = ProjectDocumentMapper.FromDomain(project);
            var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(projectDocument, SerializerOptions);
            var projectValidation = ValidateJson(Encoding.UTF8.GetString(snapshotBytes), _projectSchema, Path.GetFileName(snapshotPath));
            if (!projectValidation.IsSuccess)
            {
                return Result<HistoricalProjectRecord>.Failure(projectValidation.Errors);
            }

            var fingerprint = Fingerprint(snapshotBytes);
            var record = HistoricalProjectRecordFactory.Create(
                project,
                archiveId,
                confirmedBy,
                confirmedAt,
                Path.GetFileName(snapshotPath),
                fingerprint,
                note);
            var recordJson = JsonSerializer.Serialize(record, SerializerOptions);
            var recordValidation = ValidateJson(recordJson, _recordSchema, Path.GetFileName(recordPath));
            if (!recordValidation.IsSuccess)
            {
                return Result<HistoricalProjectRecord>.Failure(recordValidation.Errors);
            }

            await WriteAtomicallyAsync(snapshotPath, snapshotBytes, overwrite: false, cancellationToken);
            await WriteAtomicallyAsync(recordPath, Encoding.UTF8.GetBytes(recordJson), overwrite: false, cancellationToken);

            var index = await RebuildIndexCoreAsync(cancellationToken);
            if (!index.TryGetValue(out _))
            {
                return Result<HistoricalProjectRecord>.Failure(index.Errors);
            }

            return Result<HistoricalProjectRecord>.Success(record);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Archiving project {ProjectId} failed.", project.Id);
            return Result<HistoricalProjectRecord>.Failure(
                new DomainError("history.archive.partial_failure", "Archiving did not complete successfully. Check the history workspace and rebuild its index."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access was denied while archiving project {ProjectId}.", project.Id);
            return Result<HistoricalProjectRecord>.Failure(
                new DomainError("history.archive.access_denied", "The application does not have access to write the history workspace."));
        }
    }

    public async Task<Result<HistoryListResult>> ListAsync(CancellationToken cancellationToken) =>
        await RebuildIndexCoreAsync(cancellationToken, writeWhenNeeded: false);

    public async Task<Result<Project>> LoadArchivedProjectAsync(
        string archiveId,
        CancellationToken cancellationToken)
    {
        if (!IsValidArchiveId(archiveId))
        {
            return Result<Project>.Failure(
                new DomainError("history.archive_id.invalid", "The archive ID is invalid."));
        }

        var records = await ListAsync(cancellationToken);
        if (!records.TryGetValue(out var listed))
        {
            return Result<Project>.Failure(records.Errors);
        }

        var matches = listed.Records.Where(record => record.ArchiveId == archiveId).Take(2).ToArray();
        if (matches.Length != 1)
        {
            return Result<Project>.Failure(
                new DomainError("history.archive.not_found", "The verified historical project was not found uniquely."));
        }

        try
        {
            var record = matches[0];
            var path = GetSnapshotPath(record.ArchiveId);
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            if (Fingerprint(bytes) != record.ContentFingerprint)
            {
                return Result<Project>.Failure(
                    new DomainError("history.snapshot.hash_mismatch", "The historical snapshot fingerprint does not match its record."));
            }

            var document = JsonSerializer.Deserialize<ProjectDocument>(bytes, SerializerOptions);
            if (document is null)
            {
                return Result<Project>.Failure(
                    new DomainError("history.snapshot.invalid", "The historical project snapshot could not be deserialized."));
            }

            var restored = ProjectDocumentMapper.ToDomain(document);
            return restored.TryGetValue(out var project) &&
                   project.Id.ToString("D") == record.ProjectId &&
                   project.Revision.ToString() == record.ProjectRevision
                ? Result<Project>.Success(project)
                : Result<Project>.Failure(
                    new DomainError("history.snapshot.invalid", "The snapshot project identity or revision does not match its history record."));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read historical snapshot {ArchiveId}.", archiveId);
            return Result<Project>.Failure(
                new DomainError("history.snapshot.read_failed", "The historical snapshot could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to historical snapshot {ArchiveId} was denied.", archiveId);
            return Result<Project>.Failure(
                new DomainError("history.snapshot.access_denied", "The application does not have access to this snapshot."));
        }
    }

    public async Task<Result<bool>> RemoveAsync(
        string archiveId,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed)
        {
            return Result<bool>.Failure(
                new DomainError("history.remove.confirmation_required", "Explicit confirmation is required to remove a historical project."));
        }

        var loaded = await ListAsync(cancellationToken);
        if (!loaded.TryGetValue(out var list))
        {
            return Result<bool>.Failure(loaded.Errors);
        }

        var matches = list.Records.Where(record => record.ArchiveId == archiveId).Take(2).ToArray();
        if (matches.Length != 1)
        {
            return Result<bool>.Failure(
                new DomainError("history.archive.not_found", "The historical project was not found uniquely."));
        }

        try
        {
            File.Delete(GetSnapshotPath(archiveId));
            File.Delete(GetRecordPath(archiveId));
            var rebuilt = await RebuildIndexCoreAsync(cancellationToken);
            return rebuilt.IsSuccess
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(rebuilt.Errors);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Removing historical project {ArchiveId} failed.", archiveId);
            return Result<bool>.Failure(
                new DomainError("history.remove.partial_failure", "Removal was incomplete. Rebuild the index and inspect the history files."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access was denied while removing historical project {ArchiveId}.", archiveId);
            return Result<bool>.Failure(
                new DomainError("history.remove.access_denied", "The application does not have access to remove this historical project."));
        }
    }

    public async Task<Result<HistoryListResult>> RebuildIndexAsync(CancellationToken cancellationToken) =>
        await RebuildIndexCoreAsync(cancellationToken, forceRebuild: true);

    public async Task<Result<HistoryPolicyLoadResult>> LoadSimilarityPolicyAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_workspaceRoot, "policies", "history-similarity");
        try
        {
            if (!Directory.Exists(directory))
            {
                return Result<HistoryPolicyLoadResult>.Success(new HistoryPolicyLoadResult(
                    null,
                    [new HistoryDiagnostic("history.policy.missing", "No similarity policy is configured; metadata search remains available.")]));
            }

            var policies = new List<HistorySimilarityPolicy>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                         .Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                var validation = ValidateJson(json, _policySchema, Path.GetFileName(path));
                if (!validation.IsSuccess)
                {
                    return Result<HistoryPolicyLoadResult>.Failure(validation.Errors);
                }

                var policy = JsonSerializer.Deserialize<HistorySimilarityPolicy>(json, SerializerOptions);
                if (policy is null || !HistorySimilarityPolicyRules.Validate(policy).IsSuccess)
                {
                    return Result<HistoryPolicyLoadResult>.Failure(
                        new DomainError("history.policy.invalid", $"Similarity policy '{Path.GetFileName(path)}' contains invalid dimensions or weights."));
                }

                policies.Add(policy);
            }

            if (policies.Count == 0)
            {
                return Result<HistoryPolicyLoadResult>.Success(new HistoryPolicyLoadResult(
                    null,
                    [new HistoryDiagnostic("history.policy.missing", "No similarity policy is configured; metadata search remains available.")]));
            }

            if (policies.Count > 1)
            {
                return Result<HistoryPolicyLoadResult>.Failure(
                    new DomainError("history.policy.ambiguous", "More than one similarity policy is configured; select one policy before comparing."));
            }

            return Result<HistoryPolicyLoadResult>.Success(new HistoryPolicyLoadResult(policies[0], Array.Empty<HistoryDiagnostic>()));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read the historical similarity policy.");
            return Result<HistoryPolicyLoadResult>.Failure(
                new DomainError("history.policy.read_failed", "The similarity policy could not be read."));
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "The historical similarity policy contains invalid JSON values.");
            return Result<HistoryPolicyLoadResult>.Failure(
                new DomainError("history.policy.invalid", "The similarity policy contains values outside the supported JSON contract."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to the historical similarity policy was denied.");
            return Result<HistoryPolicyLoadResult>.Failure(
                new DomainError("history.policy.access_denied", "The application does not have access to the similarity policy."));
        }
    }

    private async Task<Result<HistoryListResult>> RebuildIndexCoreAsync(
        CancellationToken cancellationToken,
        bool forceRebuild = false,
        bool writeWhenNeeded = true)
    {
        try
        {
            if (writeWhenNeeded)
            {
                Directory.CreateDirectory(_recordsDirectory);
                Directory.CreateDirectory(_snapshotsDirectory);
            }
            else if (!Directory.Exists(_recordsDirectory))
            {
                return Result<HistoryListResult>.Success(new HistoryListResult(
                    Array.Empty<HistoricalProjectRecord>(),
                    [new HistoryDiagnostic("history.index.rebuild_required", "The local history index does not exist; rebuilding is available explicitly.")],
                    false));
            }

            var records = new List<HistoricalProjectRecord>();
            var diagnostics = new List<HistoryDiagnostic>();
            foreach (var path in Directory.EnumerateFiles(_recordsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                         .Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recordResult = await ReadVerifiedRecordAsync(path, cancellationToken);
                if (recordResult.TryGetValue(out var record))
                {
                    records.Add(record);
                }
                else
                {
                    diagnostics.Add(new HistoryDiagnostic(
                        "history.record.invalid",
                        $"{Path.GetFileName(path)} is excluded: {recordResult.Errors[0].Message}"));
                }
            }

            var entries = records.OrderBy(record => record.ArchiveId, StringComparer.Ordinal)
                .Select(record => new HistoryIndexEntry(
                    record.ArchiveId,
                    Path.GetFileName(GetRecordPath(record.ArchiveId)),
                    record.ProjectId,
                    record.ArchivedAt,
                    record.ContentFingerprint))
                .ToArray();
            var expectedIndex = new HistoryIndex(
                "1.0",
                await GetNextIndexRevisionAsync(cancellationToken),
                entries);
            var currentValid = !forceRebuild && await IsIndexCurrentAsync(entries, cancellationToken);
            var rebuiltIndex = !currentValid;
            if (rebuiltIndex)
            {
                var indexWritten = writeWhenNeeded || forceRebuild;
                diagnostics.Add(new HistoryDiagnostic(
                    indexWritten ? "history.index.rebuilt" : "history.index.rebuild_required",
                    indexWritten
                        ? "The local history index was rebuilt from verified records."
                        : "The local history index is missing, invalid, or out of date; use the explicit rebuild action to repair it."));
                if (indexWritten)
                {
                    var indexJson = JsonSerializer.Serialize(expectedIndex, SerializerOptions);
                    var validation = ValidateJson(indexJson, _indexSchema, Path.GetFileName(_indexPath));
                    if (!validation.IsSuccess)
                    {
                        return Result<HistoryListResult>.Failure(validation.Errors);
                    }

                    await WriteAtomicallyAsync(_indexPath, Encoding.UTF8.GetBytes(indexJson), overwrite: true, cancellationToken);
                }

                rebuiltIndex = indexWritten;
            }

            return Result<HistoryListResult>.Success(new HistoryListResult(
                records.OrderByDescending(record => record.ArchivedAt)
                    .ThenBy(record => record.ArchiveId, StringComparer.Ordinal).ToArray(),
                diagnostics,
                rebuiltIndex));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to rebuild the local history index.");
            return Result<HistoryListResult>.Failure(
                new DomainError("history.index.rebuild_failed", "The local history index could not be rebuilt."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to the local history index was denied.");
            return Result<HistoryListResult>.Failure(
                new DomainError("history.index.access_denied", "The application does not have access to the history workspace."));
        }
    }

    private async Task<Result<HistoricalProjectRecord>> ReadVerifiedRecordAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var validation = ValidateJson(json, _recordSchema, Path.GetFileName(path));
        if (!validation.IsSuccess)
        {
            return Result<HistoricalProjectRecord>.Failure(validation.Errors);
        }

        var record = JsonSerializer.Deserialize<HistoricalProjectRecord>(json, SerializerOptions);
        if (record is null ||
            !IsValidArchiveId(record.ArchiveId) ||
            Path.GetFileName(path) != $"{record.ArchiveId}.json" ||
            record.SnapshotFile != $"{record.ArchiveId}.json")
        {
            return Result<HistoricalProjectRecord>.Failure(
                new DomainError("history.record.invalid", "The history record identity or snapshot filename is invalid."));
        }

        var snapshotPath = GetSnapshotPath(record.ArchiveId);
        if (!File.Exists(snapshotPath))
        {
            return Result<HistoricalProjectRecord>.Failure(
                new DomainError("history.snapshot.missing", "The referenced historical snapshot is missing."));
        }

        var snapshotBytes = await File.ReadAllBytesAsync(snapshotPath, cancellationToken);
        if (Fingerprint(snapshotBytes) != record.ContentFingerprint)
        {
            return Result<HistoricalProjectRecord>.Failure(
                new DomainError("history.snapshot.hash_mismatch", "The historical snapshot hash does not match its record."));
        }

        var snapshotValidation = ValidateJson(Encoding.UTF8.GetString(snapshotBytes), _projectSchema, record.SnapshotFile);
        if (!snapshotValidation.IsSuccess)
        {
            return Result<HistoricalProjectRecord>.Failure(snapshotValidation.Errors);
        }

        return Result<HistoricalProjectRecord>.Success(record);
    }

    private async Task<bool> IsIndexCurrentAsync(
        IReadOnlyList<HistoryIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexPath))
        {
            return false;
        }

        var json = await File.ReadAllTextAsync(_indexPath, cancellationToken);
        if (!ValidateJson(json, _indexSchema, Path.GetFileName(_indexPath)).IsSuccess)
        {
            return false;
        }

        var index = JsonSerializer.Deserialize<HistoryIndex>(json, SerializerOptions);
        return index is not null &&
               index.Entries.OrderBy(entry => entry.ArchiveId, StringComparer.Ordinal)
                   .SequenceEqual(entries);
    }

    private async Task<long> GetNextIndexRevisionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexPath))
        {
            return 1;
        }

        var json = await File.ReadAllTextAsync(_indexPath, cancellationToken);
        if (!ValidateJson(json, _indexSchema, Path.GetFileName(_indexPath)).IsSuccess)
        {
            return 1;
        }

        var index = JsonSerializer.Deserialize<HistoryIndex>(json, SerializerOptions);
        return index is null || index.IndexRevision == long.MaxValue ? 1 : index.IndexRevision + 1;
    }

    private Result<bool> ValidateJson(string json, JsonSchema schema, string fileName)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "History file {HistoryFile} contains invalid JSON.", fileName);
            return Result<bool>.Failure(
                new DomainError("history.file.invalid_json", $"'{fileName}' contains invalid JSON."));
        }

        using (document)
        {
            var evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (evaluation.IsValid)
            {
                return Result<bool>.Success(true);
            }

            _logger.LogWarning("History file {HistoryFile} does not match its supported schema.", fileName);
            return Result<bool>.Failure(
                new DomainError("history.schema.invalid", $"'{fileName}' does not match its supported JSON schema."));
        }
    }

    private JsonSchema LoadSchema(string fileName)
    {
        using var stream = typeof(JsonHistoryRepository).Assembly.GetManifestResourceStream(
            $"ArchitectureAssistant.Infrastructure.Schemas.{fileName}");
        if (stream is null)
        {
            throw new InvalidOperationException($"The embedded {fileName} JSON schema is missing.");
        }

        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private string GetRecordPath(string archiveId) =>
        GetConfinedPath(_recordsDirectory, $"{archiveId}.json");

    private string GetSnapshotPath(string archiveId) =>
        GetConfinedPath(_snapshotsDirectory, $"{archiveId}.json");

    private static string GetConfinedPath(string directory, string fileName)
    {
        if (!IsValidArchiveId(Path.GetFileNameWithoutExtension(fileName)) || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException("The history file path contains an invalid archive ID.");
        }

        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        var prefix = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The history path is outside the configured workspace.");
        }

        return path;
    }

    private static bool IsValidArchiveId(string archiveId) =>
        !string.IsNullOrWhiteSpace(archiveId) &&
        Regex.IsMatch(archiveId, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,119}$", RegexOptions.CultureInvariant);

    private static string Fingerprint(byte[] bytes) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";

    private static async Task WriteAtomicallyAsync(
        string path,
        byte[] content,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
