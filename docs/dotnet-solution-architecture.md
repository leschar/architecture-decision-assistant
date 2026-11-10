# Arquitetura da Solução .NET

## 1. Objetivo e decisões

Esta especificação descreve a solução .NET da plataforma. A decisão aprovada
para iniciar a implementação é .NET 10 LTS, Blazor Server self-hosted local,
MediatR quando útil, FluentValidation e Serilog. A aplicação lê e grava
somente no workspace local configurado no host; o navegador não recebe acesso
ao sistema de arquivos do computador.

Decisões:

- `ArchitectureAssistant.Domain` contém modelo e invariantes; não depende de
  framework de UI, infraestrutura, arquivos ou biblioteca de aplicação.
- `ArchitectureAssistant.Application` expõe casos de uso e portas; depende do
  Domain e contém validação/orquestração.
- `ArchitectureAssistant.Infrastructure` implementa arquivos JSON, catálogo,
  validação de schemas e demais adaptadores; depende de Application/Domain.
- `ArchitectureAssistant.Web` é a composição e apresentação Blazor; chama
  Application e registra Infrastructure, sem implementar regra de negócio.
- `ArchitectureAssistant.KnowledgeBase` distribui conteúdo JSON inicial como
  dados, sem classes de domínio, executáveis, scripts ou DSL com código.
- O catálogo inicial reúne 11 itens; extensões são descobertas em
  `<workspace>/knowledge`, validadas pelo schema de conhecimento e publicadas
  atomicamente com hash do conteúdo. A interface consulta e filtra esses itens
  via Application; detalhes e limites estão em
  [knowledge-model.md](./knowledge-model.md).
- O MVP é self-hosted para um usuário local. `Workspace:RootPath` configura a
  raiz do workspace; sem configuração, usar
  `%LOCALAPPDATA%/ArchitectureDecisionAssistant/workspace`. Infrastructure
  resolve e confina caminhos a essa raiz. Não expor a aplicação em rede ou
  habilitar multiusuário implicitamente.
- Não há projeto de banco, ORM, API externa ou execução generativa.
- CQRS e MediatR são usados onde ajudam a delimitar caso de uso; não se cria
  uma hierarquia de command/query para cada operação trivial.
- Resultados previstos de negócio usam Result Pattern; exceções ficam para
  falhas técnicas inesperadas e não são mecanismo de fluxo de domínio.

## 2. Estrutura proposta

```text
ArchitectureDecisionAssistant/
|-- ArchitectureAssistant.sln
|-- global.json
|-- Directory.Build.props
|-- Directory.Packages.props
|-- .editorconfig
|-- README.md
|-- docs/
|-- schemas/
|   `-- project.schema.json
|-- src/
|   |-- ArchitectureAssistant.Domain/
|   |   |-- ArchitectureAssistant.Domain.csproj
|   |   |-- Common/Result.cs
|   |   |-- Projects/
|   |   |   |-- Project.cs
|   |   |   |-- Requirement.cs
|   |   |   |-- ArchitecturalDecision.cs
|   |   |   `-- ValueObjects/
|   |   |-- Analysis/
|   |   |-- Knowledge/
|   |   |-- Diagrams/
|   |   |-- Adr/
|   |   `-- History/
|   |-- ArchitectureAssistant.Application/
|   |   |-- ArchitectureAssistant.Application.csproj
|   |   |-- Abstractions/
|   |   |   |-- Persistence/
|   |   |   |-- Knowledge/
|   |   |   `-- Time/
|   |   |-- Common/
|   |   |   |-- Results/
|   |   |   `-- Behaviors/
|   |   |-- Projects/
|   |   |   |-- Commands/
|   |   |   |-- Queries/
|   |   |   `-- Validators/
|   |   |-- Analysis/
|   |   |-- Requirements/
|   |   |-- Interpretation/
|   |   |-- Questionnaire/
|   |   |-- Analysis/
|   |   |-- KnowledgeGraph/
|   |   |-- Diagrams/
|   |   |-- Decisions/
|   |   |-- Adr/
|   |   |-- History/
|   |   `-- DependencyInjection.cs
|   |-- ArchitectureAssistant.Infrastructure/
|   |   |-- ArchitectureAssistant.Infrastructure.csproj
|   |   |-- Persistence/Json/
|   |   |   `-- JsonAnalysisRepository.cs
|   |   |-- Knowledge/Json/
|   |   |-- Validation/JsonSchema/
|   |   |-- FileSystem/
|   |   |-- Time/
|   |   `-- DependencyInjection.cs
|   |-- ArchitectureAssistant.KnowledgeBase/
|   |   |-- ArchitectureAssistant.KnowledgeBase.csproj
|   |   |-- architectures/
|   |   |-- patterns/
|   |   |-- technologies/
|   |   |-- rules/
|   |   |-- questionnaires/
|   |   `-- lexical/
|   `-- ArchitectureAssistant.Web/
|       |-- ArchitectureAssistant.Web.csproj
|       |-- Program.cs
|       |-- appsettings.json
|       |-- Components/
|       |   |-- App.razor
|       |   |-- Routes.razor
|       |   |-- Layout/
|       |   |-- Pages/
|       |   `-- Shared/
|       |-- wwwroot/
|       `-- Properties/
`-- tests/
    |-- ArchitectureAssistant.Domain.Tests/
    |-- ArchitectureAssistant.Application.Tests/
    |-- ArchitectureAssistant.Infrastructure.Tests/
    `-- ArchitectureAssistant.Web.Tests/
```

Pastas são agrupamentos por responsabilidade/caso de uso, não comandos para
criar tipos vazios. `Adr` e `History` só passam a conter funcionalidade quando
seus casos de uso forem implementados. Tipos e nomes finais devem acompanhar
as etapas de domínio; a árvore é proposta de organização, não contrato público.

## 3. Responsabilidades dos projetos

### ArchitectureAssistant.Domain

- Agregados e invariantes de Projeto, Requisito e Decisão.
- Value objects para IDs, versões, estados e proveniência.
- Semântica determinística de avaliação, grafo, interpretação e projeções de
  diagrama conforme os limites definidos nas especificações.
- Operações de domínio retornam `Result` tipado para recusas/invariantes
  esperadas.
- Não referencia MediatR, FluentValidation, Serilog, Blazor, System.IO,
  schemas físicos nem Infrastructure.

Evitar criar um "Shared Kernel" ou framework de domínio antes de haver
vocabulário e invariantes compartilhados comprovados.

### ArchitectureAssistant.Application

- Casos de uso de comandos e consultas, agrupados por capacidade.
- Orquestra domínio e dependências por interfaces próprias (ports).
- Valida entrada do caso de uso com FluentValidation; validação de UI não
  substitui esta fronteira.
- Usa MediatR para despacho/pipeline onde trouxer composição/testabilidade.
- Retorna resultados tipados e diagnósticos; traduz falhas técnicas sem
  esconder causa operacional.
- Define DTOs/view models de aplicação; não expõe entidades como contrato de
  transporte da UI.
- Não referencia Web ou Infrastructure.

### ArchitectureAssistant.Infrastructure

- Implementa portas de Projeto/artefatos com JSON e diretórios locais.
- Carrega e valida conjunto completo de catálogo/schema; conteúdo inválido
  não é publicado parcialmente.
- Implementa escrita segura, detecção de revisão concorrente, fingerprints,
  snapshots e resolução confinada ao workspace.
- Serializa JSON com comportamento/versionamento explícitos e valida o
  contrato inicial do Projeto com JSON Schema Draft 2020-12.
- Não decide regra de negócio nem seleciona alternativa.

### ArchitectureAssistant.KnowledgeBase

- Projeto de conteúdo, sem código de domínio.
- Distribui exemplos iniciais de JSON e schemas licenciados/curados.
- Pode copiar os dados de exemplo à saída/publicação como arquivos de conteúdo.
- A aplicação também lê diretório de conhecimento externo configurado em
  runtime para permitir extensão sem recompilar.
- Nova extensão só usa tipos e operadores suportados; dados não executam
  código, expressão arbitrária ou assembly.

### ArchitectureAssistant.Web

- Blazor Server (ASP.NET Core Web App com modo de renderização interativo
  Server) apresenta páginas e coleta intenção.
- Composição de dependências/host e configuração de Serilog ocorrem na borda.
- Chamadas às funcionalidades passam por Application; componentes não abrem
  arquivos, percorrem o grafo ou calculam scores.
- Estado local da UI limita-se a filtros, foco, abas e rascunhos transitórios.
- Exibe Result/erros, estados desconhecidos/incompletos e proveniência com
  acessibilidade.
- Não referencia Infrastructure a partir de componentes; referência a esse
  projeto fica restrita à composição/registro na raiz do host.

### Projetos de teste

- **Domain.Tests:** invariantes, transições, Result Pattern e determinismo.
- **Application.Tests:** handlers, validações, orquestração, erros e contratos
  de ports com doubles.
- **Infrastructure.Tests:** arquivos temporários, schemas, round-trip,
  concorrência, escritas interrompidas, path traversal e integridade de índice.
- **Web.Tests:** navegação, binding, acessibilidade e encaminhamento de ações;
  não devem reimplementar ou substituir testes de regra.

Os projetos de teste não entram em dependências de produção. Framework e
bibliotecas de teste serão selecionados no momento de criar a solução e
versionados centralmente.

## 4. Grafo de dependências

```text
ArchitectureAssistant.Domain
            ^
            |
ArchitectureAssistant.Application
          ^       ^
          |       |
Infrastructure   Web
          ^       |
          +-------+  (somente registro/composição no host)

KnowledgeBase -- arquivos JSON de conteúdo --> Infrastructure carrega em runtime
```

Dependências permitidas:

| Projeto | Pode referenciar |
|---|---|
| Domain | BCL; sem dependências de projetos |
| Application | Domain; MediatR, FluentValidation e abstrações mínimas necessárias |
| Infrastructure | Application, Domain e adaptadores técnicos |
| KnowledgeBase | SDK/data build tooling; nenhum projeto de camadas |
| Web | Application; Infrastructure somente no composition root |
| Tests | Projeto sob teste e ferramentas de teste |

Nenhuma referência invertida, ciclo ou atalho UI → repositório está permitido.
Domain não conhece tipo de request do MediatR; requests e handlers são
Application.

## 5. Persistência, configuração e observabilidade

- Persistência inicial exclusivamente em JSON local; sem EF Core/ORM, SQL ou
  banco embutido.
- Snapshots imutáveis de análise ficam em
  `<workspace>/analyses/<projectId>/<analysisId>.json`; usam schema versionado
  separado do documento do Projeto e não são apagados ao atualizar Projeto ou
  catálogo.
- Infrastructure recebe a raiz local do workspace por configuração do host;
  domínio e componente Blazor não constroem paths.
- Padrão do MVP: `%LOCALAPPDATA%/ArchitectureDecisionAssistant/workspace`.
  O host escuta em loopback e não deve ser exposto a outros usuários/rede sem
  novo desenho de identidade e isolamento.
- Projeto, catálogo e resultados imutáveis preservam IDs, revisões e hashes;
  escrita concorrente não sobrescreve revisão divergente silenciosamente.
- A estratégia de arquivo temporário + substituição atômica e recuperação é
  definida/testada na implementação de Infrastructure, sem alegar
  atomicidade universal entre sistemas de arquivos.
- Serilog é configurado no host Web, com eventos estruturados e correlação.
  Logs não incluem texto livre, conteúdo integral de projeto ou credenciais por
  padrão.
- Configuração e segredos não ficam em catálogo JSON versionado; segredos de
  produção usam mecanismo de configuração apropriado ao ambiente.
- O MVP é self-hosted local para um usuário; o navegador não recebe acesso ao
  disco local. Acesso remoto/multiusuário depende de revisão de identidade,
  autorização e isolamento.

## 6. Validação e tratamento de erros

1. FluentValidation valida formato/combinações de entrada do caso de uso.
2. Domain protege invariantes mesmo quando chamado sem UI.
3. Infrastructure valida sintaxe, Draft 2020-12, semântica de referências e
   integridade de arquivos/catalog.
4. Resultado previsto de negócio retorna `Result` com código estável e
   mensagem/contexto apropriados à aplicação.
5. Falha inesperada usa exceção, logging/correlation e fronteira de erro; não
   converter falha em `Result.Success` vazio.
6. A UI apresenta mensagem acionável sem stack trace e preserva rascunho após
   falha.

FluentValidation não é executado pelo Domain e exceção não substitui Result
Pattern para recusas de negócio. Serilog não determina resposta de domínio.

## 7. Pontos de risco e decisões em aberto

### Target e suporte do .NET

A decisão aprovada para a implementação é `net10.0` (.NET 10 LTS), em vez do
requisito inicial .NET 9, cujo suporte termina em 10 de novembro de 2026.
Fixar SDK/runtime e manter a versão revisada antes de releases produtivos.
Mudanças futuras de target devem ser explícitas, validadas e refletidas nos
projetos e nesta especificação.

Fonte: [política oficial de suporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

### Outros riscos

- O validador JSON Schema escolhido precisa suportar Draft 2020-12 e referências
  locais/entre arquivos.
- A escrita JSON e o lock otimista precisam ser testados sob falha de processo,
  concorrência e ambientes Windows.
- O host Blazor Server mantém circuitos/conexões; o MVP self-hosted não se
  destina a acesso de rede ou uso multiusuário. Exposição remota exige nova
  avaliação de identidade, autorização e isolamento de workspace.
- Versões específicas de MediatR, FluentValidation e Serilog, licenças e
  compatibilidade devem ser confirmadas antes de alterar manifests.
- Autenticação, autorização, backup, criptografia e compartilhamento são fora
  do escopo atual e não devem ser presumidos pelo diagrama.

## 8. Critérios de aceite para a etapa de especificação

1. Responsabilidades e dependências da tabela/árvore não formam ciclos.
2. Nenhuma regra de negócio reside em Web e nenhuma camada interna depende de
   Blazor, Infrastructure ou sistema de arquivos.
3. KnowledgeBase distribui dados não executáveis; catálogo externo continua
   extensível sem recompilar.
4. Persistência não introduz banco ou ORM.
5. MediatR/FluentValidation/Serilog são confinados às responsabilidades
   descritas; CQRS não é obrigatório para cada operação.
6. Erros previstos de domínio usam Result Pattern; erros técnicos não são
   mascarados como sucesso.
7. Testes cobrem Domain, Application, Infrastructure e UI sem adicionar
   referência de produção a testes.
8. A solução compila para `net10.0`; SDK/runtime e decisão de target são
   documentados e revistos antes de releases produtivos.

## 9. Alternativas e trade-offs

| Decisão | Benefício | Custo / risco | Alternativa |
|---|---|---|---|
| Blazor Server com Interactive Server | Stack .NET e domínio centralizado no host | Circuito depende de conexão e requer capacidade do servidor | WebAssembly reduz estado no servidor, mas muda fronteira de execução/segurança |
| KnowledgeBase como conteúdo separado | Curadoria/distribuição clara, conteúdo JSON não executável | É preciso definir diretório externo e política de publicação | Compilar catálogo no código viola extensibilidade |
| Arquivos JSON e ports substituíveis | Portabilidade e aderência às restrições atuais | Concorrência, busca e recuperação exigem trabalho explícito | Banco facilita consultas mas não é permitido inicialmente |
| MediatR para casos de uso úteis | Pipeline/teste e fronteiras claras | Dependência e indireção adicionais | Chamada direta de serviços é mais simples para operações triviais |
| .NET 10 LTS aprovado para a implementação | Target suportado para um novo início de desenvolvimento | Diverge do requisito inicial .NET 9 e requer sincronização dos documentos | .NET 9 foi rejeitado para o início da implementação por estar no fim do suporte |
