using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Diagrams;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

public sealed class JsonDiagramRepository : IDiagramRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _diagramsDirectory;
    private readonly JsonSchema _diagramSchema;
    private readonly JsonSchema _layoutSchema;
    private readonly ILogger<JsonDiagramRepository> _logger;

    public JsonDiagramRepository(string workspaceRoot, ILogger<JsonDiagramRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(logger);

        _diagramsDirectory = Path.Combine(Path.GetFullPath(workspaceRoot), "diagrams");
        _logger = logger;

        using var schemaStream = typeof(JsonDiagramRepository).Assembly.GetManifestResourceStream(
            "ArchitectureAssistant.Infrastructure.Schemas.diagram.schema.json");
        if (schemaStream is null)
        {
            throw new InvalidOperationException("The embedded diagram JSON schema is missing.");
        }

        using var reader = new StreamReader(schemaStream);
        _diagramSchema = JsonSchema.FromText(reader.ReadToEnd());

        using var layoutSchemaStream = typeof(JsonDiagramRepository).Assembly.GetManifestResourceStream(
            "ArchitectureAssistant.Infrastructure.Schemas.diagram-layout.schema.json");
        if (layoutSchemaStream is null)
        {
            throw new InvalidOperationException("The embedded diagram layout JSON schema is missing.");
        }

        using var layoutSchemaReader = new StreamReader(layoutSchemaStream);
        _layoutSchema = JsonSchema.FromText(layoutSchemaReader.ReadToEnd());
    }

    public async Task<Result<ArchitecturalDiagram>> SaveAsync(
        ArchitecturalDiagram diagram,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        if (diagram.ProjectId == Guid.Empty ||
            !Enum.IsDefined(diagram.DiagramType) ||
            string.IsNullOrWhiteSpace(diagram.DiagramId) ||
            string.IsNullOrWhiteSpace(diagram.SourceFingerprint) ||
            !Regex.IsMatch(diagram.SourceFingerprint, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant) ||
            !long.TryParse(diagram.ProjectRevision, out var revision) ||
            revision < 1 ||
            string.IsNullOrWhiteSpace(diagram.ScopeId) ||
            diagram.Elements is null ||
            diagram.Connections is null ||
            diagram.Warnings is null)
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.snapshot.invalid", "The diagram snapshot is incomplete or invalid."));
        }

        var path = GetSnapshotPath(diagram);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(diagram, SerializerOptions);
            var validation = ValidateDocument(json, Path.GetFileName(path));
            if (!validation.IsSuccess)
            {
                return Result<ArchitecturalDiagram>.Failure(validation.Errors);
            }

            if (File.Exists(path))
            {
                return await ReadExistingSnapshotAsync(path, diagram, cancellationToken);
            }

            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                File.Move(temporaryPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                return await ReadExistingSnapshotAsync(path, diagram, cancellationToken);
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
                        _logger.LogWarning(cleanupException, "Failed to remove a temporary diagram snapshot.");
                    }
                    catch (UnauthorizedAccessException cleanupException)
                    {
                        _logger.LogWarning(cleanupException, "Access was denied while removing a temporary diagram snapshot.");
                    }
                }
            }

            return Result<ArchitecturalDiagram>.Success(diagram);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to save diagram {DiagramId}.", diagram.DiagramId);
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.file.write_failed", "The diagram snapshot could not be saved."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to diagram {DiagramId} was denied.", diagram.DiagramId);
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.file.access_denied", "The application does not have access to save this diagram."));
        }
    }

    public async Task<Result<IReadOnlyList<ArchitecturalDiagram>>> ListAsync(
                Guid projectId,
                CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return Result<IReadOnlyList<ArchitecturalDiagram>>.Failure(
                new DomainError("diagram.project.invalid", "Project ID must not be empty."));
        }

        var directory = GetProjectDirectory(projectId);
        try
        {
            if (!Directory.Exists(directory))
            {
                return Result<IReadOnlyList<ArchitecturalDiagram>>.Success(
                    Array.Empty<ArchitecturalDiagram>());
            }

            var snapshots = new List<ArchitecturalDiagram>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                         .Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                var validation = ValidateDocument(json, Path.GetFileName(path));
                if (!validation.IsSuccess)
                {
                    return Result<IReadOnlyList<ArchitecturalDiagram>>.Failure(validation.Errors);
                }

                var snapshot = JsonSerializer.Deserialize<ArchitecturalDiagram>(json, SerializerOptions);
                if (snapshot is null || snapshot.ProjectId != projectId)
                {
                    return Result<IReadOnlyList<ArchitecturalDiagram>>.Failure(
                        new DomainError("diagram.snapshot.invalid", "A saved diagram snapshot is invalid."));
                }

                snapshots.Add(snapshot);
            }

            return Result<IReadOnlyList<ArchitecturalDiagram>>.Success(
                snapshots.OrderByDescending(snapshot => snapshot.GeneratedAt)
                    .ThenBy(snapshot => snapshot.DiagramId, StringComparer.Ordinal)
                    .ToArray());
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to list diagram snapshots for project {ProjectId}.", projectId);
            return Result<IReadOnlyList<ArchitecturalDiagram>>.Failure(
                new DomainError("diagram.file.read_failed", "Saved diagram snapshots could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to diagram snapshots for project {ProjectId} was denied.", projectId);
            return Result<IReadOnlyList<ArchitecturalDiagram>>.Failure(
                new DomainError("diagram.file.access_denied", "The application does not have access to saved diagrams."));
        }
    }

    public async Task<Result<ArchitecturalDiagram>> LoadSnapshotAsync(
        Guid projectId,
        string diagramId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(diagramId))
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.snapshot.invalid", "Project and diagram IDs are required."));
        }

        var snapshots = await ListAsync(projectId, cancellationToken);
        if (!snapshots.TryGetValue(out var diagrams))
        {
            return Result<ArchitecturalDiagram>.Failure(snapshots.Errors);
        }

        var matches = diagrams.Where(snapshot => snapshot.DiagramId == diagramId).Take(2).ToArray();
        return matches.Length == 1
            ? Result<ArchitecturalDiagram>.Success(matches[0])
            : Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.snapshot.not_found", "The saved diagram snapshot was not found uniquely."));
    }

    public async Task<Result<ArchitecturalDiagramLayout>> SaveLayoutAsync(
        ArchitecturalDiagram diagram,
        ArchitecturalDiagramLayout layout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(layout);
        var elementMap = diagram.Elements.ToDictionary(element => element.Id, StringComparer.Ordinal);
        var elementIds = elementMap.Keys.ToHashSet(StringComparer.Ordinal);
        var positions = layout.Positions ?? Array.Empty<DiagramElementPosition>();
        var collapsedIds = layout.CollapsedBoundaryIds ?? Array.Empty<string>();
        if (layout.SchemaVersion != "1.0" ||
            layout.DiagramId != diagram.DiagramId ||
            layout.SourceFingerprint != diagram.SourceFingerprint ||
            positions.Any(position => !elementIds.Contains(position.ElementId)) ||
            positions.Select(position => position.ElementId).Distinct(StringComparer.Ordinal).Count() != positions.Count ||
            collapsedIds.Any(id => !elementIds.Contains(id) ||
                                   elementMap[id].ElementType is not ("SoftwareSystem" or "Container")) ||
            collapsedIds.Distinct(StringComparer.Ordinal).Count() != collapsedIds.Count ||
            positions.Any(position => position is null || string.IsNullOrWhiteSpace(position.ElementId)))
        {
            return Result<ArchitecturalDiagramLayout>.Failure(
                new DomainError("diagram.layout.invalid", "Layout overrides must reference unique elements in this diagram."));
        }

        var path = GetLayoutPath(diagram);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var normalized = layout with
            {
                Positions = positions.OrderBy(position => position.ElementId, StringComparer.Ordinal).ToArray(),
                CollapsedBoundaryIds = collapsedIds.Order(StringComparer.Ordinal).ToArray()
            };
            var json = JsonSerializer.Serialize(normalized, SerializerOptions);
            var validation = ValidateJson(json, _layoutSchema, Path.GetFileName(path), "diagram.layout.schema.invalid");
            if (!validation.IsSuccess)
            {
                return Result<ArchitecturalDiagramLayout>.Failure(validation.Errors);
            }

            await WriteAtomicallyAsync(path, json, cancellationToken);
            return Result<ArchitecturalDiagramLayout>.Success(normalized);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to save layout for diagram {DiagramId}.", diagram.DiagramId);
            return Result<ArchitecturalDiagramLayout>.Failure(
                new DomainError("diagram.layout.write_failed", "The diagram layout could not be saved."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to layout for diagram {DiagramId} was denied.", diagram.DiagramId);
            return Result<ArchitecturalDiagramLayout>.Failure(
                new DomainError("diagram.layout.access_denied", "The application does not have access to save this layout."));
        }
    }

    public async Task<Result<DiagramLayoutLoadResult>> LoadLayoutAsync(
        ArchitecturalDiagram diagram,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        var path = GetLayoutPath(diagram);
        try
        {
            if (!File.Exists(path))
            {
                return Result<DiagramLayoutLoadResult>.Success(new DiagramLayoutLoadResult(
                    new ArchitecturalDiagramLayout(
                        "1.0",
                        diagram.DiagramId,
                        diagram.SourceFingerprint,
                        Array.Empty<DiagramElementPosition>(),
                        Array.Empty<string>()),
                    Array.Empty<string>()));
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var validation = ValidateJson(json, _layoutSchema, Path.GetFileName(path), "diagram.layout.schema.invalid");
            if (!validation.IsSuccess)
            {
                return Result<DiagramLayoutLoadResult>.Failure(validation.Errors);
            }

            var layout = JsonSerializer.Deserialize<ArchitecturalDiagramLayout>(json, SerializerOptions);
            if (layout is null ||
                layout.SchemaVersion != "1.0" ||
                layout.DiagramId != diagram.DiagramId ||
                !Regex.IsMatch(layout.SourceFingerprint, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant) ||
                layout.Positions is null ||
                layout.CollapsedBoundaryIds is null ||
                layout.Positions.Any(position => position is null || string.IsNullOrWhiteSpace(position.ElementId)) ||
                layout.Positions.Select(position => position.ElementId).Distinct(StringComparer.Ordinal).Count() != layout.Positions.Count ||
                layout.CollapsedBoundaryIds.Distinct(StringComparer.Ordinal).Count() != layout.CollapsedBoundaryIds.Count)
            {
                return Result<DiagramLayoutLoadResult>.Failure(
                    new DomainError("diagram.layout.invalid", "The saved diagram layout is invalid."));
            }

            var elementMap = diagram.Elements.ToDictionary(element => element.Id, StringComparer.Ordinal);
            var validIds = elementMap.Keys.ToHashSet(StringComparer.Ordinal);
            var remainingPositions = layout.Positions.Where(position => validIds.Contains(position.ElementId))
                .OrderBy(position => position.ElementId, StringComparer.Ordinal).ToArray();
            var remainingCollapsed = layout.CollapsedBoundaryIds.Where(id =>
                    validIds.Contains(id) && elementMap[id].ElementType is "SoftwareSystem" or "Container")
                .Order(StringComparer.Ordinal).ToArray();
            var orphanedIds = layout.Positions.Select(position => position.ElementId)
                .Concat(layout.CollapsedBoundaryIds)
                .Where(id => !validIds.Contains(id) ||
                             layout.CollapsedBoundaryIds.Contains(id) &&
                             elementMap.TryGetValue(id, out var element) &&
                             element.ElementType is not ("SoftwareSystem" or "Container"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var refreshed = layout with
            {
                SourceFingerprint = diagram.SourceFingerprint,
                Positions = remainingPositions,
                CollapsedBoundaryIds = remainingCollapsed
            };
            return Result<DiagramLayoutLoadResult>.Success(new DiagramLayoutLoadResult(refreshed, orphanedIds));
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Layout file for diagram {DiagramId} contains invalid JSON.", diagram.DiagramId);
            return Result<DiagramLayoutLoadResult>.Failure(
                new DomainError("diagram.layout.invalid_json", "The saved diagram layout contains invalid JSON."));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read layout for diagram {DiagramId}.", diagram.DiagramId);
            return Result<DiagramLayoutLoadResult>.Failure(
                new DomainError("diagram.layout.read_failed", "The saved diagram layout could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to layout for diagram {DiagramId} was denied.", diagram.DiagramId);
            return Result<DiagramLayoutLoadResult>.Failure(
                new DomainError("diagram.layout.access_denied", "The application does not have access to read this layout."));
        }
    }

    private async Task<Result<ArchitecturalDiagram>> ReadExistingSnapshotAsync(
        string path,
        ArchitecturalDiagram expected,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var validation = ValidateDocument(json, Path.GetFileName(path));
        if (!validation.IsSuccess)
        {
            return Result<ArchitecturalDiagram>.Failure(validation.Errors);
        }

        var existing = JsonSerializer.Deserialize<ArchitecturalDiagram>(json, SerializerOptions);
        return existing is not null &&
               existing.DiagramId == expected.DiagramId &&
               existing.SourceFingerprint == expected.SourceFingerprint &&
               existing.ProjectRevision == expected.ProjectRevision
            ? Result<ArchitecturalDiagram>.Success(existing)
            : Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.snapshot.collision", "A different snapshot already exists for these diagram inputs."));
    }

    private string GetSnapshotPath(ArchitecturalDiagram diagram)
    {
        var projectDirectory = GetProjectDirectory(diagram.ProjectId);
        var scopeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(diagram.ScopeId))).ToLowerInvariant();
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(diagram.SourceFingerprint))).ToLowerInvariant();
        var fileName = $"{diagram.DiagramType}-{scopeHash}-{fingerprint}.json";
        var path = Path.GetFullPath(Path.Combine(projectDirectory, fileName));
        var prefix = projectDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? projectDirectory
            : projectDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The diagram snapshot path is outside the configured workspace.");
        }

        return path;
    }

    private string GetLayoutPath(ArchitecturalDiagram diagram)
    {
        var projectDirectory = GetProjectDirectory(diagram.ProjectId);
        var diagramHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(diagram.DiagramId)))
            .ToLowerInvariant();
        var path = Path.GetFullPath(Path.Combine(projectDirectory, "layouts", $"{diagramHash}.json"));
        var prefix = Path.GetFullPath(Path.Combine(projectDirectory, "layouts")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The diagram layout path is outside the configured workspace.");
        }

        return path;
    }

    private string GetProjectDirectory(Guid projectId) =>
        Path.Combine(_diagramsDirectory, projectId.ToString("N"));

    private static async Task WriteAtomicallyAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private Result<bool> ValidateDocument(string json, string fileName)
        => ValidateJson(json, _diagramSchema, fileName, "diagram.schema.invalid");

    private Result<bool> ValidateJson(
        string json,
        JsonSchema schema,
        string fileName,
        string errorCode)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Diagram file {DiagramFile} contains invalid JSON.", fileName);
            return Result<bool>.Failure(
                new DomainError("diagram.file.invalid_json", "The diagram snapshot contains invalid JSON."));
        }

        using (document)
        {
            var evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
            if (evaluation.IsValid)
            {
                return Result<bool>.Success(true);
            }

            var instancePath = evaluation.InstanceLocation.ToString();
            _logger.LogWarning(
                "Diagram file {DiagramFile} does not match the supported schema at {InstancePath}.",
                fileName,
                instancePath);
            return Result<bool>.Failure(
                new DomainError(
                    errorCode,
                    $"Diagram file '{fileName}' does not match its supported schema (location: {instancePath})."));
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
