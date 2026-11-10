using ArchitectureAssistant.Domain.Common;
using System.Text.RegularExpressions;

namespace ArchitectureAssistant.Domain.Projects;

public sealed record StructuralElement(
    string Id,
    string Name,
    StructuralElementType Type,
    string? ParentId,
    string? Description,
    string? Responsibility,
    string? Technology,
    StructuralElementStatus Status,
    StructuralProvenance Provenance);

public sealed record StructuralConnection(
    string Id,
    string SourceId,
    string TargetId,
    string Description,
    StructuralInteractionType InteractionType,
    string? Protocol,
    string? MessageName,
    StructuralElementStatus Status,
    StructuralProvenance Provenance);

public sealed record StructuralProvenance(
    StructuralSourceKind SourceKind,
    string? SourceId,
    string? SourceVersion);

public sealed class ArchitecturalStructure
{
    private static readonly Regex StableIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._:-]*$", RegexOptions.CultureInvariant);
    private ArchitecturalStructure(
        IReadOnlyList<StructuralElement> elements,
        IReadOnlyList<StructuralConnection> connections)
    {
        Elements = elements;
        Connections = connections;
    }

    public static ArchitecturalStructure Empty { get; } =
        new(Array.Empty<StructuralElement>(), Array.Empty<StructuralConnection>());

    public IReadOnlyList<StructuralElement> Elements { get; }

    public IReadOnlyList<StructuralConnection> Connections { get; }

    public static Result<ArchitecturalStructure> Create(
        IEnumerable<StructuralElement> elements,
        IEnumerable<StructuralConnection> connections)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(connections);

        var elementList = elements.ToArray();
        var connectionList = connections.ToArray();
        var errors = new List<DomainError>();
        var elementsById = new Dictionary<string, StructuralElement>(StringComparer.Ordinal);

        foreach (var element in elementList)
        {
            if (!IsValidId(element.Id) || !elementsById.TryAdd(element.Id, element))
            {
                errors.Add(new DomainError("structure.element.id.invalid", "Structural element IDs must be unique and contain 1 to 120 characters."));
            }

            if (!HasText(element.Name, 120) ||
                !Enum.IsDefined(element.Type) ||
                !Enum.IsDefined(element.Status) ||
                !IsValidProvenance(element.Provenance) ||
                !HasOptionalText(element.Description, 2000) ||
                !HasOptionalText(element.Responsibility, 1000) ||
                !HasOptionalText(element.Technology, 120))
            {
                errors.Add(new DomainError("structure.element.invalid", "A structural element contains an invalid name, type, status, description, or provenance."));
            }
        }

        foreach (var element in elementList)
        {
            if (element.ParentId is null)
            {
                if (element.Type is StructuralElementType.Container or StructuralElementType.Component)
                {
                    errors.Add(new DomainError("structure.element.parent.required", "Containers and components must belong to a parent element."));
                }

                continue;
            }

            if (!elementsById.TryGetValue(element.ParentId, out var parent))
            {
                errors.Add(new DomainError("structure.element.parent.not_found", "A structural element refers to a parent that does not exist."));
                continue;
            }

            var validParent = element.Type switch
            {
                StructuralElementType.Container => parent.Type == StructuralElementType.SoftwareSystem,
                StructuralElementType.Component => parent.Type == StructuralElementType.Container,
                _ => false
            };
            if (!validParent)
            {
                errors.Add(new DomainError("structure.element.parent.invalid", "A container must belong to a software system and a component must belong to a container."));
            }
        }

        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in connectionList)
        {
            if (!IsValidId(connection.Id) ||
                !connectionIds.Add(connection.Id) ||
                elementsById.ContainsKey(connection.Id) ||
                !IsValidId(connection.SourceId) ||
                !IsValidId(connection.TargetId) ||
                string.Equals(connection.SourceId, connection.TargetId, StringComparison.Ordinal) ||
                !HasText(connection.Description, 2000) ||
                !Enum.IsDefined(connection.InteractionType) ||
                !Enum.IsDefined(connection.Status) ||
                !IsValidProvenance(connection.Provenance) ||
                !HasOptionalText(connection.Protocol, 120) ||
                !HasOptionalText(connection.MessageName, 200))
            {
                errors.Add(new DomainError("structure.connection.invalid", "A structural connection contains invalid identity, endpoints, description, interaction, status, or provenance."));
                continue;
            }

            if (!elementsById.ContainsKey(connection.SourceId) || !elementsById.ContainsKey(connection.TargetId))
            {
                errors.Add(new DomainError("structure.connection.endpoint.not_found", "A structural connection endpoint does not exist."));
            }

            if (connection.InteractionType == StructuralInteractionType.AsynchronousEvent &&
                string.IsNullOrWhiteSpace(connection.MessageName))
            {
                errors.Add(new DomainError("structure.connection.event_name.required", "Asynchronous event connections require an event name."));
            }
            else if (connection.InteractionType != StructuralInteractionType.AsynchronousEvent &&
                     connection.MessageName is not null)
            {
                errors.Add(new DomainError("structure.connection.event_name.invalid", "Only asynchronous event connections can have an event name."));
            }
        }

        return errors.Count == 0
            ? Result<ArchitecturalStructure>.Success(new ArchitecturalStructure(elementList, connectionList))
            : Result<ArchitecturalStructure>.Failure(errors);
    }

    private static bool IsValidProvenance(StructuralProvenance provenance) =>
        provenance is not null &&
        Enum.IsDefined(provenance.SourceKind) &&
        (provenance.SourceKind == StructuralSourceKind.Manual
            ? provenance.SourceId is null && provenance.SourceVersion is null
            : HasText(provenance.SourceId, 120) && HasOptionalText(provenance.SourceVersion, 100));

    private static bool HasText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;

    private static bool IsValidId(string? value) =>
        value is not null && HasText(value, 120) && StableIdPattern.IsMatch(value);

    private static bool HasOptionalText(string? value, int maximumLength) =>
        value is null || value.Trim().Length > 0 && value.Trim().Length <= maximumLength;
}

public enum StructuralElementType
{
    Person,
    SoftwareSystem,
    Container,
    Component
}

public enum StructuralElementStatus
{
    Confirmed,
    Proposed,
    Unresolved
}

public enum StructuralInteractionType
{
    Synchronous,
    AsynchronousCommand,
    AsynchronousEvent,
    BatchTransfer,
    Unknown
}

public enum StructuralSourceKind
{
    Manual,
    Requirement,
    Decision,
    KnowledgeItem,
    Blueprint
}
