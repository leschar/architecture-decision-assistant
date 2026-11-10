using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Domain.Projects;

public sealed record ProjectContext(
    string? BusinessDomain,
    int? TeamSize,
    ProjectCriticality? Criticality,
    IReadOnlyList<string> Constraints,
    ScalabilityTarget? ScalabilityTarget)
{
    public static ProjectContext Empty { get; } =
        new(null, null, null, Array.Empty<string>(), null);

    public Result<ProjectContext> Update(
        string? businessDomain,
        int? teamSize,
        ProjectCriticality? criticality,
        IEnumerable<string> constraints,
        ScalabilityTarget? scalabilityTarget = null)
    {
        if (teamSize is <= 0)
        {
            return Result<ProjectContext>.Failure(
                new DomainError("project.context.team_size.invalid", "Team size must be greater than zero."));
        }
        if (criticality.HasValue && !Enum.IsDefined(criticality.Value) ||
            scalabilityTarget.HasValue && !Enum.IsDefined(scalabilityTarget.Value))
        {
            return Result<ProjectContext>.Failure(
                new DomainError("project.context.enum.invalid", "Project context contains an unsupported classification."));
        }

        var normalizedDomain = string.IsNullOrWhiteSpace(businessDomain)
            ? null
            : businessDomain.Trim();
        if (normalizedDomain?.Length > 120)
        {
            return Result<ProjectContext>.Failure(
                new DomainError("project.context.domain.too_long", "Business domain cannot exceed 120 characters."));
        }

        var normalizedConstraints = constraints
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

        if (normalizedConstraints.Any(value => value.Length > 500))
        {
            return Result<ProjectContext>.Failure(
                new DomainError("project.context.constraint.too_long", "A constraint cannot exceed 500 characters."));
        }

        return Result<ProjectContext>.Success(
            new ProjectContext(normalizedDomain, teamSize, criticality, normalizedConstraints, scalabilityTarget));
    }
}

public enum ScalabilityTarget
{
    Low,
    Medium,
    High
}

public enum ProjectCriticality
{
    Low,
    Medium,
    High,
    MissionCritical
}
