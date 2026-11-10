using ArchitectureAssistant.Domain.KnowledgeGraph;

namespace ArchitectureAssistant.Application.Knowledge;

internal static class KnowledgeGraphCatalogMapper
{
    public static IReadOnlyList<KnowledgeGraphItem> Map(IReadOnlyList<KnowledgeItemView> items) =>
        items.Select(item => new KnowledgeGraphItem(
            item.Id,
            item.Kind,
            item.Version,
            item.Name,
            item.Source,
            item.KnowledgeSource,
            item.Relations.Select(relation => new KnowledgeGraphRelationDeclaration(
                relation.Type,
                relation.TargetId,
                relation.Rationale,
                relation.Source,
                relation.AppliesWhen,
                relation.KnowledgeSource)).ToArray()))
        .ToArray();
}
