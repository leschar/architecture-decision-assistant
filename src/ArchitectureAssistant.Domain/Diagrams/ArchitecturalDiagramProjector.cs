using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Diagrams;

public sealed class ArchitecturalDiagramProjector
{
    public const string CurrentVersion = "1.0.0";

    private static readonly JsonSerializerOptions FingerprintOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Result<ArchitecturalDiagram> Project(
        Project project,
        ArchitecturalDiagramType diagramType,
        string scopeId,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!Enum.IsDefined(diagramType))
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.type.invalid", "The requested diagram type is not supported."));
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.scope.required", "A structural scope ID is required."));
        }

        var scope = project.Structure.Elements.SingleOrDefault(element =>
            string.Equals(element.Id, scopeId, StringComparison.Ordinal));
        if (scope is null)
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.scope.not_found", "The requested structural scope does not exist."));
        }

        var expectedScopeType = diagramType == ArchitecturalDiagramType.Component
            ? StructuralElementType.Container
            : StructuralElementType.SoftwareSystem;
        if (scope.Type != expectedScopeType)
        {
            return Result<ArchitecturalDiagram>.Failure(
                new DomainError("diagram.scope.invalid", "The selected element is not a valid scope for this C4 diagram type."));
        }

        var selectedElements = SelectElements(project.Structure, diagramType, scope);
        var selectedIds = selectedElements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        var coreIds = selectedElements
            .Where(element => element.Id == scope.Id ||
                              diagramType == ArchitecturalDiagramType.Container &&
                              element.Type == StructuralElementType.Container &&
                              element.ParentId == scope.Id ||
                              diagramType == ArchitecturalDiagramType.Component &&
                              element.Type == StructuralElementType.Component &&
                              element.ParentId == scope.Id)
            .Select(element => element.Id)
            .ToHashSet(StringComparer.Ordinal);
        var selectedConnections = project.Structure.Connections
            .Where(connection => selectedIds.Contains(connection.SourceId) &&
                                 selectedIds.Contains(connection.TargetId) &&
                                 (coreIds.Contains(connection.SourceId) || coreIds.Contains(connection.TargetId)))
            .OrderBy(connection => connection.Id, StringComparer.Ordinal)
            .ToArray();
        var warnings = new List<string>();
        if (selectedElements.Any(element => element.Description is null && element.Responsibility is null))
        {
            warnings.Add("Há elementos sem descrição ou responsabilidade registradas.");
        }

        if (diagramType == ArchitecturalDiagramType.Container &&
            !selectedElements.Any(element => element.Type == StructuralElementType.Container))
        {
            warnings.Add("Nenhum container foi explicitamente modelado para este sistema.");
        }
        else if (diagramType == ArchitecturalDiagramType.Component &&
                 !selectedElements.Any(element => element.Type == StructuralElementType.Component))
        {
            warnings.Add("Nenhum componente foi explicitamente modelado para este container.");
        }
        else if (selectedConnections.Length == 0)
        {
            warnings.Add("Nenhuma interação foi explicitamente modelada para este escopo.");
        }

        var elements = selectedElements
            .OrderBy(element => element.Id, StringComparer.Ordinal)
            .Select(ToDiagramElement)
            .ToArray();
        var connections = selectedConnections.Select(ToDiagramConnection).ToArray();
        var fingerprint = ComputeFingerprint(project, diagramType, scopeId);
        var title = $"{diagramType}: {scope.Name}";

        return Result<ArchitecturalDiagram>.Success(new ArchitecturalDiagram(
            "1.0",
            $"{project.Id:N}:{diagramType}:{scopeId}",
            diagramType,
            project.Id,
            project.Revision.ToString(CultureInfo.InvariantCulture),
            fingerprint,
            CurrentVersion,
            generatedAt,
            scopeId,
            title,
            warnings.Count == 0 ? ArchitecturalDiagramStatus.Complete : ArchitecturalDiagramStatus.Incomplete,
            elements,
            connections,
            warnings));
    }

    private static StructuralElement[] SelectElements(
        ArchitecturalStructure structure,
        ArchitecturalDiagramType diagramType,
        StructuralElement scope)
    {
        var selectedIds = new HashSet<string>(StringComparer.Ordinal) { scope.Id };

        if (diagramType == ArchitecturalDiagramType.Context)
        {
            AddConnectedContextElements(structure, selectedIds, [scope.Id]);
        }
        else if (diagramType == ArchitecturalDiagramType.Container)
        {
            var containerIds = structure.Elements
                .Where(element => element.Type == StructuralElementType.Container && element.ParentId == scope.Id)
                .Select(element => element.Id)
                .ToArray();
            selectedIds.UnionWith(containerIds);
            AddConnectedContextElements(structure, selectedIds, containerIds.Append(scope.Id));
        }
        else
        {
            var parentSystemId = scope.ParentId;
            if (parentSystemId is not null)
            {
                selectedIds.Add(parentSystemId);
            }

            var componentIds = structure.Elements
                .Where(element => element.Type == StructuralElementType.Component && element.ParentId == scope.Id)
                .Select(element => element.Id)
                .ToArray();
            selectedIds.UnionWith(componentIds);
            AddConnectedContextElements(structure, selectedIds, componentIds.Append(scope.Id));
        }

        return structure.Elements.Where(element => selectedIds.Contains(element.Id)).ToArray();
    }

    private static void AddConnectedContextElements(
        ArchitecturalStructure structure,
        HashSet<string> selectedIds,
        IEnumerable<string> scopedIds)
    {
        var scoped = scopedIds.ToHashSet(StringComparer.Ordinal);
        foreach (var connection in structure.Connections)
        {
            if (scoped.Contains(connection.SourceId) && IsContextElement(structure, connection.TargetId))
            {
                selectedIds.Add(connection.TargetId);
            }
            else if (scoped.Contains(connection.TargetId) && IsContextElement(structure, connection.SourceId))
            {
                selectedIds.Add(connection.SourceId);
            }
        }
    }

    private static bool IsContextElement(ArchitecturalStructure structure, string elementId) =>
        structure.Elements.Any(element =>
            element.Id == elementId &&
            element.Type is StructuralElementType.Person or StructuralElementType.SoftwareSystem);

    private static DiagramElement ToDiagramElement(StructuralElement element) =>
        new(
            element.Id,
            element.Type.ToString(),
            element.Name,
            element.Description ?? element.Responsibility ?? "Descrição não informada.",
            element.Technology,
            element.ParentId,
            element.Status.ToString(),
            [ToSourceReference(element.Provenance, element.Id)]);

    private static DiagramConnection ToDiagramConnection(StructuralConnection connection) =>
        new(
            connection.Id,
            connection.SourceId,
            connection.TargetId,
            connection.Description,
            connection.InteractionType.ToString(),
            connection.Protocol,
            connection.MessageName,
            connection.Status.ToString(),
            [ToSourceReference(connection.Provenance, connection.Id)]);

    private static DiagramSourceReference ToSourceReference(StructuralProvenance provenance, string fallbackId) =>
        new(
            provenance.SourceKind switch
            {
                StructuralSourceKind.Manual => "ManualInput",
                StructuralSourceKind.Requirement => "Requirement",
                StructuralSourceKind.Decision => "Decision",
                StructuralSourceKind.KnowledgeItem => "KnowledgeItem",
                StructuralSourceKind.Blueprint => "StructuralModel",
                _ => "ManualInput"
            },
            provenance.SourceId ?? fallbackId,
            provenance.SourceVersion);

    private static string ComputeFingerprint(
        Project project,
        ArchitecturalDiagramType diagramType,
        string scopeId)
    {
        var source = new
        {
            diagramType,
            scopeId,
            project.Id,
            project.Revision,
            generatorVersion = CurrentVersion,
            elements = project.Structure.Elements.OrderBy(element => element.Id, StringComparer.Ordinal),
            connections = project.Structure.Connections.OrderBy(connection => connection.Id, StringComparer.Ordinal)
        };
        var content = JsonSerializer.SerializeToUtf8Bytes(source, FingerprintOptions);
        return $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";
    }
}
