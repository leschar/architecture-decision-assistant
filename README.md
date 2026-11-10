# Definições importantes

Arquitetura:
Estilo de construção de software.

Pattern:
Solução recorrente para um problema.

Technology:
Ferramenta utilizada na implementação.

Rule:
Lógica utilizada pelo motor de decisão.

Recommendation:
Resultado calculado pelo sistema.

TradeOff:
Consequências positivas e negativas de uma escolha.

## Documentação do produto

- [Visão do produto](docs/project-vision.md)
- [Modelo de domínio](docs/domain-model.md)
- [Modelo de conhecimento](docs/knowledge-model.md)
- [Motor de regras e decisão](docs/rule-engine.md)
- [Questionário arquitetural](docs/architectural-questionnaire.md)
- [Interpretação determinística de requisitos em texto](docs/requirement-text-interpretation.md)
- [Grafo de conhecimento e dependências](docs/graph-dependency-model.md)
- [Geração de Diagramas C4](docs/diagram-generation.md)
- [Experiência da Aplicação](docs/application-experience.md)
- [Geração e Gestão de ADRs](docs/adr-generation.md)
- [Architectural Knowledge History](docs/architectural-knowledge-history.md)
- [Arquitetura da Solução .NET](docs/dotnet-solution-architecture.md)
- [Backlog Completo do Produto](docs/product-backlog.md)

## Executar localmente

Requisitos: .NET SDK 10.0.401 ou versão de patch compatível.

```powershell
dotnet restore ArchitectureAssistant.sln
dotnet run --project src\ArchitectureAssistant.Web\ArchitectureAssistant.Web.csproj
```

O MVP escuta somente em `http://127.0.0.1:5182`. Projetos são arquivos JSON
no `%LOCALAPPDATA%\ArchitectureDecisionAssistant\workspace\projects` por
padrão. Para configurar outro workspace local, defina `Workspace:RootPath`
(por exemplo, `Workspace__RootPath` no ambiente). Não exponha o host a uma
rede: autenticação e isolamento multiusuário ainda não fazem parte do MVP.
Snapshots imutáveis de análise ficam em
`<workspace>\analyses\<projectId>\<analysisId>.json`; análises antigas são
mantidas e podem ser identificadas como desatualizadas por revisão/hash do
Projeto, do catálogo, das regras e do avaliador.
Diagramas semânticos e seus layouts de apresentação ficam em pastas JSON
separadas dentro de `<workspace>\diagrams`; ADRs versionados ficam em
`<workspace>\adrs`; snapshots históricos opt-in, registros e índice ficam em
`<workspace>\history`. Uma política de comparação histórica é opcional e, se
curada, fica em `<workspace>\policies\history-similarity`. Nenhum banco ou
serviço de IA é usado.

O catálogo inicial contém exemplos de arquiteturas, patterns, tecnologias e
regras. Para estendê-lo sem recompilar, coloque um arquivo JSON por item em
`%LOCALAPPDATA%\ArchitectureDecisionAssistant\workspace\knowledge` (ou no
subdiretório `knowledge` do workspace configurado). Os itens devem seguir
[`knowledge-item.schema.json`](schemas/knowledge-item.schema.json), usar IDs
únicos e apontar apenas para IDs existentes. O catálogo inteiro é validado
antes de ser exibido; JSON inválido, ID duplicado ou referência quebrada
impede a publicação do conjunto, com diagnóstico visível na interface. O
conteúdo é declarativo e não executa código. Recarregue a página para ler
alterações feitas nos arquivos durante a execução.

Execute os testes com:

```powershell
dotnet test ArchitectureAssistant.sln
```