using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Adrs;
using ArchitectureAssistant.Domain.Common;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

public sealed class JsonAdrRepository : IAdrRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private const int LockRetryCount = 10;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(30);

    private readonly string _adrsDirectory;
    private readonly JsonSchema _schema;
    private readonly ILogger<JsonAdrRepository> _logger;

    public JsonAdrRepository(string workspaceRoot, ILogger<JsonAdrRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(logger);
        _adrsDirectory = Path.Combine(Path.GetFullPath(workspaceRoot), "adrs");
        _logger = logger;

        using var schemaStream = typeof(JsonAdrRepository).Assembly.GetManifestResourceStream(
            "ArchitectureAssistant.Infrastructure.Schemas.adr.schema.json");
        if (schemaStream is null)
        {
            throw new InvalidOperationException("The embedded ADR JSON schema is missing.");
        }

        using var reader = new StreamReader(schemaStream);
        _schema = JsonSchema.FromText(reader.ReadToEnd());
    }

    public async Task<Result<AdrSnapshot>> SaveAsync(
        AdrSnapshot snapshot,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!IsValidIdentity(snapshot.ProjectId, snapshot.AdrId) ||
            snapshot.Revision < 1 ||
            expectedRevision is null && snapshot.Revision != 1 ||
            expectedRevision is not null && snapshot.Revision != expectedRevision.Value + 1)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.document.invalid", "The ADR revision or identity is invalid."));
        }

        var directory = GetAdrDirectory(snapshot.ProjectId, snapshot.AdrId);
        var path = Path.Combine(directory, $"{snapshot.Revision:D8}.json");
        try
        {
            Directory.CreateDirectory(directory);
            var lockResult = await AcquireLockAsync(path, cancellationToken);
            if (!lockResult.TryGetValue(out var writeLock))
            {
                return Result<AdrSnapshot>.Failure(lockResult.Errors);
            }

            await using (writeLock)
            {
                var latest = await LoadLatestAsync(snapshot.ProjectId, snapshot.AdrId, cancellationToken);
                if (expectedRevision is null)
                {
                    if (latest.IsSuccess)
                    {
                        return Result<AdrSnapshot>.Failure(
                            new DomainError("adr.conflict.exists", "An ADR with this ID already exists."));
                    }

                    if (latest.Errors.Any(error => error.Code != "adr.not_found"))
                    {
                        return Result<AdrSnapshot>.Failure(latest.Errors);
                    }
                }
                else if (!latest.TryGetValue(out var current))
                {
                    return Result<AdrSnapshot>.Failure(latest.Errors);
                }
                else if (current.Revision != expectedRevision.Value)
                {
                    return Result<AdrSnapshot>.Failure(
                        new DomainError("adr.conflict.revision", "The ADR changed on disk. Reload it before saving."));
                }

                var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
                var validation = ValidateDocument(json, Path.GetFileName(path));
                if (!validation.IsSuccess)
                {
                    return Result<AdrSnapshot>.Failure(validation.Errors);
                }

                if (File.Exists(path))
                {
                    return Result<AdrSnapshot>.Failure(
                        new DomainError("adr.conflict.revision", "This immutable ADR revision already exists."));
                }

                await WriteAtomicallyAsync(path, json, cancellationToken);
                return Result<AdrSnapshot>.Success(snapshot);
            }
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to save ADR {AdrId} for project {ProjectId}.", snapshot.AdrId, snapshot.ProjectId);
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.file.write_failed", "The ADR revision could not be saved."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to ADR {AdrId} was denied.", snapshot.AdrId);
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.file.access_denied", "The application does not have access to save this ADR."));
        }
    }

    public async Task<Result<AdrSnapshot>> LoadLatestAsync(
        Guid projectId,
        string adrId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(projectId, adrId))
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.identity.invalid", "Project and ADR IDs are invalid."));
        }

        var directory = GetAdrDirectory(projectId, adrId);
        try
        {
            if (!Directory.Exists(directory))
            {
                return Result<AdrSnapshot>.Failure(
                    new DomainError("adr.not_found", "The ADR was not found."));
            }

            var revisions = new List<AdrSnapshot>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await ReadSnapshotAsync(path, projectId, adrId, cancellationToken);
                if (!read.TryGetValue(out var snapshot))
                {
                    return Result<AdrSnapshot>.Failure(read.Errors);
                }

                revisions.Add(snapshot);
            }

            var ordered = revisions.OrderByDescending(snapshot => snapshot.Revision).Take(2).ToArray();
            return ordered.Length > 0
                ? Result<AdrSnapshot>.Success(ordered[0])
                : Result<AdrSnapshot>.Failure(new DomainError("adr.not_found", "The ADR was not found."));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read ADR {AdrId}.", adrId);
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.file.read_failed", "The ADR could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to ADR {AdrId} was denied.", adrId);
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.file.access_denied", "The application does not have access to read this ADR."));
        }
    }

    public async Task<Result<IReadOnlyList<AdrSnapshot>>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return Result<IReadOnlyList<AdrSnapshot>>.Failure(
                new DomainError("adr.project.invalid", "Project ID must not be empty."));
        }

        try
        {
            if (!Directory.Exists(GetProjectDirectory(projectId)))
            {
                return Result<IReadOnlyList<AdrSnapshot>>.Success(Array.Empty<AdrSnapshot>());
            }

            var snapshots = new List<AdrSnapshot>();
            foreach (var directory in Directory.EnumerateDirectories(GetProjectDirectory(projectId)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var revisions = new List<AdrSnapshot>();
                foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var read = await ReadSnapshotAsync(path, projectId, null, cancellationToken);
                    if (!read.TryGetValue(out var snapshot))
                    {
                        return Result<IReadOnlyList<AdrSnapshot>>.Failure(read.Errors);
                    }

                    revisions.Add(snapshot);
                }

                var latest = revisions.OrderByDescending(snapshot => snapshot.Revision).FirstOrDefault();
                if (latest is not null)
                {
                    snapshots.Add(latest);
                }
            }

            return Result<IReadOnlyList<AdrSnapshot>>.Success(
                snapshots.OrderByDescending(snapshot => snapshot.UpdatedAt)
                    .ThenBy(snapshot => snapshot.AdrId, StringComparer.Ordinal)
                    .ToArray());
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to list ADRs for project {ProjectId}.", projectId);
            return Result<IReadOnlyList<AdrSnapshot>>.Failure(
                new DomainError("adr.file.read_failed", "ADRs for this project could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to ADRs for project {ProjectId} was denied.", projectId);
            return Result<IReadOnlyList<AdrSnapshot>>.Failure(
                new DomainError("adr.file.access_denied", "The application does not have access to this project's ADRs."));
        }
    }

    private async Task<Result<AdrSnapshot>> ReadSnapshotAsync(
        string path,
        Guid projectId,
        string? adrId,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var validation = ValidateDocument(json, Path.GetFileName(path));
        if (!validation.IsSuccess)
        {
            return Result<AdrSnapshot>.Failure(validation.Errors);
        }

        var snapshot = JsonSerializer.Deserialize<AdrSnapshot>(json, SerializerOptions);
        return snapshot is not null &&
               snapshot.ProjectId == projectId &&
               (adrId is null || snapshot.AdrId == adrId) &&
               Path.GetFileNameWithoutExtension(path) == snapshot.Revision.ToString("D8")
            ? Result<AdrSnapshot>.Success(snapshot)
            : Result<AdrSnapshot>.Failure(
                new DomainError("adr.document.invalid", "The ADR file does not match its project, identity, or revision."));
    }

    private Result<bool> ValidateDocument(string json, string fileName)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "ADR file {AdrFile} contains invalid JSON.", fileName);
            return Result<bool>.Failure(new DomainError("adr.file.invalid_json", "The ADR file contains invalid JSON."));
        }

        using (document)
        {
            var evaluation = _schema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
            if (evaluation.IsValid)
            {
                return Result<bool>.Success(true);
            }

            _logger.LogWarning("ADR file {AdrFile} does not match the supported schema.", fileName);
            return Result<bool>.Failure(new DomainError(
                "adr.schema.invalid",
                $"ADR file '{fileName}' does not match schema version 1.0."));
        }
    }

    private async Task<Result<FileStream>> AcquireLockAsync(string revisionPath, CancellationToken cancellationToken)
    {
        var lockPath = revisionPath + ".lock";
        for (var attempt = 0; attempt < LockRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Result<FileStream>.Success(new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true));
            }
            catch (IOException) when (attempt + 1 < LockRetryCount)
            {
                await Task.Delay(LockRetryDelay, cancellationToken);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "Could not acquire the write lock for ADR file {AdrFile}.", lockPath);
                return Result<FileStream>.Failure(
                    new DomainError("adr.conflict.locked", "The ADR is being edited by another process. Try again."));
            }
        }

        return Result<FileStream>.Failure(
            new DomainError("adr.conflict.locked", "The ADR is being edited by another process. Try again."));
    }

    private static async Task WriteAtomicallyAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetProjectDirectory(Guid projectId) =>
        Path.Combine(_adrsDirectory, projectId.ToString("N"));

    private string GetAdrDirectory(Guid projectId, string adrId)
    {
        if (!IsValidIdentity(projectId, adrId))
        {
            throw new InvalidOperationException("The ADR path contains an invalid identity.");
        }

        var projectDirectory = GetProjectDirectory(projectId);
        var path = Path.GetFullPath(Path.Combine(projectDirectory, adrId));
        var prefix = projectDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The ADR path is outside the configured workspace.");
        }

        return path;
    }

    private static bool IsValidIdentity(Guid projectId, string adrId) =>
        projectId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(adrId) &&
        Regex.IsMatch(adrId, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,119}$", RegexOptions.CultureInvariant);

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
