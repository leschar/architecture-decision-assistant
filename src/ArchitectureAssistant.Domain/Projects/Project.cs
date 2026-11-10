using ArchitectureAssistant.Domain.Common;

namespace ArchitectureAssistant.Domain.Projects;

public sealed class Project
{
    private Project(
        Guid id,
        long revision,
        string name,
        string purpose,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        ProjectContext context,
        IReadOnlyList<Requirement> requirements,
        IReadOnlyList<ArchitecturalDecision> decisions,
        ArchitecturalStructure structure)
    {
        Id = id;
        Revision = revision;
        Name = name;
        Purpose = purpose;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Context = context;
        Requirements = requirements;
        Decisions = decisions;
        Structure = structure;
    }

    public Guid Id { get; }

    public long Revision { get; }

    public string Name { get; }

    public string Purpose { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public ProjectContext Context { get; }

    public IReadOnlyList<Requirement> Requirements { get; }

    public IReadOnlyList<ArchitecturalDecision> Decisions { get; }

    public ArchitecturalStructure Structure { get; }

    public static Result<Project> Create(
        Guid id,
        string name,
        string purpose,
        DateTimeOffset now)
    {
        var errors = new List<DomainError>();
        if (id == Guid.Empty)
        {
            errors.Add(new DomainError("project.id.invalid", "Project ID must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new DomainError("project.name.required", "Project name is required."));
        }
        else if (name.Trim().Length > 120)
        {
            errors.Add(new DomainError("project.name.too_long", "Project name cannot exceed 120 characters."));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            errors.Add(new DomainError("project.purpose.required", "Project purpose is required."));
        }
        else if (purpose.Trim().Length > 2000)
        {
            errors.Add(new DomainError("project.purpose.too_long", "Project purpose cannot exceed 2000 characters."));
        }

        if (errors.Count > 0)
        {
            return Result<Project>.Failure(errors);
        }

        return Result<Project>.Success(new Project(
            id,
            1,
            name.Trim(),
            purpose.Trim(),
            now,
            now,
            ProjectContext.Empty,
            Array.Empty<Requirement>(),
            Array.Empty<ArchitecturalDecision>(),
            ArchitecturalStructure.Empty));
    }

    public static Result<Project> Restore(
        Guid id,
        long revision,
        string name,
        string purpose,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        ProjectContext context,
        IReadOnlyList<Requirement> requirements,
        IReadOnlyList<ArchitecturalDecision>? decisions = null,
        ArchitecturalStructure? structure = null)
    {
        var project = Create(id, name, purpose, createdAt);
        if (!project.TryGetValue(out var createdProject))
        {
            return project;
        }

        if (revision < 1 || updatedAt < createdAt ||
            requirements.Select(requirement => requirement.Id).Distinct().Count() != requirements.Count ||
            (decisions is not null && decisions.Select(decision => decision.Id).Distinct().Count() != decisions.Count) ||
            !HasValidDecisionLinks(decisions ?? Array.Empty<ArchitecturalDecision>()) ||
            !HasValidStructureReferences(structure ?? ArchitecturalStructure.Empty, requirements, decisions ?? Array.Empty<ArchitecturalDecision>()))
        {
            return Result<Project>.Failure(
                new DomainError("project.document.invalid", "The project document contains an invalid revision, timestamp, or duplicate requirement ID."));
        }

        return Result<Project>.Success(new Project(
            id,
            revision,
            createdProject.Name,
            createdProject.Purpose,
            createdAt,
            updatedAt,
            context,
            requirements.ToArray(),
            decisions?.ToArray() ?? Array.Empty<ArchitecturalDecision>(),
            structure ?? ArchitecturalStructure.Empty));
    }

    public Result<Project> AddRequirement(Requirement requirement, DateTimeOffset now)
    {
        if (Requirements.Any(existing => existing.Id == requirement.Id))
        {
            return Result<Project>.Failure(
                new DomainError("requirement.id.duplicate", "A requirement with this ID already exists in the project."));
        }

        return Result<Project>.Success(new Project(
            Id,
            Revision + 1,
            Name,
            Purpose,
            CreatedAt,
            now,
            Context,
            Requirements.Append(requirement).ToArray(),
            Decisions,
            Structure));
    }

    public Result<Project> UpdateContext(ProjectContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Result<Project>.Success(new Project(
            Id,
            Revision + 1,
            Name,
            Purpose,
            CreatedAt,
            now,
            context,
            Requirements,
            Decisions,
            Structure));
    }

    public Result<Project> UpdateStructure(ArchitecturalStructure structure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(structure);
        if (!HasValidStructureReferences(structure, Requirements, Decisions))
        {
            return Result<Project>.Failure(
                new DomainError("structure.provenance.not_found", "Structural provenance must reference a requirement or decision in this project."));
        }

        return Result<Project>.Success(new Project(
            Id,
            Revision + 1,
            Name,
            Purpose,
            CreatedAt,
            now,
            Context,
            Requirements,
            Decisions,
            structure));
    }

    public Result<Project> AddDecision(ArchitecturalDecision decision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (Decisions.Any(existing => existing.Id == decision.Id))
        {
            return Result<Project>.Failure(
                new DomainError("decision.id.duplicate", "A decision with this ID already exists in the project."));
        }
        if (decision.SupersedesDecisionId is not null)
        {
            return Result<Project>.Failure(
                new DomainError("decision.predecessor.required", "A replacement decision must be added by revising its predecessor."));
        }

        return Result<Project>.Success(new Project(
            Id,
            Revision + 1,
            Name,
            Purpose,
            CreatedAt,
            now,
            Context,
            Requirements,
            Decisions.Append(decision).ToArray(),
            Structure));
    }

    public Result<Project> TransitionDecision(
        Guid decisionId,
        DecisionStatus status,
        string changedBy,
        DateTimeOffset changedAt)
    {
        if (status == DecisionStatus.Superseded)
        {
            return Result<Project>.Failure(
                new DomainError("decision.revision.required", "A superseding decision must be created with the revision operation."));
        }

        var matches = Decisions.Where(decision => decision.Id == decisionId).ToArray();
        if (matches.Length != 1)
        {
            return Result<Project>.Failure(
                new DomainError("decision.not_found", "The decision was not found in this project."));
        }

        var transition = matches[0].TransitionTo(status, changedBy, changedAt);
        if (!transition.TryGetValue(out var revisedDecision))
        {
            return Result<Project>.Failure(transition.Errors);
        }

        return ReplaceDecision(revisedDecision, changedAt);
    }

    public Result<Project> ReviseDecision(
        Guid decisionId,
        ArchitecturalDecision replacement,
        string changedBy,
        DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var matches = Decisions.Where(decision => decision.Id == decisionId).ToArray();
        if (matches.Length != 1)
        {
            return Result<Project>.Failure(
                new DomainError("decision.not_found", "The decision to revise was not found in this project."));
        }

        if (Decisions.Any(decision => decision.Id == replacement.Id))
        {
            return Result<Project>.Failure(
                new DomainError("decision.id.duplicate", "A decision with this ID already exists in the project."));
        }

        if (replacement.SupersedesDecisionId != decisionId ||
            replacement.Status != DecisionStatus.Proposed ||
            replacement.SupersededByDecisionId is not null ||
            matches[0].Status is not (DecisionStatus.Proposed or DecisionStatus.Accepted))
        {
            return Result<Project>.Failure(
                new DomainError("decision.revision.invalid", "The replacement decision does not validly supersede the current decision."));
        }

        var transition = matches[0].TransitionTo(
            DecisionStatus.Superseded,
            changedBy,
            changedAt,
            replacement.Id);
        if (!transition.TryGetValue(out var supersededDecision))
        {
            return Result<Project>.Failure(transition.Errors);
        }

        var revisedDecisions = Decisions
            .Select(decision => decision.Id == decisionId ? supersededDecision : decision)
            .Append(replacement)
            .ToArray();
        if (!HasValidDecisionLinks(revisedDecisions))
        {
            return Result<Project>.Failure(
                new DomainError("decision.revision.invalid", "The replacement decision history is inconsistent."));
        }

        return Result<Project>.Success(new Project(
            Id,
            Revision + 1,
            Name,
            Purpose,
            CreatedAt,
            changedAt,
            Context,
            Requirements,
            revisedDecisions,
            Structure));
    }

    private Result<Project> ReplaceDecision(ArchitecturalDecision decision, DateTimeOffset now)
    {
        var revisedDecisions = Decisions
            .Select(existing => existing.Id == decision.Id ? decision : existing)
            .ToArray();
        return Result<Project>.Success(new Project(
            Id,
            Revision + 1,
            Name,
            Purpose,
            CreatedAt,
            now,
            Context,
            Requirements,
            revisedDecisions,
            Structure));
    }

    private static bool HasValidDecisionLinks(IReadOnlyList<ArchitecturalDecision> decisions)
    {
        var decisionsById = decisions.ToDictionary(decision => decision.Id);
        return decisions.All(decision =>
            (decision.Status == DecisionStatus.Superseded || decision.SupersededByDecisionId is null) &&
            (decision.SupersededByDecisionId is null ||
                decisionsById.TryGetValue(decision.SupersededByDecisionId.Value, out var successor) &&
                successor.SupersedesDecisionId == decision.Id) &&
            (decision.SupersedesDecisionId is null ||
                decisionsById.TryGetValue(decision.SupersedesDecisionId.Value, out var predecessor) &&
                predecessor.SupersededByDecisionId == decision.Id));
    }

    private static bool HasValidStructureReferences(
        ArchitecturalStructure structure,
        IReadOnlyList<Requirement> requirements,
        IReadOnlyList<ArchitecturalDecision> decisions)
    {
        var requirementIds = requirements.Select(requirement => requirement.Id).ToHashSet();
        var decisionIds = decisions.Select(decision => decision.Id).ToHashSet();
        var provenance = structure.Elements.Select(element => element.Provenance)
            .Concat(structure.Connections.Select(connection => connection.Provenance));

        return provenance.All(source => source.SourceKind switch
        {
            StructuralSourceKind.Requirement => Guid.TryParse(source.SourceId, out var requirementId) && requirementIds.Contains(requirementId),
            StructuralSourceKind.Decision => Guid.TryParse(source.SourceId, out var decisionId) && decisionIds.Contains(decisionId),
            _ => true
        });
    }
}
