using System.Text.Json;
using System.Text.Json.Serialization;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

public sealed class JsonAnalysisRepository : IAnalysisRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _analysesDirectory;
    private readonly JsonSchema _analysisSchema;
    private readonly ILogger<JsonAnalysisRepository> _logger;

    public JsonAnalysisRepository(string workspaceRoot, ILogger<JsonAnalysisRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(logger);

        _analysesDirectory = Path.Combine(Path.GetFullPath(workspaceRoot), "analyses");
        _logger = logger;

        using var schemaStream = typeof(JsonAnalysisRepository).Assembly.GetManifestResourceStream(
            "ArchitectureAssistant.Infrastructure.Schemas.analysis-snapshot.schema.json");
        if (schemaStream is null)
        {
            throw new InvalidOperationException("The embedded analysis snapshot JSON schema is missing.");
        }

        using var reader = new StreamReader(schemaStream);
        _analysisSchema = JsonSchema.FromText(reader.ReadToEnd());
    }

    public async Task<Result<AnalysisSnapshot>> SaveAsync(
        AnalysisSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.AnalysisId == Guid.Empty || snapshot.ProjectId == Guid.Empty ||
            snapshot.ProjectRevision < 1 || snapshot.SchemaVersion is not ("1.0" or "2.0") ||
            snapshot.CandidateGroups is null || snapshot.Limitations is null)
        {
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.snapshot.invalid", "The analysis snapshot is incomplete or invalid."));
        }

        try
        {
            var projectDirectory = GetProjectDirectory(snapshot.ProjectId);
            Directory.CreateDirectory(projectDirectory);
            var filePath = GetSnapshotPath(projectDirectory, snapshot.AnalysisId);
            var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
                var validation = ValidateDocument(json, Path.GetFileName(filePath));
                if (!validation.IsSuccess)
                {
                    return Result<AnalysisSnapshot>.Failure(validation.Errors);
                }

                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                File.Move(temporaryPath, filePath, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException cleanupException)
                    {
                        _logger.LogWarning(
                            cleanupException,
                            "Failed to remove a temporary analysis file for {AnalysisId}.",
                            snapshot.AnalysisId);
                    }
                    catch (UnauthorizedAccessException cleanupException)
                    {
                        _logger.LogWarning(
                            cleanupException,
                            "Access was denied while removing a temporary analysis file for {AnalysisId}.",
                            snapshot.AnalysisId);
                    }
                }
            }

            return Result<AnalysisSnapshot>.Success(snapshot);
        }
        catch (IOException exception) when (File.Exists(GetSnapshotPath(GetProjectDirectory(snapshot.ProjectId), snapshot.AnalysisId)))
        {
            _logger.LogWarning(exception, "Analysis {AnalysisId} already exists and is immutable.", snapshot.AnalysisId);
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.snapshot.duplicate", "An analysis with this ID already exists and cannot be replaced."));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to save analysis {AnalysisId}.", snapshot.AnalysisId);
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.file.write_failed", "The analysis snapshot could not be saved to the local workspace."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to the analysis workspace was denied while saving {AnalysisId}.", snapshot.AnalysisId);
            return Result<AnalysisSnapshot>.Failure(
                new DomainError("analysis.file.access_denied", "The application does not have permission to save this analysis."));
        }
    }

    public async Task<Result<IReadOnlyList<AnalysisSnapshot>>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return Result<IReadOnlyList<AnalysisSnapshot>>.Failure(
                new DomainError("analysis.project_id.invalid", "Project ID must not be empty."));
        }

        try
        {
            var projectDirectory = GetProjectDirectory(projectId);
            if (!Directory.Exists(projectDirectory))
            {
                return Result<IReadOnlyList<AnalysisSnapshot>>.Success(Array.Empty<AnalysisSnapshot>());
            }

            var snapshots = new List<AnalysisSnapshot>();
            foreach (var filePath in Directory.EnumerateFiles(projectDirectory, "*.json", SearchOption.TopDirectoryOnly)
                         .Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var validation = ValidateDocument(json, Path.GetFileName(filePath));
                if (!validation.IsSuccess)
                {
                    return Result<IReadOnlyList<AnalysisSnapshot>>.Failure(validation.Errors);
                }

                var snapshot = JsonSerializer.Deserialize<AnalysisSnapshot>(json, SerializerOptions);
                if (snapshot is null || snapshot.SchemaVersion is not ("1.0" or "2.0") ||
                    snapshot.ProjectId != projectId || snapshot.AnalysisId == Guid.Empty ||
                    !string.Equals(
                        Path.GetFileName(filePath),
                        $"{snapshot.AnalysisId:N}.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Analysis file {AnalysisFile} has an invalid or mismatched snapshot identity.",
                        Path.GetFileName(filePath));
                    return Result<IReadOnlyList<AnalysisSnapshot>>.Failure(
                        new DomainError(
                            "analysis.file.invalid",
                            $"Analysis file '{Path.GetFileName(filePath)}' is invalid or belongs to a different project."));
                }

                snapshots.Add(snapshot);
            }

            return Result<IReadOnlyList<AnalysisSnapshot>>.Success(snapshots);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "An analysis snapshot for project {ProjectId} contains invalid JSON.", projectId);
            return Result<IReadOnlyList<AnalysisSnapshot>>.Failure(
                new DomainError("analysis.file.invalid_json", "An analysis snapshot contains invalid JSON."));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read analysis snapshots for project {ProjectId}.", projectId);
            return Result<IReadOnlyList<AnalysisSnapshot>>.Failure(
                new DomainError("analysis.file.read_failed", "Analysis snapshots could not be read from the local workspace."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to analysis snapshots for project {ProjectId} was denied.", projectId);
            return Result<IReadOnlyList<AnalysisSnapshot>>.Failure(
                new DomainError("analysis.file.access_denied", "The application does not have access to the analysis snapshots."));
        }
    }

    private string GetProjectDirectory(Guid projectId)
    {
        var path = Path.GetFullPath(Path.Combine(_analysesDirectory, projectId.ToString("N")));
        var prefix = _analysesDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _analysesDirectory
            : _analysesDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The analysis path is outside the configured workspace.");
        }

        return path;
    }

    private static string GetSnapshotPath(string projectDirectory, Guid analysisId) =>
        Path.Combine(projectDirectory, $"{analysisId:N}.json");

    private Result<bool> ValidateDocument(string json, string fileName)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Analysis file {AnalysisFile} contains invalid JSON.", fileName);
            return Result<bool>.Failure(
                new DomainError("analysis.file.invalid_json", "The analysis snapshot contains invalid JSON."));
        }

        using (document)
        {
            var evaluation = _analysisSchema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
            if (evaluation.IsValid)
            {
                return Result<bool>.Success(true);
            }

            var instancePath = evaluation.InstanceLocation.ToString();
            _logger.LogWarning(
                "Analysis file {AnalysisFile} does not match the supported snapshot schema at {InstancePath}.",
                fileName,
                instancePath);
            return Result<bool>.Failure(
                new DomainError(
                    "analysis.schema.invalid",
                    $"Analysis file '{fileName}' does not match schema version 1.0 (location: {instancePath})."));
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
