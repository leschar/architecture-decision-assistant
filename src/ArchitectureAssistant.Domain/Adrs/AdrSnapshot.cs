using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.Domain.Projects;

namespace ArchitectureAssistant.Domain.Adrs;

public enum AdrStatus
{
    Draft,
    NeedsReview,
    Accepted,
    Superseded
}

public enum AdrCompleteness
{
    Complete,
    Partial,
    NotProvided
}

public enum AdrAuthorship
{
    GeneratedFromDecision,
    UserAuthored,
    KnowledgeCatalog
}

public enum AdrSectionKind
{
    Context,
    Problem,
    Decision,
    Consequences,
    Risks,
    Benefits,
    Justification,
    TradeOffs
}

public sealed record AdrSourceReference(string SourceType, string SourceId, string? SourceVersion);

public sealed record AdrTextSection(
    string Content,
    AdrCompleteness Completeness,
    AdrAuthorship Authorship,
    IReadOnlyList<AdrSourceReference> SourceReferences);

public sealed record AdrAlternative(
    string AlternativeId,
    string Name,
    string? Description,
    IReadOnlyList<AdrSourceReference> SourceReferences);

public sealed record AdrAlternativesSection(
    AdrCompleteness Completeness,
    IReadOnlyList<AdrAlternative> Items,
    IReadOnlyList<AdrSourceReference> SourceReferences);

public sealed record AdrSections(
    AdrTextSection Context,
    AdrTextSection Problem,
    AdrAlternativesSection Alternatives,
    AdrTextSection Decision,
    AdrTextSection Consequences,
    AdrTextSection Risks,
    AdrTextSection Benefits,
    AdrTextSection Justification,
    AdrTextSection TradeOffs)
{
    public AdrTextSection Get(AdrSectionKind kind) =>
        kind switch
        {
            AdrSectionKind.Context => Context,
            AdrSectionKind.Problem => Problem,
            AdrSectionKind.Decision => Decision,
            AdrSectionKind.Consequences => Consequences,
            AdrSectionKind.Risks => Risks,
            AdrSectionKind.Benefits => Benefits,
            AdrSectionKind.Justification => Justification,
            AdrSectionKind.TradeOffs => TradeOffs,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public AdrSections With(AdrSectionKind kind, AdrTextSection section) =>
        kind switch
        {
            AdrSectionKind.Context => this with { Context = section },
            AdrSectionKind.Problem => this with { Problem = section },
            AdrSectionKind.Decision => this with { Decision = section },
            AdrSectionKind.Consequences => this with { Consequences = section },
            AdrSectionKind.Risks => this with { Risks = section },
            AdrSectionKind.Benefits => this with { Benefits = section },
            AdrSectionKind.Justification => this with { Justification = section },
            AdrSectionKind.TradeOffs => this with { TradeOffs = section },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}

public sealed record AdrSourceSnapshot(
    string ProjectRevision,
    string DecisionRevision,
    string? AnalysisId,
    string? CatalogFingerprint,
    string GeneratorVersion);

public sealed record AdrReview(string ReviewedBy, DateTimeOffset ReviewedAt, string? Note);

public sealed record AdrSnapshot(
    string SchemaVersion,
    string AdrId,
    Guid ProjectId,
    Guid DecisionId,
    long Revision,
    AdrStatus Status,
    string Title,
    AdrSections Sections,
    AdrSourceSnapshot SourceSnapshot,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? SupersedesAdrId,
    AdrReview? Review,
    string? SupersededByAdrId = null)
{
    public static Result<AdrSnapshot> Compose(
        Project project,
        ArchitecturalDecision decision,
        string adrId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(adrId) ||
            adrId.Length > 120 ||
            project.Id == Guid.Empty ||
            project.Decisions.All(existing => existing.Id != decision.Id) ||
            now == default)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.source.invalid", "ADR requires a decision revision belonging to the project."));
        }

        var decisionSource = Reference("Decision", decision.Id.ToString("D"), decision.Revision.ToString());
        var projectSource = Reference("ManualInput", project.Id.ToString("D"), project.Revision.ToString());
        var contextLines = new List<string> { $"Projeto: {project.Name}", $"Objetivo registrado: {project.Purpose}" };
        contextLines.AddRange(project.Requirements
            .Where(requirement => requirement.Status == RequirementStatus.Confirmed)
            .Select(requirement => $"Requisito confirmado: {requirement.Description}"));
        contextLines.AddRange(project.Context.Constraints.Select(constraint => $"Restrição registrada: {constraint}"));
        var contextSources = project.Requirements
            .Where(requirement => requirement.Status == RequirementStatus.Confirmed)
            .Select(requirement => Reference("ProjectRequirement", requirement.Id.ToString("D"), project.Revision.ToString()))
            .Prepend(projectSource)
            .ToArray();

        var alternatives = decision.ConsideredAlternatives.Select(id => new AdrAlternative(
            id,
            id,
            null,
            [decisionSource])).ToArray();
        var sections = new AdrSections(
            Text(string.Join(Environment.NewLine, contextLines), AdrCompleteness.Partial, AdrAuthorship.UserAuthored, contextSources),
            Missing(),
            new AdrAlternativesSection(
                alternatives.Length == 0 ? AdrCompleteness.NotProvided : AdrCompleteness.Partial,
                alternatives,
                alternatives.Length == 0 ? Array.Empty<AdrSourceReference>() : [decisionSource]),
            Text($"{decision.SelectedAlternativeKind}: {decision.SelectedAlternativeId}", AdrCompleteness.Complete, AdrAuthorship.GeneratedFromDecision, [decisionSource]),
            decision.Consequences.Count == 0
                ? Missing()
                : Text(string.Join(Environment.NewLine, decision.Consequences), AdrCompleteness.Partial, AdrAuthorship.GeneratedFromDecision, [decisionSource]),
            Missing(),
            Missing(),
            Text(decision.Justification, AdrCompleteness.Complete, AdrAuthorship.GeneratedFromDecision, [decisionSource]),
            Missing());

        return Result<AdrSnapshot>.Success(new AdrSnapshot(
            "1.0",
            adrId,
            project.Id,
            decision.Id,
            1,
            AdrStatus.NeedsReview,
            $"Decisão: {decision.SelectedAlternativeId}",
            sections,
            new AdrSourceSnapshot(
                project.Revision.ToString(),
                decision.Revision.ToString(),
                decision.AnalysisId?.ToString("D"),
                null,
                "1.0.0"),
            now,
            now,
            null,
            null));
    }

    public Result<AdrSnapshot> ReviseSection(
        AdrSectionKind sectionKind,
        string content,
        string author,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(sectionKind) ||
            string.IsNullOrWhiteSpace(author) ||
            author.Trim().Length > 120 ||
            content is null ||
            content.Length > 10000 ||
            now < UpdatedAt ||
            Revision == long.MaxValue ||
            Status is AdrStatus.Accepted or AdrStatus.Superseded)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.revision.invalid", "The ADR section revision is invalid or the document is not editable."));
        }

        var section = new AdrTextSection(
            content.Trim(),
            string.IsNullOrWhiteSpace(content) ? AdrCompleteness.NotProvided : AdrCompleteness.Complete,
            AdrAuthorship.UserAuthored,
            [Reference("ManualInput", author.Trim(), Revision.ToString())]);
        var revisedSections = Sections.With(sectionKind, section);
        var nextStatus = HasMissingSections(revisedSections) ? AdrStatus.NeedsReview : AdrStatus.Draft;
        return Result<AdrSnapshot>.Success(this with
        {
            Revision = Revision + 1,
            Sections = revisedSections,
            Status = nextStatus,
            UpdatedAt = now,
            Review = null
        });
    }

    public Result<AdrSnapshot> Accept(string reviewer, DateTimeOffset reviewedAt, string? note)
    {
        if (Status is AdrStatus.Accepted or AdrStatus.Superseded ||
            string.IsNullOrWhiteSpace(reviewer) ||
            reviewer.Trim().Length > 120 ||
            reviewedAt < UpdatedAt ||
            reviewedAt == default ||
            Revision == long.MaxValue ||
            HasMissingSections(Sections))
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.review.invalid", "The ADR review details or current state are invalid."));
        }

        return Result<AdrSnapshot>.Success(this with
        {
            Revision = Revision + 1,
            Status = AdrStatus.Accepted,
            UpdatedAt = reviewedAt,
            Review = new AdrReview(reviewer.Trim(), reviewedAt, note?.Trim())
        });
    }

    public Result<AdrSnapshot> SupersedeBy(string replacementAdrId, DateTimeOffset supersededAt)
    {
        if (string.IsNullOrWhiteSpace(replacementAdrId) ||
            replacementAdrId.Trim().Length > 120 ||
            string.Equals(AdrId, replacementAdrId.Trim(), StringComparison.Ordinal) ||
            Status == AdrStatus.Superseded ||
            SupersededByAdrId is not null ||
            supersededAt == default ||
            supersededAt < UpdatedAt ||
            Revision == long.MaxValue)
        {
            return Result<AdrSnapshot>.Failure(
                new DomainError("adr.supersession.invalid", "The ADR replacement or current state is invalid."));
        }

        return Result<AdrSnapshot>.Success(this with
        {
            Revision = Revision + 1,
            Status = AdrStatus.Superseded,
            UpdatedAt = supersededAt,
            SupersededByAdrId = replacementAdrId.Trim()
        });
    }

    private static bool HasMissingSections(AdrSections sections) =>
        sections.Alternatives.Completeness == AdrCompleteness.NotProvided ||
        new[]
        {
            sections.Context, sections.Problem, sections.Decision, sections.Consequences,
            sections.Risks, sections.Benefits, sections.Justification, sections.TradeOffs
        }.Any(section => section.Completeness == AdrCompleteness.NotProvided);

    private static AdrTextSection Missing() =>
        new(string.Empty, AdrCompleteness.NotProvided, AdrAuthorship.GeneratedFromDecision, Array.Empty<AdrSourceReference>());

    private static AdrTextSection Text(
        string content,
        AdrCompleteness completeness,
        AdrAuthorship authorship,
        IReadOnlyList<AdrSourceReference> sources) =>
        new(content, completeness, authorship, sources);

    private static AdrSourceReference Reference(string sourceType, string sourceId, string? version) =>
        new(sourceType, sourceId, version);
}
