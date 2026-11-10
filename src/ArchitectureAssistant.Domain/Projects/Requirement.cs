using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Domain.Projects;

public sealed record Requirement
{
    private Requirement(
        Guid id,
        string description,
        RequirementClassification classification,
        RequirementPriority? priority,
        RequirementStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Description = description;
        Classification = classification;
        Priority = priority;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Description { get; }

    public RequirementClassification Classification { get; }

    public RequirementPriority? Priority { get; }

    public RequirementStatus Status { get; }

    public string Origin => "UserProvided";

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private init; }

    public static Result<Requirement> Create(
        Guid id,
        string description,
        RequirementClassification classification,
        RequirementPriority? priority,
        RequirementStatus status,
        DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            return Result<Requirement>.Failure(
                new DomainError("requirement.id.invalid", "Requirement ID must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Result<Requirement>.Failure(
                new DomainError("requirement.description.required", "Requirement description is required."));
        }

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > 2000)
        {
            return Result<Requirement>.Failure(
                new DomainError("requirement.description.too_long", "Requirement description cannot exceed 2000 characters."));
        }

        if (!Enum.IsDefined(classification) || !Enum.IsDefined(status) ||
            (priority.HasValue && !Enum.IsDefined(priority.Value)))
        {
            return Result<Requirement>.Failure(
                new DomainError("requirement.values.invalid", "Requirement classification, status, or priority is invalid."));
        }

        return Result<Requirement>.Success(new Requirement(
            id,
            normalizedDescription,
            classification,
            priority,
            status,
            now,
            now));
    }

    public static Result<Requirement> Restore(
        Guid id,
        string description,
        RequirementClassification classification,
        RequirementPriority? priority,
        RequirementStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var result = Create(id, description, classification, priority, status, createdAt);
        if (!result.TryGetValue(out var requirement))
        {
            return result;
        }

        return updatedAt < createdAt
            ? Result<Requirement>.Failure(
                new DomainError("requirement.timestamp.invalid", "Requirement update time cannot be earlier than its creation time."))
            : Result<Requirement>.Success(requirement with { UpdatedAt = updatedAt });
    }
}

public enum RequirementClassification
{
    Functional,
    Quality,
    Constraint
}

public enum RequirementPriority
{
    Must,
    Should,
    Could
}

public enum RequirementStatus
{
    Confirmed,
    Provisional
}
