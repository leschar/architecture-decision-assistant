using ArchitectureAssistant.Application.Knowledge;
using ArchitectureAssistant.Domain.Analysis;
using ArchitectureAssistant.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArchitectureAssistant.Infrastructure.Tests;

public sealed class JsonKnowledgeCatalogTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(),
        "ArchitectureDecisionAssistant.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly JsonKnowledgeCatalog _catalog;

    public JsonKnowledgeCatalogTests()
    {
        _catalog = new JsonKnowledgeCatalog(_workspace, NullLogger<JsonKnowledgeCatalog>.Instance);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsValidatedBuiltInItemsWithStableHash()
    {
        var first = await _catalog.GetSnapshotAsync(CancellationToken.None);
        var second = await _catalog.GetSnapshotAsync(CancellationToken.None);

        Assert.True(first.TryGetValue(out var firstSnapshot), string.Join(Environment.NewLine, first.Errors.Select(error => $"{error.Code}: {error.Message}")));
        Assert.True(second.TryGetValue(out var secondSnapshot), string.Join(Environment.NewLine, second.Errors.Select(error => $"{error.Code}: {error.Message}")));
        Assert.Equal("1.0", firstSnapshot.SchemaVersion);
        Assert.Equal(11, firstSnapshot.Items.Count);
        Assert.Equal(64, firstSnapshot.ContentHash.Length);
        Assert.Equal(firstSnapshot.ContentHash, secondSnapshot.ContentHash);
        Assert.Equal(
            firstSnapshot.Rules.Select(rule => rule.ContentHash),
            secondSnapshot.Rules.Select(rule => rule.ContentHash));
        Assert.Contains(firstSnapshot.Rules, rule =>
            rule.Id == "small-team-microservices-cost" &&
            rule.KnowledgeSource == "built-in/rule-small-team-microservices.json" &&
            ContainsFact(rule.Conditions, "project.teamSize"));
        Assert.Contains(firstSnapshot.Items, item => item.Id == "modular-monolith" && item.Kind == "Architecture");
        Assert.Contains(firstSnapshot.Items, item => item.Id == "cqrs" && item.Kind == "Pattern");
        Assert.Contains(firstSnapshot.Items, item => item.Id == "redis" && item.Kind == "Technology");
    }

    [Fact]
    public async Task GetSnapshot_DiscoversValidJsonExtensionWithoutRecompilation()
    {
        var extensionDirectory = Path.Combine(_workspace, "knowledge");
        Directory.CreateDirectory(extensionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "hexagonal-architecture.json"),
            """
            {
              "id": "hexagonal-architecture",
              "kind": "Architecture",
              "schemaVersion": "1.0",
              "version": "1.0.0",
              "name": "Arquitetura Hexagonal",
              "description": "Organiza o domínio para reduzir dependência de detalhes externos.",
              "category": "Modular",
              "advantages": ["Protege o domínio de detalhes de infraestrutura."],
              "disadvantages": ["Exige disciplina na definição das portas."],
              "useWhen": ["Há necessidade de isolar o domínio."],
              "avoidWhen": ["A abstração não traz benefício ao contexto."],
              "complexity": 5,
              "scalability": 6
            }
            """);

        var result = await _catalog.GetSnapshotAsync(CancellationToken.None);

        Assert.True(result.TryGetValue(out var snapshot), string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Code}: {error.Message}")));
        var item = Assert.Single(snapshot.Items, value => value.Id == "hexagonal-architecture");
        Assert.Equal("Arquitetura Hexagonal", item.Name);
        Assert.Single(item.Benefits);
        Assert.Single(item.Limitations);
    }

    [Fact]
    public async Task GetSnapshot_PreservesRelationConditionsAndFileProvenance()
    {
        var extensionDirectory = Path.Combine(_workspace, "knowledge");
        Directory.CreateDirectory(extensionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "graph-architecture.json"),
            """
            {
              "id": "graph-architecture",
              "kind": "Architecture",
              "schemaVersion": "1.0",
              "version": "1.0.0",
              "name": "Graph architecture",
              "description": "Architecture used to verify relation mapping.",
              "source": "Curated architecture source",
              "category": "Modular",
              "advantages": ["Clear boundaries."],
              "disadvantages": ["Requires design effort."],
              "useWhen": ["Boundaries matter."],
              "avoidWhen": ["No boundaries are needed."],
              "complexity": 3,
              "scalability": 5,
              "relations": [
                {
                  "type": "Recommends",
                  "targetId": "graph-pattern",
                  "rationale": "Useful when team size exceeds three.",
                  "appliesWhen": {
                    "fact": "project.teamSize",
                    "operator": "GreaterThan",
                    "value": 3
                  }
                },
                {
                  "type": "Recommends",
                  "targetId": "graph-pattern",
                  "rationale": "Useful when team size is below eight.",
                  "appliesWhen": {
                    "fact": "project.teamSize",
                    "operator": "LessThan",
                    "value": 8
                  }
                }
              ]
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "graph-pattern.json"),
            """
            {
              "id": "graph-pattern",
              "kind": "Pattern",
              "schemaVersion": "1.0",
              "version": "1.0.0",
              "name": "Graph pattern",
              "description": "Pattern used as a relation endpoint.",
              "category": "Test",
              "problem": "A test problem.",
              "solution": "A test solution.",
              "benefits": ["A test benefit."],
              "risks": ["A test risk."],
              "complexity": 2
            }
            """);

        var result = await _catalog.GetSnapshotAsync(CancellationToken.None);

        Assert.True(result.TryGetValue(out var snapshot), string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Code}: {error.Message}")));
        var item = Assert.Single(snapshot.Items, value => value.Id == "graph-architecture");
        Assert.Equal(2, item.Relations.Count);
        Assert.All(item.Relations, relation =>
        {
            Assert.Equal("graph-pattern", relation.TargetId);
            Assert.Equal("Curated architecture source", relation.Source);
            Assert.Equal("project.teamSize", relation.AppliesWhen!.Fact);
            Assert.Equal("extension/graph-architecture.json", relation.KnowledgeSource);
        });
    }

    [Fact]
    public async Task GetSnapshot_RejectsInvalidExtensionWithoutReturningPartialCatalog()
    {
        var extensionDirectory = Path.Combine(_workspace, "knowledge");
        Directory.CreateDirectory(extensionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "broken.json"),
            """{"id":"broken","kind":"Technology"}""");

        var result = await _catalog.GetSnapshotAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error =>
            error.Code == "knowledge.catalog.schema_invalid" &&
            error.Message.Contains("extension/broken.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetSnapshot_ReportsInvalidJsonWithItsFileName()
    {
        var extensionDirectory = Path.Combine(_workspace, "knowledge");
        Directory.CreateDirectory(extensionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "malformed.json"),
            """{"id":""");

        var result = await _catalog.GetSnapshotAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error =>
            error.Code == "knowledge.catalog.invalid_json" &&
            error.Message.Contains("extension/malformed.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetSnapshot_RejectsDuplicateIdentifiersAndMissingReferences()
    {
        var extensionDirectory = Path.Combine(_workspace, "knowledge");
        Directory.CreateDirectory(extensionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "duplicate.json"),
            """
            {
              "id": "redis",
              "kind": "Technology",
              "schemaVersion": "1.0",
              "version": "1.0.0",
              "name": "Duplicado",
              "description": "Item de teste duplicado.",
              "category": "Test",
              "capabilities": ["Teste"],
              "useWhen": ["Teste"],
              "avoidWhen": ["Teste"],
              "complexity": 1,
              "operationalCost": 1
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(extensionDirectory, "missing-reference.json"),
            """
            {
              "id": "new-architecture",
              "kind": "Architecture",
              "schemaVersion": "1.0",
              "version": "1.0.0",
              "name": "Nova arquitetura",
              "description": "Item de teste com referência quebrada.",
              "category": "Test",
              "advantages": ["Teste"],
              "disadvantages": ["Teste"],
              "useWhen": ["Teste"],
              "avoidWhen": ["Teste"],
              "complexity": 1,
              "scalability": 1,
              "relations": [
                {
                  "type": "DependsOn",
                  "targetId": "missing-item",
                  "rationale": "Referência inexistente."
                }
              ]
            }
            """);

        var result = await _catalog.GetSnapshotAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "knowledge.catalog.duplicate_id");
        Assert.Contains(result.Errors, error => error.Code == "knowledge.catalog.reference_missing");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private static bool ContainsFact(RuleCondition condition, string fact) =>
        condition.Fact == fact || condition.Children.Any(child => ContainsFact(child, fact));
}
