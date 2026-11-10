# Backlog Completo do Produto

## 1. Objetivo e autoridade

Este documento organiza o trabalho futuro do Architecture Decision Assistant
em épicos, features, histórias de usuário, tarefas técnicas, critérios de
aceite, prioridades, complexidade, riscos e dependências. É uma especificação
de produto, não um compromisso de prazo nem autorização para iniciar
implementação.

As fases abaixo seguem a organização solicitada na etapa 13. Os contratos
normativos de domínio, experiência, arquivos e arquitetura permanecem nas
especificações vinculadas no [README.md](../README.md). Em caso de divergência,
uma mudança de backlog não altera silenciosamente um contrato: a especificação
afetada deve ser revista e versionada.

### Fontes normativas para refinamento

- [Visão do produto](./project-vision.md) e [modelo de domínio](./domain-model.md);
- [modelo de conhecimento](./knowledge-model.md) e
  [motor de regras](./rule-engine.md);
- [questionário arquitetural](./architectural-questionnaire.md) e
  [interpretação determinística de requisitos](./requirement-text-interpretation.md);
- [grafo de conhecimento](./graph-dependency-model.md) e
  [geração de diagramas](./diagram-generation.md);
- [experiência da aplicação](./application-experience.md);
- [geração e gestão de ADRs](./adr-generation.md);
- [Architectural Knowledge History](./architectural-knowledge-history.md);
- [arquitetura da solução .NET](./dotnet-solution-architecture.md).

## 2. Guardrails e convenções

### Guardrails obrigatórios

- Clean Architecture e DDD; regras de negócio em Domain/Application, nunca em
  componentes Blazor.
- Persistência inicial exclusivamente em arquivos JSON locais. Não adicionar
  banco, ORM ou serviço externo sem revisão explícita das restrições do produto.
- Não usar IA generativa, OpenAI, Azure OpenAI, embeddings ou inferência
  generativa em runtime. Regras, grafo, interpretação lexical e histórico são
  determinísticos.
- Catálogo, regras, questionário e vocabulário são conteúdo declarativo
  versionado, validado e extensível sem recompilação dentro dos contratos
  suportados. JSON não executa código, expressões arbitrárias ou scripts.
- Recomendações, diagramas, histórico e ADRs apoiam a decisão humana; não
  escolhem, confirmam ou alteram uma decisão sem ação explícita.
- Usar FluentValidation na fronteira de casos de uso e Result Pattern para
  resultados previstos de negócio. Falhas técnicas devem ser diagnosticadas,
  registradas com segurança e nunca convertidas em sucesso vazio.
- `Unknown`, incompleto, inválido, não avaliado e desatualizado são estados
  explícitos; não equivalem a `false`, zero, ausência de risco ou sucesso.
- Novos requisitos corporativos (compartilhamento, identidade, governança ou
  persistência centralizada) exigem decisão de produto, avaliação de risco e
  atualização da arquitetura antes de serem tratados como escopo aprovado.

### Prioridade

| Código | Significado |
|---|---|
| P0 | Obrigatório para a fase e para seu valor principal; bloqueia aceite da fase. |
| P1 | Importante para uso confiável; pode seguir após o caminho principal da fase. |
| P2 | Desejável, condicional ou dependente de validação; não bloqueia a fase. |

Prioridade é relativa ao backlog, não uma sequência de entrega por si só.

### Complexidade

Estimativa relativa de esforço/risco técnico: **S** (pequena), **M** (média),
**L** (grande), **XL** (muito grande). Não representa duração. Histórias XL
devem ser divididas durante refinamento, preservando seus critérios de aceite.

### Identificadores

Épicos `E<n>`, features `F<n>.<n>`, histórias `US<n>.<n>` e tarefas técnicas
`T<n>.<n>`. Dependências listadas são bloqueios ou pré-requisitos reais, não
uma inferência baseada apenas na ordem visual. Histórias sem dependência
registrada podem ser planejadas independentemente dentro das restrições da
fase.

## 3. Mapa de fases e dependências

| Fase | Resultado verificável | Dependências principais |
|---|---|---|
| 1 — MVP | Fluxo local de projeto, requisitos, catálogo inicial, análise básica explicável, decisão humana e exportação JSON. | Target .NET 10 LTS e self-hosted local aprovados; fechar schema e persistência JSON antes de completar o fluxo. |
| 2 — Motor de Regras | DSL declarativa extensível, avaliação tri-state, efeitos, pontuação/cobertura e explicações reproduzíveis. | Fase 1; schemas e registro de fatos tipados. |
| 3 — Diagramas | Grafo navegável e snapshots C4 derivados de estrutura explícita, com proveniência e lacunas. | Catálogo/relacionamentos da Fase 1; avaliação e semântica da Fase 2; decisões/modelo estrutural confirmados. |
| 4 — ADR | ADR JSON versionado derivado de decisão humana, revisável e exportável em Markdown. | Decisões e persistência da Fase 1; contratos de fonte e revisão do Projeto. |
| 5 — Conhecimento Histórico | Arquivamento local opt-in, busca/comparação determinística e reuso explícito de decisões. | Formato do Projeto e hashes da Fase 1; política de comparação aprovada; Fases 2 e 4 quando usados dados de análise/ADR. |
| 6 — Plataforma Enterprise | Governança, operação e colaboração somente após requisitos empresariais aprovados; nenhuma mudança implícita de persistência. | Descoberta e aprovação de requisitos de implantação, identidade, privacidade, retenção e operação. |

As Fases 3, 4 e 5 podem ser executadas em sequência ou em trilhas distintas
após seus pré-requisitos. A Fase 6 não é um destino automático de migração
para banco ou nuvem.

## 4. Fase 1 — MVP

### E1 — Projeto local e fundação da solução

**Objetivo:** permitir que uma pessoa mantenha um projeto arquitetural
portável, com responsabilidades de camada e contrato de arquivo explícitos.

#### F1.1 — Ciclo de vida e persistência de Projeto

**US1.1 — Criar, abrir e salvar Projeto**

Como arquiteto, quero criar, abrir e salvar um Projeto local para continuar
meu trabalho sem depender de banco de dados.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T1.1.1 Implementar e testar o schema inicial
    [project.schema.json](../schemas/project.schema.json), IDs, revisões,
    referências aos artefatos e política explícita de compatibilidade/migração.
  - T1.1.2 Usar workspace self-hosted local configurável no host; documentar
    diretório padrão, descoberta, importação/exportação e confinar paths.
  - T1.1.3 Definir ports de persistência e adaptador JSON em Infrastructure,
    incluindo leitura, validação, revisão otimista e gravação segura.
  - T1.1.4 Definir Composition Root, tratamento de erro/correlação e contratos
    Result sem dependência de Infrastructure na UI.
- **Critérios de aceite:**
  1. Criar e reabrir um Projeto preserva IDs, contexto, requisitos e decisões
     válidos.
  2. Documento inválido, versão incompatível, arquivo ausente e falha de
     leitura/gravação retornam diagnóstico explícito; não há sucesso vazio.
  3. Revisão concorrente divergente não sobrescreve silenciosamente o arquivo
     mais novo e preserva o rascunho do usuário.
  4. Leitura e gravação passam pela Application/ports; Web não acessa arquivos
     nem aplica invariantes.
  5. JSON exportado é validável e reimportável na mesma versão de contrato.
- **Dependências:** nenhuma história funcional; modo self-hosted local está
  aprovado. Fechar schema do Projeto antes de persistência definitiva.
- **Riscos:** R1, R2, R3.

#### F1.2 — Contexto e requisitos explícitos

**US1.2 — Registrar e revisar contexto e requisitos**

Como arquiteto ou tech lead, quero registrar contexto e requisitos com origem e
estado explícitos para preparar uma análise sem transformar lacunas em fatos.

- **Prioridade:** P0; **complexidade:** M.
- **Tarefas técnicas:**
  - T1.2.1 Modelar comandos/consultas de Projeto e Requisito e suas invariantes
    no domínio.
  - T1.2.2 Validar entradas de casos de uso com FluentValidation e resultados
    de domínio com Result Pattern.
  - T1.2.3 Implementar formulários/listas Blazor para contexto e CRUD de
    requisitos, com estados salvos, não salvos e erro.
- **Critérios de aceite:**
  1. Requisitos registram descrição, classificação, prioridade quando
     conhecida, origem e estado de confirmação.
  2. Alterar contexto/requisito invalida a atualidade de resultados derivados
     sem apagar o resultado anterior.
  3. Valor, unidade, cenário ou horizonte ausentes permanecem desconhecidos;
     a UI não os completa.
  4. Ações inválidas produzem resultado acionável, preservando outros dados
     preenchidos.
- **Dependências:** US1.1.
- **Riscos:** R1, R4.

### E2 — Catálogo inicial de conhecimento

#### F2.1 — Carregar, validar e consultar conteúdo JSON

**US1.3 — Explorar conhecimento arquitetural**

Como arquiteto, quero consultar itens de conhecimento versionados e validados
para inspecionar alternativas sem cadastrar manualmente cada definição.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T1.3.1 Definir configuração de catálogo inicial e diretório externo de
    extensões JSON, sem acoplá-los aos componentes.
  - T1.3.2 Implementar validação de sintaxe, schema suportado, IDs duplicados,
    tipos e referências; selecionar validador compatível com Draft 2020-12.
  - T1.3.3 Publicar snapshot completo somente quando válido e registrar
    versões/hash do conteúdo carregado.
  - T1.3.4 Curar e revisar exemplos iniciais de arquitetura, tecnologia e
    patterns; identificar fontes e limites editoriais.
  - T1.3.5 Apresentar busca/filtro e detalhes de itens na UI sem regras de
    domínio no componente.
- **Critérios de aceite:**
  1. Catálogo válido exibe tipo, versão, descrição e benefícios/limitações
     definidos pelo contrato.
  2. Um item JSON novo válido dentro do schema suportado pode ser descoberto
     sem recompilar.
  3. ID duplicado, referência inválida ou schema incorreto identifica arquivo
     e campo; catálogo inválido não aparece como parcialmente carregado.
  4. Conhecimento e regras permanecem dados não executáveis.
- **Dependências:** US1.1.
- **Riscos:** R2, R5.

### E3 — Análise básica e decisão humana

#### F3.1 — Análise inicial limitada e explicável

**US1.4 — Executar análise básica**

Como arquiteto, quero avaliar requisitos confirmados contra um conjunto
pequeno de regras determinísticas para obter candidatos iniciais sem decisão
automática.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T1.4.1 Definir o subconjunto mínimo e versionado de regras do MVP, os fatos
    suportados e as limitações antes da implementação.
  - T1.4.2 Capturar retrato consistente do Projeto e versões/hash do catálogo,
    regras e avaliador.
  - T1.4.3 Implementar serviço de domínio da análise básica e persistência de
    resultado imutável conforme o contrato aprovado.
  - T1.4.4 Expor caso de uso de análise e apresentar candidatos separados por
    tipo, evidências e lacunas.
- **Critérios de aceite:**
  1. A mesma entrada e as mesmas versões produzem resultado equivalente e
     ordenação estável.
  2. Somente fatos confirmados definidos pelo subconjunto participam; ausência
     de fato não vira falso, score neutro ou restrição satisfeita.
  3. Cada resultado identifica critérios e fontes realmente usados, bem como
     avisos e limites do MVP.
  4. Arquiteturas, patterns e tecnologias não são colocados num único ranking.
  5. Atualizar Projeto ou catálogo marca a análise antiga como desatualizada,
     sem reescrevê-la.
- **Subconjunto implementado na Fase 1:** condições com um único predicado
  `project.teamSize LessThanOrEqual <inteiro>`, dentro de `allOf` unitário;
  alvos por tipo, IDs e tags; efeitos `Score` e `Flag`. Regras habilitadas fora
  desse formato são reportadas como não avaliadas. Texto de requisitos,
  relações e efeitos `Exclude` não são interpretados nessa fase. Esse limite
  permite consumir regras declarativas do catálogo sem implementar a DSL da
  Fase 2.
- **Dependências:** US1.2, US1.3.
- **Riscos:** R4, R5.

#### F3.2 — Comparar e registrar escolha humana

**US1.5 — Comparar opções e registrar decisão**

Como arquiteto, quero comparar candidatos e registrar minha própria escolha,
justificativa e alternativas consideradas para preservar a responsabilidade
humana e a rastreabilidade.

- **Prioridade:** P0; **complexidade:** M.
- **Tarefas técnicas:**
  - T1.5.1 Definir estados/transições da decisão e referências à análise,
    alternativas e proveniência. **Implementada:** `Proposed` transita para
    `Accepted`, `Rejected` ou `Superseded`; `Accepted` pode ser substituída;
    `Rejected` e `Superseded` são terminais. Transições guardam autoria/data e
    incrementam revisão, mantendo histórico no JSON. A decisão pode referenciar
    análise e versão escolhida.
  - T1.5.2 Implementar casos de uso para registrar, revisar e rejeitar decisão.
    **Implementada:** comandos MediatR validam candidatos e alternativas contra
    análise/snapshot ou catálogo; aceitar/rejeitar é explícito; revisão cria
    nova proposta e marca a anterior como substituída.
  - T1.5.3 Criar comparação lado a lado limitada a candidatos comparáveis e
    ações explícitas de decisão. **Implementada:** comparação só aceita dois
    candidatos do mesmo tipo na mesma análise; exibe métricas individuais,
    relações, dependências e limitações sem agregar scores.
- **Critérios de aceite:**
  1. Selecionar ou abrir candidato na UI não cria decisão.
  2. A decisão registra escolha e autoria humana; edição preserva revisão ou
     histórico identificável.
  3. A comparação mantém ausências visíveis e não agrega escalas/tipos
     incompatíveis.
  4. Resultado de recomendação não altera a decisão nem o Projeto sem comando
     explícito.
- **Dependências:** US1.1, US1.4.
- **Riscos:** R4.
- **Estado do recorte:** US1.5 está implementada em Domain, Application,
  Infrastructure e Web. A consulta de comparação de grafo de T3.2.3 continua
  sendo separada, somente leitura e sem agregação de scores.

### E4 — Experiência mínima e qualidade

#### F4.1 — Navegação, estados e acessibilidade do fluxo MVP

**US1.6 — Entender estado e limitações do trabalho**

Como usuário, quero distinguir estados de salvamento, validação, análise e
lacunas para não interpretar erro ou dado desconhecido como sucesso.

- **Prioridade:** P0; **complexidade:** M.
- **Tarefas técnicas:**
  - T1.6.1 Implementar navegação por tarefa, resumo do Projeto e indicação de
    revisão/estado.
  - T1.6.2 Tratar carregando, vazio, inválido, stale, conflito e falha com
    mensagens acessíveis e sem fallback de sucesso.
  - T1.6.3 Adicionar testes de integração/contrato entre Web, Application,
    Domain e Infrastructure e testes de teclado/estados essenciais.
- **Critérios de aceite:**
  1. Usuário consegue concluir o fluxo de criação, captura, análise, decisão,
     salvamento e reabertura sem banco ou serviço externo.
  2. Nenhuma regra necessária de negócio reside em componente Blazor.
  3. Estados não dependem apenas de cor ou ícone; fluxos essenciais podem ser
     usados por teclado.
  4. Falha de gravação/conflito mantém o trabalho não salvo e não anuncia
     sucesso.
- **Dependências:** US1.1, US1.2, US1.4, US1.5.
- **Riscos:** R1, R3, R6.

## 5. Fase 2 — Motor de Regras

### E5 — DSL e semântica determinística

#### F5.1 — Validar regras e registro de fatos

**US2.1 — Estender regras como dados**

Como curador de conhecimento, quero adicionar regras declarativas dentro de
uma DSL limitada para ampliar a análise sem recompilar nem executar código
arbitrário.

- **Prioridade:** P0; **complexidade:** XL.
- **Tarefas técnicas:**
  - T2.1.1 Formalizar versão do schema, catálogo de fatos tipados, operadores,
    tipos, limites de tamanho/profundidade e compatibilidade.
  - T2.1.2 Validar `appliesTo`, `appliesWhen`, `conditions` e `effects`,
    referências e versões antes de ativar regras.
  - T2.1.3 Implementar parser/avaliador fechado para `allOf`, `anyOf`, `not`
    e operadores suportados; rejeitar expressão executável.
  - T2.1.4 Ampliar arquivos de regras e matriz de testes para efeitos e
    resultados conhecidos/desconhecidos.
- **Critérios de aceite:**
  1. Regras válidas novas são carregadas sem recompilar dentro do contrato
     suportado.
  2. Operador, fato, tipo ou campo não suportado é diagnóstico acionável; regra
     inválida não contribui parcialmente.
  3. Nenhum conteúdo JSON executa script, reflexão, I/O, método ou expressão
     arbitrária.
  4. Conteúdo ativo é publicado como conjunto consistente, com versões/hash
     rastreáveis.
- **Dependências:** US1.3, US1.4.
- **Riscos:** R2, R5, R7.

#### F5.2 — Lógica tri-state e efeitos da regra

**US2.2 — Preservar incerteza na avaliação**

Como arquiteto, quero que fatos ausentes e condições inconclusivas sejam
representados como `Unknown` para que candidatos não sejam falsamente
favorecidos ou excluídos.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T2.2.1 Implementar semântica tri-state para operadores e grupos lógicos.
  - T2.2.2 Implementar `Score`, `Exclude` rígido e `Flag` com evidências
    tipadas.
  - T2.2.3 Distinguir inelegível, não avaliado, elegível e sem evidência.
  - T2.2.4 Cobrir tabelas de verdade, ausência, negação e erro de tipo em
    testes de domínio.
- **Critérios de aceite:**
  1. Fato ausente resulta em `Unknown`; não é convertido em `False`, zero ou
     valor padrão.
  2. `Unknown` não dispara score, exclusão nem flag cujo predicado não foi
     confirmado.
  3. Apenas uma restrição rígida explicitamente declarada e avaliada como
     verdadeira exclui candidato.
  4. Cada efeito guarda regra/versão, condição, justificativa e fato(s)
     relevantes.
- **Dependências:** US2.1.
- **Riscos:** R4, R7.

### E6 — Explicabilidade, score e reprodutibilidade

#### F6.1 — Score/cobertura e ordenação comparável

**US2.3 — Inspecionar adequação sem falsa precisão**

Como arquiteto, quero ver score, cobertura e regras avaliadas separadamente
para interpretar evidência sem confundi-la com probabilidade de sucesso.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T2.3.1 Implementar cálculo e limites documentados de `fit`, cobertura e
    casos sem denominador/sem regras avaliadas.
  - T2.3.2 Implementar ordenação estável por tipo, elegibilidade, score,
    cobertura e ID; manter não avaliados e excluídos visíveis.
  - T2.3.3 Expor DTOs/view models de evidências, contribuições, desconhecidos e
    versões na Application.
  - T2.3.4 Atualizar tela de análise/comparação e testes de regressão.
- **Critérios de aceite:**
  1. Score ausente aparece como “Não avaliado”, nunca como 50 ou zero.
  2. Cobertura é exibida separada do score e não é descrita como probabilidade.
  3. Ordenação e cálculo repetidos com mesmas entradas e versões são
     determinísticos.
  4. A escala só é comparada dentro do mesmo tipo, perfil e versões compatíveis.
  5. Evidências favoráveis, desfavoráveis, regras não correspondidas,
     desconhecidos, riscos e limitações são inspecionáveis.
- **Dependências:** US2.2.
- **Riscos:** R4, R7.

#### F6.2 — Histórico reproduzível da análise

**US2.4 — Reproduzir a origem de um resultado**

Como arquiteto, quero identificar o retrato do Projeto, as versões das regras,
do catálogo e do motor que produziram uma análise para auditar resultados
anteriores.

- **Prioridade:** P1; **complexidade:** L.
- **Tarefas técnicas:**
  - T2.4.1 Definir fingerprint canônico, versão do avaliador e formato do
    snapshot de análise.
  - T2.4.2 Persistir resultados imutáveis e referências necessárias sem
    sobrescrever execução anterior.
  - T2.4.3 Exibir e validar stale, versão indisponível e limitações de
    reprodução.
- **Critérios de aceite:**
  1. Toda análise identifica revisões/hashes e versões efetivamente usados.
  2. Nova execução cria novo resultado; não reescreve análise antiga.
  3. Conteúdo atualizado não altera silenciosamente interpretação anterior.
  4. Versão ausente/incompatível impede alegação de reprodução exata e gera
     diagnóstico.
- **Dependências:** US1.1, US2.3.
- **Riscos:** R2, R3, R7.

## 6. Fase 3 — Diagramas

### E7 — Grafo de conhecimento e análise de relações

#### F7.1 — Validar e projetar o grafo

**US3.1 — Explorar relações tipadas**

Como arquiteto, quero explorar nós de Arquitetura, Pattern e Technology e suas
relações tipadas para entender dependências, combinações, conflitos e
alternativas.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T3.1.1 Validar tipos de endpoint, relações duplicadas, auto-arestas,
    condições e ciclos de `DependsOn`.
  - T3.1.2 Construir projeção estável identificada por catálogo/hash e
    `relationKey`.
  - T3.1.3 Implementar navegação visual/textual acessível, sem mutação de
    seleção ou score por interação.
- **Critérios de aceite:**
  1. A projeção contém somente nós e relações permitidos pelo contrato e
     preserva versão/proveniência.
  2. `DependsOn` é direcionada; conflitos e alternativas são navegáveis dos
     dois lados sem duplicação canônica.
  3. `Recommends` não é apresentado como dependência obrigatória nem como
     recomendação do motor.
  4. Erro de referência ou ciclo de dependência produz diagnóstico, não
     carregamento parcial silencioso.
  5. Navegar/selecionar visualmente um nó não registra decisão nem altera score.
- **Estado do recorte:** T3.1.3 está implementada na página Grafo de
  conhecimento, com busca/filtro de nós, navegação dos dois lados para relações
  simétricas, direção explícita das relações e proveniência textual. A seleção
  é somente estado de navegação; a UI consome a projeção validada pelo Domain.
- **Dependências:** US1.3, US2.1.
- **Riscos:** R5, R7.

#### F7.2 — Expandir relações no contexto

**US3.2 — Entender dependências e conflitos de uma análise**

Como arquiteto, quero ver relações aplicáveis ao contexto e caminhos de
dependência para avaliar implicações sem instalação ou seleção automática.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T3.2.1 Implementar avaliação tri-state de `appliesWhen` e expansão
    determinística somente de `DependsOn`.
  - T3.2.2 Implementar limites registrados, deduplicação de nós e detecção de
    ciclos/caminhos alternativos.
  - T3.2.3 Integrar dependências, conflitos e alternativas à análise e
    comparação.
- **Critérios de aceite:**
  1. Só relação aplicável expande; `Unknown` gera lacuna e não é tratado como
     verdadeiro ou falso.
  2. `Recommends` não expande recursivamente nem soma score por si só.
  3. Caminhos, estados de seleção, conflitos e limitações ficam visíveis.
  4. Nenhuma relação cria seleção, decisão, instalação ou topologia
     automaticamente.
- **Dependências:** US2.2, US3.1.
- **Riscos:** R4, R7.

**Estado do recorte:** projeção determinística do catálogo, avaliação direta
das quatro relações tipadas e expansão transitiva limitada de `DependsOn` são
persistidas no snapshot 2.0. A consulta de comparação de candidatos expõe
relações entre o par, dependências por raiz, interseção/diferença de requisitos,
scores individuais e limitações de completude, sem agregar scores ou mutar
decisões. A navegação do catálogo está implementada em T3.1.3. A navegação
contextual por projeto e diagramas C4 continuam pendentes; os limites precisam
ser medidos com catálogos representativos. Ver
[Grafo de Conhecimento e Dependências](./graph-dependency-model.md).

### E8 — Modelo estrutural e projeções C4

#### F8.1 — Capturar estrutura explícita

**US3.3 — Registrar elementos e interações arquiteturais**

Como arquiteto, quero registrar pessoas, sistemas, containers, componentes e
interações confirmados para que diagramas reflitam estrutura real, não
inferências de catálogo.

- **Prioridade:** P0; **complexidade:** XL.
- **Tarefas técnicas:**
  - T3.3.1 Definir schema/versionamento para estrutura, IDs estáveis,
    escopos, proveniência e estados Confirmed/Proposed/Unresolved.
  - T3.3.2 Definir e implementar invariantes de nós, endpoints, níveis e tipos
    de interação.
  - T3.3.3 Implementar casos de uso de inclusão/revisão/confirmação de
    estrutura; manter referências a requisito/decisão/fontes.
- **Critérios de aceite:**
  1. IDs não dependem de nome de exibição; conexões têm direção e origem
     explícitas.
  2. Elemento proposto ou não resolvido não é mostrado como confirmado.
  3. Ausência de protocolo, tecnologia, sistema ou componente não recebe valor
     presumido.
  4. Estrutura é persistida em JSON versionado e validada pela Application e
     Infrastructure.
- **Dependências:** US1.1, US1.5.
- **Riscos:** R3, R4, R8.

#### F8.2 — Gerar e salvar diagramas C4

**US3.4 — Gerar preview e snapshot C4**

Como arquiteto, quero gerar preview de Context, Container ou Component a
partir da estrutura explícita e salvar um snapshot identificável para comunicar
uma arquitetura revisável.

- **Prioridade:** P0; **complexidade:** XL.
- **Tarefas técnicas:**
  - T3.4.1 Implementar projeções C4 determinísticas por nível/escopo e
    proveniência por nó/conexão.
  - T3.4.2 Implementar estados incompletos, avisos e coerência entre níveis.
  - T3.4.3 Implementar preview reativo com fingerprint solicitado e descarte de
    respostas obsoletas.
  - T3.4.4 Persistir snapshots explicitamente e separar layout/overrides da
    semântica.
  - T3.4.5 Renderizar alternativa textual/acessível e testar geração, edição de
    layout e regressão.
- **Critérios de aceite:**
  1. Context/Container/Component não misturam níveis; Component ou Container
     sem dados suficientes retorna incompleto e lista lacunas.
  2. Score, recomendação ou nome de tecnologia não cria nó/conexão confirmada.
  3. Cada elemento/conexão tem proveniência e estado; topologia não é
     inventada.
  4. Preview atrasado não substitui revisão mais nova; falha não vira diagrama
     vazio com sucesso.
  5. Layout não modifica semântica; salvar é ação explícita e versionada.
  6. Diagramas são compreensíveis por alternativa textual, teclado e rótulos
     além de cor.
- **Dependências:** US3.1, US3.3; US3.2 quando a projeção utilizar relações
  expandidas.
- **Riscos:** R3, R4, R8.

**Sequenciamento atual:** US1.5, navegação do catálogo (T3.1.3), estrutura
explícita do Projeto (F8.1) e preview textual acessível C4 estão implementados.
O contexto tipado do Projeto inclui alvo opcional de escalabilidade e o fato
`project.scalabilityTarget`; quando ausente, a avaliação permanece `Unknown`.
F8.2 está implementada: layout em JSON separado do snapshot semântico,
edição de posições/fronteiras, aviso de overrides órfãos, atualização reativa
com descarte de respostas concorrentes e lista/abertura de snapshots salvos.
O comando de salvar revalida o fingerprint atual. Testes de contrato e
persistência foram adicionados, mas não executados nesta sessão conforme
solicitado. Fase 6 segue bloqueada pelo gate de aprovação enterprise definido
na seção 9.

## 7. Fase 4 — ADR

### E9 — Ciclo de vida de ADR rastreável

#### F9.1 — Preparar, revisar e versionar ADR JSON

**US4.1 — Gerar rascunho de ADR de decisão humana**

Como arquiteto, quero preparar um ADR a partir de uma decisão registrada e de
fontes identificadas para documentar contexto e consequências sem inventar
fatos.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T4.1.1 Fechar campos/referências entre Projeto, decisão, análise e schema
    ADR; adotar convenção de identificadores.
  - T4.1.2 Implementar composição determinística das seções e estado de
    completude/atribuição/proveniência.
  - T4.1.3 Implementar revisão editorial com preservação de conteúdo do usuário
    e transições de ciclo de vida independentes da decisão.
  - T4.1.4 Implementar validação JSON e integridade de referências entre
    arquivos/artefatos.
- **Critérios de aceite:**
  1. Só é possível gerar ADR vinculado a decisão e revisão existentes.
  2. Seção sem fonte fica `NotProvided` e exige revisão; não recebe prosa
     genérica como fato.
  3. Cada conteúdo indica autoria/fonte e versões; recomendação não escolhe
     decisão.
  4. Regenerar não sobrescreve edição humana sem confirmação explícita e
     revisável.
  5. Aceitar ADR não confirma nem substitui decisão; mudanças preservam
     histórico.
- **Dependências:** US1.5, US1.1.
- **Riscos:** R3, R4, R9.

#### F9.2 — Exportar ADR em Markdown

**US4.2 — Exportar snapshot de ADR**

Como arquiteto, quero exportar o ADR salvo em Markdown para compartilhar uma
visão legível, mantendo o JSON como registro canônico.

- **Prioridade:** P1; **complexidade:** M.
- **Tarefas técnicas:**
  - T4.2.1 Implementar renderização do template a partir de snapshot JSON
    validado, sem execução de template fornecido por dados não confiáveis.
  - T4.2.2 Exibir lacunas, IDs/versões de fontes e estado do documento na
    projeção.
  - T4.2.3 Cobrir round-trip conceitual, exportação, falhas e compatibilidade.
- **Critérios de aceite:**
  1. Markdown representa exatamente a revisão salva e não é fonte de verdade
     concorrente.
  2. Lacunas e proveniência são legíveis; exportação não completa conteúdo
     ausente.
  3. Falha de exportação informa erro e preserva o ADR salvo.
- **Dependências:** US4.1.
- **Riscos:** R3, R9.

**Sequenciamento atual:** F9.1 e F9.2 têm fluxo operacional na aplicação:
rascunho vinculado à decisão/revisão, seções com autoria e fontes, versões
JSON imutáveis, aceite independente e exportação Markdown da revisão salva.
O aceite rejeita lacunas `NotProvided`. A substituição entre ADRs existentes
cria nova revisão imutável do ADR substituído, com referência ao sucessor, sem
alterar a decisão; o sucessor precisa documentar a decisão ligada explicitamente
como substituta no Projeto. Cada decisão recebe um único ADR; revisões editoriais
permanecem no mesmo registro. O Markdown da revisão salva é disponibilizado para
prévia e download no navegador; o JSON permanece canônico. Testes adicionados não
foram executados nesta sessão.

## 8. Fase 5 — Conhecimento Histórico

### E10 — Arquivamento e integridade de snapshots

#### F10.1 — Arquivar e remover projetos anteriores com consentimento

**US5.1 — Arquivar snapshot histórico**

Como arquiteto, quero arquivar explicitamente uma revisão imutável do Projeto
para consultar seu contexto no futuro sem alterar o arquivo de trabalho.

- **Prioridade:** P0; **complexidade:** L.
- **Tarefas técnicas:**
  - T5.1.1 Fechar política de workspace, retenção e estrutura de arquivos
    locais para histórico.
  - T5.1.2 Implementar snapshot, registro, fingerprint e índice JSON
    reconstruível, com paths confinados.
  - T5.1.3 Implementar prévia de dados/metadados e confirmação antes de
    arquivar ou excluir.
  - T5.1.4 Implementar integridade de hash, recuperação explícita de índice e
    tratamento de falha parcial.
- **Critérios de aceite:**
  1. Arquivamento requer consentimento explícito e registra revisão, origem,
     versões e fingerprint.
  2. Snapshot é imutável; editar Projeto atual não o altera.
  3. Índice corrompido é reconstruível a partir de registros válidos e nunca
     substitui o snapshot como fonte de verdade.
  4. Path arbitrário, hash divergente ou arquivo inválido não participa como
     dado confiável.
  5. Exclusão e falhas parciais têm confirmação/diagnóstico e não anunciam
     conclusão falsa.
- **Dependências:** US1.1.
- **Riscos:** R3, R8, R10.

**Sequenciamento atual:** F10.1 arquiva somente após confirmação, valida e
registra fingerprint SHA-256, mantém snapshots imutáveis, reconstrói o índice
de registros/snapshots verificados e exige confirmação para remoção. Paths
derivam de IDs validados e ficam confinados ao workspace. Falha parcial é
reportada; operação de reparo explícito e testes foram adicionados, mas não
executados nesta sessão.

### E11 — Comparação e reuso histórico determinísticos

#### F11.1 — Buscar, filtrar e comparar

**US5.2 — Comparar projetos por dimensões conhecidas**

Como arquiteto, quero encontrar projetos por metadados e comparar dimensões
estruturadas para avaliar semelhanças com suas limitações explícitas.

- **Prioridade:** P1; **complexidade:** XL.
- **Tarefas técnicas:**
  - T5.2.1 Curar política JSON versionada de dimensões, pesos, comparadores e
    cobertura mínima com exemplos representativos.
  - T5.2.2 Implementar `Exact`, `NumericLinear` e `SetJaccard` conforme
    contrato, sem fórmula executável em JSON.
  - T5.2.3 Implementar filtros literais e comparação com unidade/cenário,
    dados ausentes e versões incompatíveis identificados.
  - T5.2.4 Exibir score, coverage, dimensões usadas/excluídas, política e
    motivos sem sugerir qualidade ou probabilidade.
- **Critérios de aceite:**
  1. Busca por metadado funciona sem política de similaridade válida.
  2. `Unknown`, `NotApplicable`, provisório, unidade ou cenário incompatível
     não pontuam como zero/igualdade.
  3. Sem peso comparável retorna `NotComparable`; cobertura abaixo do limite
     retorna `InsufficientCoverage`.
  4. Mesmas entradas e versões geram resultado e desempate determinísticos.
  5. Resultado histórico não altera análise, ranking, score ou recomendação
     atuais.
- **Dependências:** US5.1; US2.4 para comparar versões/análises quando
  incluídas no escopo.
- **Riscos:** R7, R8, R10.

#### F11.2 — Reutilizar decisões e apresentar estatísticas descritivas

**US5.3 — Reutilizar decisão como proposta**

Como arquiteto, quero copiar ou referenciar uma decisão histórica com sua
proveniência para avaliá-la no contexto atual sem importar sua confirmação.

- **Prioridade:** P1; **complexidade:** M.
- **Tarefas técnicas:**
  - T5.3.1 Implementar proposta local com referência ao snapshot e à decisão
    de origem.
  - T5.3.2 Implementar revisão/confirmação explícita e inspeção lado a lado do
    contexto.
  - T5.3.3 Se estatísticas forem aprovadas, definir população, janela,
    denominador e dados ausentes antes de expô-las.
- **Critérios de aceite:**
  1. Reuso cria proposta nova; não copia autoria, status, análise ou confirmação
     para o Projeto atual.
  2. Usuário consegue abrir origem, fontes e contexto antes de confirmar.
  3. Similaridade não é apresentada como recomendação nem inferência de sucesso.
  4. Estatística, caso aprovada, declara população/denominador e não mede
     qualidade por frequência.
- **Dependências:** US1.5, US5.1, US5.2.
- **Riscos:** R4, R8, R10.

**Sequenciamento atual:** busca literal e comparação `Exact`,
`NumericLinear`/`SetJaccard` já estão disponíveis. Similaridade exige uma
única política JSON válida em
`<workspace>/policies/history-similarity/`; nenhum peso padrão foi criado sem
curadoria. O loader valida caminhos e comparadores contra o perfil de atributos
suportado e rejeita dimensões repetidas, inválidas ou não pontuáveis. O diretório
[`data/history-curation`](../data/history-curation/README.md) documenta como
preparar snapshots locais sem publicá-los no Git; nenhum snapshot de usuário
está disponível para justificar pesos ou cobertura padrão. A UI apresenta
automaticamente uma avaliação agregada de prontidão da curadoria, sem expor
valores de projetos nem definir pesos/limites. Reuso exige
inspecionar o snapshot, grava uma proposta local com proveniência e não importa
status/análise/autoria; estatísticas são descritivas com população e
denominadores. Testes adicionados não foram executados nesta sessão.

## 9. Fase 6 — Plataforma Enterprise

### Escopo e gate de aprovação

Esta fase é condicional. Antes de qualquer feature operacional, devem ser
aprovados público/ambiente de implantação, modelo de identidade e autorização,
fronteiras de workspace, privacidade, retenção, backup/recuperação, auditoria,
ameaças e requisitos legais aplicáveis. Uma solução multiusuário não pode
expor arquivos locais do servidor por padrão. O desenho precisa manter JSON
local enquanto essa for a restrição vigente; se escala ou colaboração exigir
outro armazenamento, abrir decisão arquitetural explícita e atualizar o
produto, a arquitetura e os schemas antes de implementar.

**Refinamento do gate:** foram confirmados hospedagem local para um usuário,
workspace local único, sem autenticação adicional, sem política de retenção
adicional ou backup automático, e logs operacionais sem conteúdo do Projeto.
Essas escolhas mantêm os limites do MVP e não autorizam exposição em rede,
multiusuário, armazenamento centralizado ou trilha corporativa de auditoria. A
Fase 6 operacional permanece sem escopo adicional aprovado.

### E12 — Governança do conhecimento e operação controlada

#### F12.1 — Validar e distribuir pacotes de conhecimento

**US6.1 — Revisar pacote de conhecimento antes de disponibilizar**

Como curador, quero validar IDs, schemas, referências, versões e proveniência
de um pacote JSON para reduzir risco de conteúdo inválido em uso corporativo.

- **Prioridade:** P1; **complexidade:** L.
- **Tarefas técnicas:**
  - T6.1.1 Definir fluxo de revisão/publicação, compatibilidade e rollback
    somente após decisão sobre autoria e responsabilidade do curador.
  - T6.1.2 Implementar relatórios agregados de validação sem execução de
    código e sem alterar arquivos de origem automaticamente.
  - T6.1.3 Definir distribuição/assinatura/integridade dos pacotes conforme o
    modelo de ameaça aprovado.
- **Critérios de aceite:**
  1. Pacote inválido não substitui conteúdo válido ativo.
  2. Relatório identifica erros, versões e proveniência sem executar conteúdo.
  3. Publicação/rollback são explícitos, auditáveis e reversíveis conforme
     política definida.
  4. Extensões compatíveis continuam sendo dados JSON carregáveis sem
     recompilação.
- **Dependências:** US1.3 e gate de aprovação enterprise.
- **Riscos:** R2, R5, R11.

### E13 — Implantação, privacidade e colaboração

#### F13.1 — Definir modelo de implantação e acesso

**US6.2 — Operar a aplicação dentro de limites aprovados**

Como responsável técnico, quero implantar a aplicação com identidade,
isolamento e operação documentados para proteger projetos e conhecimento
locais conforme o ambiente aprovado.

- **Prioridade:** P0 após aprovação do gate; **complexidade:** XL.
- **Tarefas técnicas:**
  - T6.2.1 Resolver requisito de target .NET/runtime suportado e modo de
    hospedagem; revisar especificação se .NET 9 não estiver suportado na data
    de início produtivo.
  - T6.2.2 Modelar ameaças e implementar autenticação/autorização e isolamento
    de workspace somente para os cenários aprovados.
  - T6.2.3 Definir e testar backup, recuperação, retenção, atualização,
    observabilidade e resposta a falhas sem registrar conteúdo sensível.
  - T6.2.4 Definir se colaboração simultânea é necessária; só então projetar
    controle de concorrência e sincronização.
- **Critérios de aceite:**
  1. Identidade e autorização são testadas nos fluxos e limites de workspace
     aprovados.
  2. Navegador/usuário não acessa paths ou projetos fora de seu escopo.
  3. Logs não contêm por padrão texto livre, payload integral de Projeto,
     credenciais ou ADR completo.
  4. Backup e recuperação são verificáveis e as limitações operacionais são
     documentadas.
  5. Banco, serviço externo ou mudança de target só entram após decisão
     aprovada e atualização dos guardrails; nenhuma mudança silenciosa.
- **Dependências:** gate de aprovação enterprise; decisão de target e
  hospedagem; US1.1.
- **Riscos:** R1, R3, R6, R11.

#### F13.2 — Governança e trilha de auditoria

**US6.3 — Auditar alterações relevantes sem reter conteúdo indevido**

Como responsável de governança, quero identificar operações e revisões
relevantes para atender políticas aprovadas sem registrar dados sensíveis
desnecessários.

- **Prioridade:** P2; **complexidade:** L.
- **Tarefas técnicas:**
  - T6.3.1 Definir eventos auditáveis, retenção, acesso e minimização antes de
    escolher formato de armazenamento.
  - T6.3.2 Implementar trilha versionada e consulta com permissões, se
    aprovada; não exigir event sourcing.
  - T6.3.3 Testar integridade, exportação e política de retenção/exclusão.
- **Critérios de aceite:**
  1. Eventos auditados têm ator, momento, ação e referência de revisão
     suficientes para a finalidade aprovada.
  2. Trilha não registra conteúdo integral de requisitos, projetos ou ADRs por
     padrão.
  3. Retenção, acesso, exportação e exclusão seguem política aprovada.
  4. Auditoria não altera regra de negócio, decisão ou score.
- **Dependências:** gate de aprovação enterprise; US6.2; políticas de
  privacidade/retenção.
- **Riscos:** R6, R11.

## 10. Registro de riscos

| ID | Risco | Impacto | Mitigação/condição de aceite |
|---|---|---|---|
| R1 | Blazor Server self-hosted local pode ser exposto em rede ou configurado com workspace fora do escopo esperado. | Exposição indevida ou acesso a dados locais não intencional. | Restringir o MVP a loopback/uso local, configurar e confinar workspace, sem multiusuário; reavaliar antes de exposição remota. |
| R2 | Schema JSON, referências ou loader incompatíveis com extensibilidade segura. | Catálogo indisponível, migrações quebradas ou execução insegura de conteúdo. | Validar Draft 2020-12, limitar DSL, testar conjunto atômico e diagnosticar versões/paths. |
| R3 | Revisões, gravação concorrente, interrupção de processo ou recuperação inadequadas em JSON local. | Perda ou corrupção de trabalho e resultados impossíveis de reproduzir. | Definir schema/fingerprint, revisão otimista, gravação segura e testes de falha em ambiente Windows. |
| R4 | Fatos desconhecidos, hipóteses ou recomendações confundidos com verdade/decisão. | Recomendação enganosa ou decisão arquitetural inadvertida. | Preservar estados/proveniência em domínio, UI, motor, grafo, ADR e histórico; testar casos negativos. |
| R5 | Qualidade, atualidade ou viés do catálogo e das regras iniciais. | Recomendações inadequadas com aparência de precisão. | Curadoria com fontes, revisão por arquitetos, limites visíveis e atualização versionada. |
| R6 | Acessibilidade, telemetria/logs ou tratamento de erro inadequados. | Exclusão de usuários, vazamento de dados ou falhas ocultas. | Testar teclado/estados, correlacionar erros sem payload sensível e nunca mascarar falha como sucesso. |
| R7 | Crescimento da DSL, operadores e dimensões sem limites ou semântica estável. | Complexidade e resultados não reprodutíveis. | Versionar contratos, limitar expressividade/recursos e rejeitar regra inválida integralmente. |
| R8 | Estrutura insuficiente ou similaridade com baixa cobertura levando a diagramas/comparações falsos. | Topologia inventada ou comparação enganosa. | Exigir dados explícitos, retornar incompleto/NotComparable e explicar cobertura/dimensões. |
| R9 | ADR fabricar contexto, consequências ou confundir aprovação do documento com decisão. | Registro de decisão incorreto ou autoria comprometida. | JSON canônico, lacunas explícitas, fontes/atribuição e ciclos de vida independentes. |
| R10 | Histórico reter dados sensíveis ou converter adoção/frequência em recomendação. | Risco de privacidade, viés de sobrevivência ou confiança indevida. | Opt-in, revisão prévia, armazenamento local, não influenciar análise e expor denominadores. |
| R11 | Requisitos enterprise indefinidos ou adoção de multiusuário/banco sem governança. | Exposição de dados, custo operacional e violação de restrições do produto. | Tratar como gate de produto/segurança; não implementar sem requisitos e decisão arquitetural aprovados. |
| R12 | Requisito inicial .NET 9 está no fim de suporte em 10/11/2026. | Iniciar implementação produtiva em target sem suporte vigente. | Decidido: implementação em .NET 10 LTS (`net10.0`); revisar SDK/runtime antes de releases. |

## 11. Decisões e pendências que bloqueiam execução

1. **Target .NET (decidido):** implementar em .NET 10 LTS (`net10.0`), aprovado
   em lugar do requisito inicial .NET 9; revisar suporte antes de releases e
   registrar mudanças em [dotnet-solution-architecture.md](./dotnet-solution-architecture.md).
2. **Hospedagem/workspace (decidido):** MVP self-hosted local para um usuário,
   com workspace configurado no host; manter acesso local/loopback e não
   habilitar multiusuário. O navegador não acessa diretamente o disco local.
3. **Contrato do Projeto:** schema v1 está definido em
   [project.schema.json](../schemas/project.schema.json); concluir migração,
   revisão concorrente, escrita segura e organização dos artefatos JSON.
4. **Escopo do MVP:** aprovar quais regras básicas e tipos de candidato serão
   avaliados; essa análise mínima não antecipa a entrega completa da Fase 2.
5. **Validação do catálogo:** selecionar biblioteca Draft 2020-12 e limites
   operacionais antes de implementação.
6. **Enterprise:** obter requisitos aprovados de identidade, isolamento,
   retenção, backup, auditoria e colaboração antes de planejar stories
   operacionais da Fase 6.

Nenhuma pendência autoriza criar banco, usar IA generativa, permitir execução
de conteúdo JSON ou transferir regra de negócio para a UI.
