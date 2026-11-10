using System.Text.Json;
using System.Text.Json.Serialization;
using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

public sealed class JsonProjectRepository : IProjectRepository
{
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(50);
    private const int LockRetryCount = 20;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _projectsDirectory;
    private readonly JsonSchema _projectSchema;
    private readonly ILogger<JsonProjectRepository> _logger;

    public JsonProjectRepository(string workspaceRoot, ILogger<JsonProjectRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(logger);

        var workspace = Path.GetFullPath(workspaceRoot);
        _projectsDirectory = Path.Combine(workspace, "projects");
        _logger = logger;

        using var schemaStream = typeof(JsonProjectRepository).Assembly.GetManifestResourceStream(
            "ArchitectureAssistant.Infrastructure.Schemas.project.schema.json");
        if (schemaStream is null)
        {
            throw new InvalidOperationException("The embedded project JSON schema is missing.");
        }

        using var reader = new StreamReader(schemaStream);
        _projectSchema = JsonSchema.FromText(reader.ReadToEnd());
    }

    public async Task<Result<IReadOnlyList<Project>>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_projectsDirectory);
            var projects = new List<Project>();

            foreach (var filePath in Directory.EnumerateFiles(_projectsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ReadProjectAsync(filePath, cancellationToken);
                if (!result.TryGetValue(out var project))
                {
                    return Result<IReadOnlyList<Project>>.Failure(result.Errors);
                }

                projects.Add(project);
            }

            return Result<IReadOnlyList<Project>>.Success(
                projects.OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase).ToArray());
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to enumerate project files in the configured workspace.");
            return Result<IReadOnlyList<Project>>.Failure(
                new DomainError("project.workspace.read_failed", "The local project workspace could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to the configured project workspace was denied.");
            return Result<IReadOnlyList<Project>>.Failure(
                new DomainError("project.workspace.access_denied", "The application does not have access to the project workspace."));
        }
    }

    public async Task<Result<Project>> LoadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return Result<Project>.Failure(
                new DomainError("project.id.invalid", "Project ID must not be empty."));
        }

        try
        {
            return await ReadProjectAsync(GetProjectPath(projectId), cancellationToken);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read project {ProjectId}.", projectId);
            return Result<Project>.Failure(
                new DomainError("project.file.read_failed", "The project file could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to project {ProjectId} was denied.", projectId);
            return Result<Project>.Failure(
                new DomainError("project.file.access_denied", "The application does not have access to this project file."));
        }
    }

    public async Task<Result<Project>> SaveAsync(
        Project project,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        try
        {
            Directory.CreateDirectory(_projectsDirectory);
            if (expectedRevision is null && project.Revision != 1)
            {
                return Result<Project>.Failure(
                    new DomainError("project.revision.invalid", "A new project must start at revision 1."));
            }

            var projectPath = GetProjectPath(project.Id);
            var lockResult = await AcquireWriteLockAsync(project.Id, cancellationToken);
            if (!lockResult.TryGetValue(out var writeLock))
            {
                return Result<Project>.Failure(lockResult.Errors);
            }

            await using (writeLock)
            {
                var current = await CheckExpectedRevisionAsync(projectPath, project, expectedRevision, cancellationToken);
                if (!current.IsSuccess)
                {
                    return Result<Project>.Failure(current.Errors);
                }

                var document = ProjectDocumentMapper.FromDomain(project);
                var json = JsonSerializer.Serialize(document, SerializerOptions);
                var validation = ValidateDocument(json, projectPath);
                if (!validation.IsSuccess)
                {
                    return Result<Project>.Failure(validation.Errors);
                }

                var temporaryPath = projectPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                    File.Move(temporaryPath, projectPath, overwrite: true);
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
                            _logger.LogWarning(cleanupException, "Failed to remove a temporary project file for {ProjectId}.", project.Id);
                        }
                        catch (UnauthorizedAccessException cleanupException)
                        {
                            _logger.LogWarning(cleanupException, "Access was denied while removing a temporary project file for {ProjectId}.", project.Id);
                        }
                    }
                }
            }

            return Result<Project>.Success(project);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to save project {ProjectId}.", project.Id);
            return Result<Project>.Failure(
                new DomainError("project.file.write_failed", "The project could not be saved. Check the workspace and try again."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to the project workspace was denied while saving {ProjectId}.", project.Id);
            return Result<Project>.Failure(
                new DomainError("project.file.access_denied", "The application does not have permission to save this project."));
        }
    }

    private async Task<Result<Project>> ReadProjectAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result<Project>.Failure(
                new DomainError("project.not_found", "The requested project does not exist in the configured workspace."));
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var validation = ValidateDocument(json, path);
        if (!validation.IsSuccess)
        {
            return Result<Project>.Failure(validation.Errors);
        }

        ProjectDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ProjectDocument>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Project file {ProjectPath} contains invalid JSON.", Path.GetFileName(path));
            return Result<Project>.Failure(
                new DomainError("project.file.invalid_json", "The project file contains invalid JSON."));
        }

        if (document is null || !string.Equals(document.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            return Result<Project>.Failure(
                new DomainError("project.schema.unsupported", "The project file is empty or uses an unsupported schema version."));
        }

        return ProjectDocumentMapper.ToDomain(document);
    }

    private Result<bool> ValidateDocument(string json, string path)
    {
        JsonDocument instance;
        try
        {
            instance = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Project file {ProjectPath} contains invalid JSON.", Path.GetFileName(path));
            return Result<bool>.Failure(
                new DomainError("project.file.invalid_json", "The project file contains invalid JSON."));
        }

        using (instance)
        {
            var evaluation = _projectSchema.Evaluate(instance.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
            if (evaluation.IsValid)
            {
                return Result<bool>.Success(true);
            }

            var instancePath = evaluation.InstanceLocation.ToString();
            _logger.LogWarning(
                "Project file {ProjectPath} does not match schema version 1.0 at {InstancePath}.",
                Path.GetFileName(path),
                instancePath);

            return Result<bool>.Failure(
                new DomainError(
                    "project.schema.invalid",
                    $"The project JSON does not match schema version 1.0 (location: {instancePath})."));
        }
    }

    private async Task<Result<bool>> CheckExpectedRevisionAsync(
        string projectPath,
        Project project,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(projectPath))
        {
            return expectedRevision is null
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(new DomainError(
                    "project.conflict.deleted",
                    "The project was removed after it was opened. Reload the workspace before saving."));
        }

        if (expectedRevision is null)
        {
            return Result<bool>.Failure(
                new DomainError("project.conflict.exists", "A project with this ID already exists."));
        }

        var existing = await ReadProjectAsync(projectPath, cancellationToken);
        if (!existing.TryGetValue(out var storedProject))
        {
            return Result<bool>.Failure(existing.Errors);
        }

        if (storedProject.Revision != expectedRevision.Value)
        {
            return Result<bool>.Failure(
                new DomainError("project.conflict.revision", "The project changed on disk. Reload it before saving your changes."));
        }

        if (project.Revision != expectedRevision.Value + 1)
        {
            return Result<bool>.Failure(
                new DomainError("project.revision.invalid", "The project revision is not the next expected revision."));
        }

        return Result<bool>.Success(true);
    }

    private async Task<Result<FileStream>> AcquireWriteLockAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var lockPath = GetProjectPath(projectId) + ".lock";
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
                _logger.LogWarning(exception, "Could not acquire the write lock for project {ProjectId}.", projectId);
                return Result<FileStream>.Failure(
                    new DomainError("project.conflict.locked", "The project is being edited by another process. Try again."));
            }
        }

        return Result<FileStream>.Failure(
            new DomainError("project.conflict.locked", "The project is being edited by another process. Try again."));
    }

    private string GetProjectPath(Guid projectId)
    {
        var path = Path.GetFullPath(Path.Combine(_projectsDirectory, $"{projectId:N}.json"));
        var prefix = _projectsDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _projectsDirectory
            : _projectsDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The project path is outside the configured workspace.");
        }

        return path;
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

internal static class ProjectDocumentMapper
{
    public static ProjectDocument FromDomain(Project project) =>
        new()
        {
            Id = project.Id,
            Revision = project.Revision,
            Name = project.Name,
            Purpose = project.Purpose,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            Context = new ProjectContextDocument
            {
                BusinessDomain = project.Context.BusinessDomain,
                TeamSize = project.Context.TeamSize,
                Criticality = project.Context.Criticality,
                ScalabilityTarget = project.Context.ScalabilityTarget,
                Constraints = project.Context.Constraints
            },
            Requirements = project.Requirements.Select(requirement => new RequirementDocument
            {
                Id = requirement.Id,
                Description = requirement.Description,
                Classification = requirement.Classification,
                Priority = requirement.Priority,
                Status = requirement.Status,
                Origin = requirement.Origin,
                CreatedAt = requirement.CreatedAt,
                UpdatedAt = requirement.UpdatedAt
            }).ToArray(),
            Decisions = project.Decisions.Select(decision => new DecisionDocument
            {
                Id = decision.Id,
                SelectedAlternativeId = decision.SelectedAlternativeId,
                SelectedAlternativeKind = decision.SelectedAlternativeKind,
                ConsideredAlternatives = decision.ConsideredAlternatives,
                Status = decision.Status,
                Justification = decision.Justification,
                Consequences = decision.Consequences,
                RecordedBy = decision.RecordedBy,
                RecordedAt = decision.RecordedAt,
                Revision = decision.Revision,
                AnalysisId = decision.AnalysisId,
                SelectedAlternativeVersion = decision.SelectedAlternativeVersion,
                StatusChangedBy = decision.StatusChangedBy,
                StatusChangedAt = decision.StatusChangedAt,
                SupersededByDecisionId = decision.SupersededByDecisionId,
                SupersedesDecisionId = decision.SupersedesDecisionId,
                StatusHistory = decision.StatusHistory,
                HistoricalSource = decision.HistoricalSource
            }).ToArray(),
            Structure = new ProjectStructureDocument
            {
                Elements = project.Structure.Elements.Select(element => new StructuralElementDocument
                {
                    Id = element.Id,
                    Name = element.Name,
                    Type = element.Type,
                    ParentId = element.ParentId,
                    Description = element.Description,
                    Responsibility = element.Responsibility,
                    Technology = element.Technology,
                    Status = element.Status,
                    Provenance = new StructuralProvenanceDocument
                    {
                        SourceKind = element.Provenance.SourceKind,
                        SourceId = element.Provenance.SourceId,
                        SourceVersion = element.Provenance.SourceVersion
                    }
                }).ToArray(),
                Connections = project.Structure.Connections.Select(connection => new StructuralConnectionDocument
                {
                    Id = connection.Id,
                    SourceId = connection.SourceId,
                    TargetId = connection.TargetId,
                    Description = connection.Description,
                    InteractionType = connection.InteractionType,
                    Protocol = connection.Protocol,
                    MessageName = connection.MessageName,
                    Status = connection.Status,
                    Provenance = new StructuralProvenanceDocument
                    {
                        SourceKind = connection.Provenance.SourceKind,
                        SourceId = connection.Provenance.SourceId,
                        SourceVersion = connection.Provenance.SourceVersion
                    }
                }).ToArray()
            }
        };

    public static Result<Project> ToDomain(ProjectDocument document)
    {
        if (!string.Equals(document.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            return Result<Project>.Failure(
                new DomainError("project.schema.unsupported", "The project file uses an unsupported schema version."));
        }

        var context = ProjectContext.Empty.Update(
            document.Context.BusinessDomain,
            document.Context.TeamSize,
            document.Context.Criticality,
            document.Context.Constraints,
            document.Context.ScalabilityTarget);
        if (!context.TryGetValue(out var restoredContext))
        {
            return Result<Project>.Failure(context.Errors);
        }

        var requirements = new List<Requirement>();
        foreach (var requirement in document.Requirements)
        {
            if (!string.Equals(requirement.Origin, "UserProvided", StringComparison.Ordinal))
            {
                return Result<Project>.Failure(
                    new DomainError("requirement.origin.unsupported", "The project contains a requirement with an unsupported origin."));
            }

            var restoredRequirement = Requirement.Restore(
                requirement.Id,
                requirement.Description,
                requirement.Classification,
                requirement.Priority,
                requirement.Status,
                requirement.CreatedAt,
                requirement.UpdatedAt);
            if (!restoredRequirement.TryGetValue(out var value))
            {
                return Result<Project>.Failure(restoredRequirement.Errors);
            }

            requirements.Add(value);
        }

        var decisions = new List<ArchitecturalDecision>();
        foreach (var decision in document.Decisions)
        {
            var restoredDecision = ArchitecturalDecision.Restore(
                decision.Id,
                decision.SelectedAlternativeId,
                decision.SelectedAlternativeKind,
                decision.ConsideredAlternatives,
                decision.Status,
                decision.Justification,
                decision.Consequences,
                decision.RecordedBy,
                decision.RecordedAt,
                decision.Revision,
                decision.AnalysisId,
                decision.SelectedAlternativeVersion,
                decision.StatusChangedBy,
                decision.StatusChangedAt,
                decision.SupersededByDecisionId,
                decision.SupersedesDecisionId,
                decision.StatusHistory,
                decision.HistoricalSource);
            if (!restoredDecision.TryGetValue(out var value))
            {
                return Result<Project>.Failure(restoredDecision.Errors);
            }

            decisions.Add(value);
        }

        var structure = ArchitecturalStructure.Create(
            document.Structure.Elements.Select(element => new StructuralElement(
                element.Id,
                element.Name,
                element.Type,
                element.ParentId,
                element.Description,
                element.Responsibility,
                element.Technology,
                element.Status,
                new StructuralProvenance(element.Provenance.SourceKind, element.Provenance.SourceId, element.Provenance.SourceVersion))),
            document.Structure.Connections.Select(connection => new StructuralConnection(
                connection.Id,
                connection.SourceId,
                connection.TargetId,
                connection.Description,
                connection.InteractionType,
                connection.Protocol,
                connection.MessageName,
                connection.Status,
                new StructuralProvenance(connection.Provenance.SourceKind, connection.Provenance.SourceId, connection.Provenance.SourceVersion))));
        if (!structure.TryGetValue(out var restoredStructure))
        {
            return Result<Project>.Failure(structure.Errors);
        }

        return Project.Restore(
            document.Id,
            document.Revision,
            document.Name,
            document.Purpose,
            document.CreatedAt,
            document.UpdatedAt,
            restoredContext,
            requirements,
            decisions,
            restoredStructure);
    }
}
