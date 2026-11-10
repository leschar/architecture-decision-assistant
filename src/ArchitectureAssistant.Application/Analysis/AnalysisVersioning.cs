using System.Security.Cryptography;
using System.Text;
using ArchitectureAssistant.Application.Knowledge;

namespace ArchitectureAssistant.Application.Analysis;

internal static class AnalysisVersioning
{
    public static string ComputeRuleSetHash(KnowledgeCatalogSnapshot catalog)
    {
        var content = catalog.Rules
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(rule => $"{rule.Id}\0{rule.Version}\0{rule.ContentHash}\n");
        var bytes = Encoding.UTF8.GetBytes(string.Concat(content));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
