using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Domain.Common;
using ArchitectureAssistant.KnowledgeBase;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace ArchitectureAssistant.Infrastructure.Persistence.Json;

public sealed class JsonKnowledgeCatalog : IKnowledgeCatalog
{
    private const int MaximumItemCount = 200;
    private const int MaximumItemFileBytes = 262_144;
    private const string BuiltInResourcePrefix = "ArchitectureAssistant.KnowledgeBase.Catalog.";

    private readonly string _extensionDirectory;
    private readonly JsonSchema _itemSchema;
    private readonly ILogger<JsonKnowledgeCatalog> _logger;

    public JsonKnowledgeCatalog(string workspaceRoot, ILogger<JsonKnowledgeCatalog> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(logger);

        _extensionDirectory = Path.Combine(Path.GetFullPath(workspaceRoot), "knowledge");
        _logger = logger;

        using var schemaStream = typeof(JsonKnowledgeCatalog).Assembly.GetManifestResourceStream(
            "ArchitectureAssistant.Infrastructure.Schemas.knowledge-item.schema.json");
        if (schemaStream is null)
        {
            throw new InvalidOperationException("The embedded knowledge item JSON schema is missing.");
        }

        using var reader = new StreamReader(schemaStream);
        _itemSchema = JsonSchema.FromText(reader.ReadToEnd());
    }

    public async Task<Result<KnowledgeCatalogSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var sources = new List<CatalogSource>();
        var errors = new List<DomainError>();
        try
        {
            var knowledgeAssembly = typeof(KnowledgeBaseAssemblyMarker).Assembly;
            foreach (var resourceName in knowledgeAssembly.GetManifestResourceNames()
                         .Where(name => name.StartsWith(BuiltInResourcePrefix, StringComparison.Ordinal))
                         .Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var stream = knowledgeAssembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded catalog resource '{resourceName}' was not found.");
                var bytes = await ReadLimitedAsync(
                    stream,
                    MaximumItemFileBytes,
                    $"built-in/{resourceName[BuiltInResourcePrefix.Length..]}",
                    cancellationToken);
                sources.Add(new CatalogSource(
                    $"built-in/{resourceName[BuiltInResourcePrefix.Length..]}",
                    bytes));
            }

            if (Directory.Exists(_extensionDirectory))
            {
                var extensionPaths = Directory.EnumerateFiles(
                        _extensionDirectory,
                        "*.json",
                        SearchOption.TopDirectoryOnly)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (sources.Count + extensionPaths.Length > MaximumItemCount)
                {
                    return CatalogFailure(
                        new DomainError(
                            "knowledge.catalog.too_many_items",
                            $"The knowledge catalog exceeds the limit of {MaximumItemCount} JSON files."));
                }

                foreach (var path in extensionPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        useAsync: true);
                    var sourcePath = $"extension/{Path.GetFileName(path)}";
                    var bytes = await ReadLimitedAsync(
                        stream,
                        MaximumItemFileBytes,
                        sourcePath,
                        cancellationToken);
                    sources.Add(new CatalogSource(
                        sourcePath,
                        bytes));
                }
            }

            if (sources.Count > MaximumItemCount)
            {
                return CatalogFailure(
                    new DomainError(
                        "knowledge.catalog.too_many_items",
                        $"The knowledge catalog exceeds the limit of {MaximumItemCount} JSON files."));
            }
        }
        catch (CatalogFileTooLargeException exception)
        {
            _logger.LogWarning(exception, "Knowledge catalog file {CatalogPath} exceeds the supported size.", exception.Path);
            return CatalogFailure(
                new DomainError(
                    "knowledge.catalog.file_too_large",
                    $"{exception.Path}: the JSON file exceeds the {MaximumItemFileBytes}-byte limit."));
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to read the configured knowledge catalog.");
            return CatalogFailure(
                new DomainError("knowledge.catalog.read_failed", "The local knowledge catalog could not be read."));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Access to the configured knowledge catalog was denied.");
            return CatalogFailure(
                new DomainError("knowledge.catalog.access_denied", "The application does not have access to the local knowledge catalog."));
        }

        try
        {
            var parsedSources = new List<ParsedCatalogSource>();
            try
            {
                foreach (var source in sources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var document = JsonDocument.Parse(source.Bytes);
                        parsedSources.Add(new ParsedCatalogSource(source.Path, source.Bytes, document));
                    }
                    catch (JsonException exception)
                    {
                        _logger.LogWarning(exception, "Knowledge catalog file {CatalogPath} contains invalid JSON.", source.Path);
                        errors.Add(new DomainError(
                            "knowledge.catalog.invalid_json",
                            $"{source.Path}: invalid JSON."));
                    }
                }

                foreach (var source in parsedSources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var evaluation = _itemSchema.Evaluate(source.Document.RootElement, new EvaluationOptions
                    {
                        OutputFormat = OutputFormat.List
                    });
                    if (!evaluation.IsValid)
                    {
                        var locations = GetSchemaErrorLocations(evaluation);
                        _logger.LogWarning(
                            "Knowledge catalog file {CatalogPath} does not match the supported schema at {Locations}.",
                            source.Path,
                            string.Join(", ", locations));
                        errors.Add(new DomainError(
                            "knowledge.catalog.schema_invalid",
                            $"{source.Path}: schema validation failed at {string.Join(", ", locations)}."));
                    }
                }

                if (errors.Count == 0)
                {
                    ValidateIdentifiersAndReferences(parsedSources, errors);
                }

                if (errors.Count == 0)
                {
                    ValidateRuleSemantics(parsedSources, errors);
                }

                if (errors.Count > 0)
                {
                    return CatalogFailure(errors);
                }

                var items = parsedSources
                    .Select(source => ToView(source.Document.RootElement, source))
                    .OrderBy(item => item.Kind, StringComparer.Ordinal)
                    .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray();
                var rules = parsedSources
                    .Where(source => ReadString(source.Document.RootElement, "kind") == "Rule")
                    .Select(source => ToRuleView(source.Document.RootElement, source))
                    .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                    .ToArray();

                return Result<KnowledgeCatalogSnapshot>.Success(new KnowledgeCatalogSnapshot(
                    "1.0",
                    ComputeContentHash(sources),
                    items,
                    rules));
            }
            finally
            {
                foreach (var source in parsedSources)
                {
                    source.Document.Dispose();
                }
            }
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Failed to validate the local knowledge catalog.");
            return CatalogFailure(
                new DomainError("knowledge.catalog.read_failed", "The local knowledge catalog could not be read."));
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream stream,
        int maximumBytes,
        string path,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + bytesRead > maximumBytes)
            {
                throw new CatalogFileTooLargeException(path, maximumBytes);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static void ValidateIdentifiersAndReferences(
        IReadOnlyList<ParsedCatalogSource> sources,
        ICollection<DomainError> errors)
    {
        var identifiers = new Dictionary<string, ParsedCatalogSource>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var root = source.Document.RootElement;
            var id = root.GetProperty("id").GetString()!;
            if (identifiers.TryGetValue(id, out var existing))
            {
                errors.Add(new DomainError(
                    "knowledge.catalog.duplicate_id",
                    $"{source.Path}: duplicate id '{id}' also declared in {existing.Path} (/id)."));
            }
            else
            {
                identifiers.Add(id, source);
            }
        }

        foreach (var source in sources)
        {
            foreach (var reference in GetReferences(source.Document.RootElement))
            {
                if (reference.Issue is not null)
                {
                    errors.Add(new DomainError(
                        "knowledge.catalog.reference_invalid",
                        $"{source.Path}: {reference.Issue} at {reference.Path}."));
                    continue;
                }

                if (!identifiers.ContainsKey(reference.Id))
                {
                    errors.Add(new DomainError(
                        "knowledge.catalog.reference_missing",
                        $"{source.Path}: reference '{reference.Id}' at {reference.Path} does not identify a catalog item."));
                }
            }
        }
    }

    private static void ValidateRuleSemantics(
        IReadOnlyList<ParsedCatalogSource> sources,
        ICollection<DomainError> errors)
    {
        var kindsById = sources.ToDictionary(
            source => ReadString(source.Document.RootElement, "id")!,
            source => ReadString(source.Document.RootElement, "kind")!,
            StringComparer.Ordinal);
        foreach (var source in sources.Where(item =>
                     ReadString(item.Document.RootElement, "kind") == "Rule"))
        {
            var root = source.Document.RootElement;
            var ruleId = ReadString(root, "id")!;
            if (ReadString(root, "schemaVersion") != "1.0")
            {
                errors.Add(new DomainError(
                    "knowledge.catalog.rule_version_unsupported",
                    $"{source.Path} ({ruleId}): unsupported rule schema version at /schemaVersion."));
            }

            var appliesTo = root.GetProperty("appliesTo");
            var declaredKinds = ReadStringList(appliesTo, "kinds");
            foreach (var (candidateId, index) in appliesTo.TryGetProperty("ids", out var ids)
                         ? EnumerateStrings(ids)
                         : [])
            {
                if (!kindsById.TryGetValue(candidateId, out var candidateKind))
                {
                    continue;
                }

                if (!declaredKinds.Contains(candidateKind, StringComparer.Ordinal))
                {
                    errors.Add(new DomainError(
                        "knowledge.catalog.rule_target_kind_mismatch",
                        $"{source.Path} ({ruleId}): target '{candidateId}' has kind '{candidateKind}', which is not listed in appliesTo.kinds at /appliesTo/ids/{index}."));
                }
            }

            var conditions = ParseCondition(root.GetProperty("conditions"));
            foreach (var error in RuleConditionEvaluator.Validate(conditions, "/conditions"))
            {
                errors.Add(new DomainError(
                    "knowledge.catalog.rule_invalid",
                    $"{source.Path} ({ruleId}): {error}"));
            }

            if (!root.TryGetProperty("appliesWhen", out var appliesWhen))
            {
                continue;
            }

            var applicability = ParseCondition(appliesWhen);
            foreach (var error in RuleConditionEvaluator.Validate(applicability, "/appliesWhen"))
            {
                errors.Add(new DomainError(
                    "knowledge.catalog.rule_invalid",
                    $"{source.Path} ({ruleId}): {error}"));
            }

            if (EnumeratePredicates(applicability)
                .Any(condition => condition.Fact is null ||
                                  !condition.Fact.StartsWith("project.", StringComparison.Ordinal)))
            {
                errors.Add(new DomainError(
                    "knowledge.catalog.rule_invalid",
                    $"{source.Path} ({ruleId}): appliesWhen may only reference project facts at /appliesWhen."));
            }
        }
    }

    private static IEnumerable<RuleCondition> EnumeratePredicates(RuleCondition condition)
    {
        if (condition.Kind == RuleConditionKind.Predicate)
        {
            yield return condition;
        }

        foreach (var child in condition.Children)
        {
            foreach (var predicate in EnumeratePredicates(child))
            {
                yield return predicate;
            }
        }
    }

    private static RuleCondition ParseCondition(JsonElement element)
    {
        if (element.TryGetProperty("fact", out var fact))
        {
            var @operator = Enum.Parse<RuleOperator>(
                element.GetProperty("operator").GetString()!,
                ignoreCase: false);
            var value = element.TryGetProperty("value", out var operand)
                ? ParseRuleValue(operand)
                : null;
            return new RuleCondition(
                RuleConditionKind.Predicate,
                fact.GetString(),
                @operator,
                value,
                Array.Empty<RuleCondition>());
        }

        if (element.TryGetProperty("allOf", out var allOf))
        {
            return new RuleCondition(
                RuleConditionKind.AllOf,
                null,
                null,
                null,
                allOf.EnumerateArray().Select(ParseCondition).ToArray());
        }

        if (element.TryGetProperty("anyOf", out var anyOf))
        {
            return new RuleCondition(
                RuleConditionKind.AnyOf,
                null,
                null,
                null,
                anyOf.EnumerateArray().Select(ParseCondition).ToArray());
        }

        return new RuleCondition(
            RuleConditionKind.Not,
            null,
            null,
            null,
            [ParseCondition(element.GetProperty("not"))]);
    }

    private static RuleValue ParseRuleValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return new RuleValue(
                RuleValueKind.Array,
                Items: element.EnumerateArray().Select(ParseRuleValue).ToArray());
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => new RuleValue(RuleValueKind.String, StringValue: element.GetString()),
            JsonValueKind.Number => element.TryGetDecimal(out var number)
                ? new RuleValue(RuleValueKind.Number, NumberValue: number)
                : new RuleValue(RuleValueKind.Null),
            JsonValueKind.True => new RuleValue(RuleValueKind.Boolean, BooleanValue: true),
            JsonValueKind.False => new RuleValue(RuleValueKind.Boolean, BooleanValue: false),
            JsonValueKind.Null => new RuleValue(RuleValueKind.Null),
            _ => throw new InvalidOperationException("The rule schema allowed an unsupported JSON literal.")
        };
    }

    private static IEnumerable<CatalogReference> GetReferences(JsonElement root)
    {
        var ownId = root.GetProperty("id").GetString()!;
        if (root.TryGetProperty("relations", out var relations) &&
            relations.ValueKind == JsonValueKind.Array)
        {
            var seenRelations = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < relations.GetArrayLength(); index++)
            {
                var relation = relations[index];
                var targetId = relation.GetProperty("targetId").GetString()!;
                var type = relation.GetProperty("type").GetString()!;
                var path = $"/relations/{index}/targetId";
                if (targetId == ownId)
                {
                    yield return new CatalogReference(path, ownId, "A relation cannot target its own item");
                }

                var conditionKey = relation.TryGetProperty("appliesWhen", out var appliesWhen)
                    ? JsonSerializer.Serialize(ParseCondition(appliesWhen))
                    : "always";
                if (!seenRelations.Add($"{type}\0{targetId}\0{conditionKey}"))
                {
                    yield return new CatalogReference(path, targetId, "The relation is duplicated");
                }

                yield return new CatalogReference(path, targetId);
            }
        }

        var kind = root.GetProperty("kind").GetString();
        if (kind == "Rule" &&
            root.TryGetProperty("appliesTo", out var ruleTarget) &&
            ruleTarget.TryGetProperty("ids", out var targetIds))
        {
            foreach (var (id, index) in EnumerateStrings(targetIds))
            {
                yield return new CatalogReference($"/appliesTo/ids/{index}", id);
            }
        }
        else if (kind == "Dependency")
        {
            foreach (var property in new[] { "sourceId", "targetId" })
            {
                yield return new CatalogReference($"/{property}", root.GetProperty(property).GetString()!);
            }
        }
        else if (kind == "BestPractice" &&
                 root.TryGetProperty("appliesTo", out var appliesTo))
        {
            foreach (var (id, index) in EnumerateStrings(appliesTo))
            {
                yield return new CatalogReference($"/appliesTo/{index}", id);
            }
        }
        else if (kind == "TradeOff" &&
                 root.TryGetProperty("appliesTo", out var tradeOffAppliesTo))
        {
            foreach (var (id, index) in EnumerateStrings(tradeOffAppliesTo))
            {
                yield return new CatalogReference($"/appliesTo/{index}", id);
            }
        }
        else if (kind == "ProjectTemplate" &&
                 root.TryGetProperty("suggestedItemIds", out var suggestedItemIds))
        {
            foreach (var (id, index) in EnumerateStrings(suggestedItemIds))
            {
                yield return new CatalogReference($"/suggestedItemIds/{index}", id);
            }
        }
    }

    private static IEnumerable<(string Id, int Index)> EnumerateStrings(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        for (var index = 0; index < array.GetArrayLength(); index++)
        {
            yield return (array[index].GetString()!, index);
        }
    }

    private static IReadOnlyList<string> GetSchemaErrorLocations(EvaluationResults evaluation)
    {
        var locations = Flatten(evaluation)
            .Where(result => !result.IsValid && result.Errors is { Count: > 0 })
            .SelectMany(result => result.Errors!.Keys.Select(error =>
                $"{result.InstanceLocation} ({error})"))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        return locations.Length > 0 ? locations : [evaluation.InstanceLocation.ToString()];
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults result)
    {
        yield return result;
        if (result.Details is null)
        {
            yield break;
        }

        foreach (var child in result.Details)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static KnowledgeItemView ToView(JsonElement root, ParsedCatalogSource source)
    {
        var kind = ReadString(root, "kind")!;
        var benefits = kind switch
        {
            "Architecture" => ReadStringList(root, "advantages"),
            "Pattern" or "Technology" => ReadStringList(root, "benefits"),
            _ => Array.Empty<string>()
        };
        var limitations = kind switch
        {
            "Architecture" => ReadStringList(root, "disadvantages"),
            "Pattern" or "Technology" => ReadStringList(root, "risks"),
            _ => Array.Empty<string>()
        };

        return new KnowledgeItemView(
            ReadString(root, "id")!,
            kind,
            ReadString(root, "version")!,
            ReadString(root, "name")!,
            ReadString(root, "description")!,
            ReadString(root, "source"),
            ReadString(root, "category"),
            ReadStringList(root, "tags"),
            benefits,
            limitations,
            ReadStringList(root, "useWhen"),
            ReadStringList(root, "avoidWhen"),
            ReadInt32(root, "complexity"),
            ReadInt32(root, "scalability"),
            ReadInt32(root, "operationalCost"),
            source.Path,
            root.TryGetProperty("relations", out var relations)
                ? relations.EnumerateArray()
                    .Select(relation => new KnowledgeRelationView(
                        ReadString(relation, "type")!,
                        ReadString(relation, "targetId")!,
                        ReadString(relation, "rationale")!,
                        ReadString(relation, "source") ?? ReadString(root, "source"),
                        relation.TryGetProperty("appliesWhen", out var appliesWhen)
                            ? ParseCondition(appliesWhen)
                            : null,
                        source.Path))
                    .ToArray()
                : Array.Empty<KnowledgeRelationView>());
    }

    private static KnowledgeRuleView ToRuleView(JsonElement root, ParsedCatalogSource source)
    {
        var appliesTo = root.GetProperty("appliesTo");
        var conditions = ParseCondition(root.GetProperty("conditions"));
        var applicabilityCondition = root.TryGetProperty("appliesWhen", out var appliesWhen)
            ? ParseCondition(appliesWhen)
            : null;

        return new KnowledgeRuleView(
            ReadString(root, "id")!,
            ReadString(root, "version")!,
            ReadString(root, "name")!,
            ReadString(root, "source"),
            source.Path,
            Convert.ToHexString(SHA256.HashData(source.Bytes)).ToLowerInvariant(),
            root.GetProperty("enabled").GetBoolean(),
            ReadInt32(root, "weight")!.Value,
            ReadStringList(appliesTo, "kinds"),
            ReadStringList(appliesTo, "ids"),
            ReadStringList(appliesTo, "tags"),
            applicabilityCondition,
            conditions,
            root.GetProperty("effects").EnumerateArray()
                .Select(effect => new KnowledgeRuleEffectView(
                    ReadString(effect, "type")!,
                    effect.TryGetProperty("hardConstraint", out var hardConstraint) &&
                        hardConstraint.ValueKind == JsonValueKind.True,
                    ReadInt32(effect, "points"),
                    ReadString(effect, "severity"),
                    ReadString(effect, "rationale")!))
                .ToArray());
    }

    private static string? ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringList(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray()
            : Array.Empty<string>();

    private static int? ReadInt32(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;

    private static string ComputeContentHash(IEnumerable<CatalogSource> sources)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var source in sources.OrderBy(source => source.Path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(source.Path));
            hash.AppendData([0]);
            hash.AppendData(source.Bytes);
            hash.AppendData([255]);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private Result<KnowledgeCatalogSnapshot> CatalogFailure(params DomainError[] errors) =>
        CatalogFailure((IEnumerable<DomainError>)errors);

    private Result<KnowledgeCatalogSnapshot> CatalogFailure(IEnumerable<DomainError> errors)
    {
        var materializedErrors = errors.ToArray();
        foreach (var error in materializedErrors)
        {
            _logger.LogWarning("Knowledge catalog validation failed: {CatalogError}", error.Message);
        }

        return Result<KnowledgeCatalogSnapshot>.Failure(materializedErrors);
    }

    private sealed record CatalogSource(string Path, byte[] Bytes);

    private sealed record ParsedCatalogSource(string Path, byte[] Bytes, JsonDocument Document);

    private sealed record CatalogReference(string Path, string Id, string? Issue = null);

    private sealed class CatalogFileTooLargeException(string path, int maximumBytes)
        : IOException($"Catalog file '{path}' exceeds the {maximumBytes}-byte file limit.")
    {
        public string Path { get; } = path;
    }
}
