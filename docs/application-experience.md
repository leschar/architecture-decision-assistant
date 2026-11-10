# Experiência da Aplicação

## 1. Propósito e limites

Esta especificação transforma a visão do produto e os modelos das etapas 06–08
em fluxos e telas da aplicação Blazor Server. É uma especificação funcional e
de arquitetura de interação, não uma implementação nem um contrato de
componentes .NET.

A aplicação apoia o arquiteto; não toma decisões em seu lugar. A tela nunca é
fonte de verdade para requisitos, análise, grafo, decisão ou diagrama. Regras,
transições de domínio, interpretação lexical, cálculo de recomendações,
validação semântica e persistência pertencem às camadas Application, Domain e
Infrastructure conforme descrito neste documento.

Restrições do produto:

- Não há IA em runtime, modelos generativos, OpenAI ou Azure OpenAI.
- Não há banco de dados inicial: projetos e conhecimento usam arquivos JSON.
- A base de conhecimento válida é extensível por dados JSON dentro dos
  contratos suportados, sem recompilação.
- A UI não interpreta relações canônicas ou calcula scores por conta própria.
- Somente requisitos/fatos confirmados podem influenciar regras.
- Diagramas são projeções explícitas, versionadas e reativas em sessão; não
  inventam topologia.

## 2. Princípios de experiência

1. **Orientar por tarefa.** Conduzir da criação/abertura de um projeto à
   captura de evidências, análise, inspeção de alternativas e decisão humana.
2. **Separar fato, hipótese e resultado.** Usar rótulos e estilos distintos
   para entrada do usuário, hipótese lexical, dado desconhecido, avaliação e
   decisão confirmada.
3. **Explicar antes de persuadir.** Mostrar regra, requisito, relação,
   proveniência, versão, cobertura e limitação junto ao resultado relevante.
4. **Não esconder ausência.** `Unknown`, incompleto, não avaliado, conflito,
   stale e erro são estados explícitos; não equivalem a `false`, zero ou
   sucesso.
5. **Preservar controle humano.** Análise, aceitação de recomendação, decisão,
   publicação de diagrama e confirmação de hipótese são ações distintas.
6. **Evitar perda de trabalho.** Distinguir alterações salvas de alterações
   locais ainda não salvas; comunicar falha de gravação e conflito de revisão.
7. **Manter acessibilidade.** Navegação por teclado, foco visível, rótulos
   textuais para cores/ícones, contraste suficiente e mensagens de estado
   anunciáveis por tecnologia assistiva.
8. **Não exigir escolha tecnológica prematura.** Começar por objetivo,
   contexto, requisitos e lacunas; mostrar conhecimento tecnológico como
   exploração, não como requisito de entrada.

## 3. Mapa de navegação e escopo

```text
Workspace
  +-- Novo projeto / Abrir projeto JSON
  +-- Projeto
       +-- Visão geral
       +-- Requisitos
       |    +-- Entrada manual
       |    +-- Interpretação lexical (assistida)
       |    +-- Questionário
       +-- Conhecimento
       |    +-- Arquiteturas / Patterns / Tecnologias
       |    +-- Grafo de relações
       +-- Análise
       |    +-- Recomendações
       |    +-- Comparação
       +-- Diagrama C4
       +-- Decisões
       +-- ADRs (etapa 10; não habilitar antes da entrega)
       +-- Projetos anteriores (etapa 11; não habilitar antes da entrega)
```

Requisitos, perguntas, tecnologias, recomendações, trade-offs, ADRs, diagramas
e projetos anteriores continuam acessíveis no fluxo global, mas não devem ser
amontoados numa única página. A visão geral resume estado e oferece atalhos; as
páginas de tarefa têm espaço para revisão detalhada.

### Estado de capacidade por etapa

| Capacidade | Tratamento na experiência | Dependência/limite |
|---|---|---|
| Projeto, requisitos manuais, catálogo, análise e decisão | Fluxo principal do produto | Contrato de arquivo do Projeto e persistência devem ser fechados na etapa 12 |
| Questionário | Fluxo de elicitação versionado | Usa catálogo e resposta dos schemas existentes; pergunta condicional com `Unknown` não é ocultada como falsa |
| Interpretação lexical | Assistente opcional de hipóteses e revisão | Só a confirmação explícita pode criar requisito/fato; nenhum texto vai a serviço de inferência |
| Grafo | Navegação acessível de nós e relações tipadas do catálogo; proveniência e direção explícitas | Não seleciona itens nem altera score/decisão; relações contextuais não são avaliadas fora de uma análise |
| Diagramas C4 | Preview textual acessível, geração reativa, edição de layout separada, snapshots listáveis e reabertos | Projeção determinística; layout não altera semântica e snapshots continuam explícitos |
| ADRs | Rascunho de decisão humana, revisão JSON imutável, aceite independente e exportação Markdown | Seções sem fonte permanecem `NotProvided`; aceite não confirma decisão |
| Projetos anteriores | Arquivamento/removal com confirmação, busca literal, comparação estruturada opcional e reuso como proposta | JSON local, hash conferido, política de similaridade explícita; sem política há somente busca |

Capacidades ainda não implementadas não aparecem como controles funcionais.
As telas ADR e histórico são disponibilizadas somente no contexto de um
Projeto aberto e encaminham operações para casos de uso da Application.

## 4. Shell e estrutura comum

### Cabeçalho

- Nome do produto e nome do projeto aberto.
- Indicador de salvamento: `Salvo`, `Alterações não salvas`, `Salvando`,
  `Falha ao salvar` ou `Conflito de revisão`.
- Ações contextuais: salvar, exportar JSON e, quando entradas estiverem
  válidas, solicitar análise.
- A ação de análise informa a revisão de entrada; não aparece habilitada com
  alterações não salvas ou entrada inválida.

### Navegação lateral do projeto

Links para Visão geral, Requisitos, Questionário, Conhecimento, Análise,
Diagrama e Decisões. Item ativo e contagens são apenas navegação/apresentação,
não estado de negócio. Não indicar contagem como score ou completude quando a
medida não estiver definida.

### Área principal

- Título e objetivo da página.
- Resumo de revisão/fingerprint quando houver resultado derivado.
- Conteúdo principal e painel de explicação/avisos quando necessário.
- Mensagens de erro associadas ao campo ou ação, com foco acessível.
- Confirmação explícita apenas para ações irreversíveis ou com efeito de
  domínio; ações de navegação não confirmam ou alteram dados.

### Wireframe do shell

```text
+------------------------------------------------------------------+
| ADA | Projeto: Nome                    [Salvamento] [Salvar] [Exportar] |
+---------------------+--------------------------------------------+
| Visão geral         | Título da tarefa             [Analisar]    |
| Requisitos          | Revisão / estado / avisos                  |
| Questionário        |                                            |
| Conhecimento        | Conteúdo da página                          |
| Análise             |                                            |
| Diagrama            |                                            |
| Decisões            |                                            |
+---------------------+--------------------------------------------+
| Mensagem de estado / validação / conflito de revisão              |
+------------------------------------------------------------------+
```

## 5. Telas e fluxos

### 5.1 Workspace: criar ou abrir projeto

**Objetivo:** iniciar um trabalho sem pressupor uma arquitetura.

**Conteúdo:** lista de projetos disponíveis no workspace configurado, ação
para criar, ação para abrir/importar um arquivo JSON e estado vazio explicando
como começar. A política de descoberta/localização dos arquivos é decisão de
Infrastructure e etapa 12; a aplicação não deve presumir acesso irrestrito ao
sistema de arquivos do computador do navegador.

**MVP self-hosted:** a aplicação roda no computador do usuário e lê/grava
somente no workspace local configurado no host (padrão
`%LOCALAPPDATA%/ArchitectureDecisionAssistant/workspace`). O host escuta em
loopback; não é hospedagem multiusuário e não concede ao navegador acesso ao
disco. Alterar a raiz exige configuração explícita e paths são confinados ao
workspace. O schema versionado inicial do agregado é
[project.schema.json](../schemas/project.schema.json).

**Criar projeto:** solicitar apenas metadados necessários definidos pelo
agregado (por exemplo, nome e objetivo). Não criar requisitos, decisões,
arquiteturas ou integrações por template silencioso. Se futuramente houver
template, apresentar seus elementos como proposta identificável e requerer
confirmação.

**Estados:** sem projetos, carregando, JSON inválido, versão incompatível,
projeto aberto, falha de leitura e falha de gravação. Erros de importação
identificam arquivo/caminho e problema, sem descartar o projeto já aberto.

**Ações:** criar, abrir, importar, exportar e abrir projeto. A seleção de um
arquivo é encaminhada a caso de uso da Application; a UI não desserializa um
documento de domínio para aplicar regras.

### 5.2 Visão geral do projeto

**Objetivo:** mostrar o estado do trabalho e próximos passos, sem pontuar a
qualidade do projeto.

**Conteúdo:** nome/objetivo, quantidade de requisitos por estado conhecido,
estado da última análise, número de alertas/lacunas e resumo das decisões
confirmadas. Cada número deriva de dados retornados pela Application e permite
navegar ao detalhe. Não combinar esses itens numa métrica inventada de
"prontidão".

**Ações:** editar contexto; ir a requisitos; responder perguntas; explorar
conhecimento; executar ou abrir análise; revisar decisões; abrir diagrama
quando houver modelo estrutural disponível.

**Estado vazio:** explicar a primeira ação sugerida sem selecionar arquitetura
ou sugerir tecnologia por padrão. Resultado derivado de revisão anterior
aparece marcado como defasado quando suas entradas mudaram.

### 5.3 Requisitos e contexto

**Objetivo:** manter requisitos explícitos e contexto do projeto.

**Lista:** descrição, classificação, prioridade quando conhecida, origem,
estado de confirmação e proveniência. Filtros são transitórios. Requisitos
desconhecidos ou provisórios não aparecem como confirmados.

**Edição:** formulário para criar/editar requisito manual, com classificação,
descrição e valores opcionais explícitos. A validação de domínio ocorre no
caso de uso; validação HTML de campos obrigatórios/formato serve apenas a
usabilidade. Não inferir unidade, cenário, prazo ou prioridade ausentes.

**Ações:** adicionar, editar, remover com confirmação quando necessário,
consultar origem e marcar/confirmar apenas por fluxo explícito.

**Efeito derivado:** mudança que afeta entrada da análise invalida sua
atualidade, mas preserva o resultado anterior como histórico identificável;
não o substitui por uma tela de sucesso vazia.

### 5.4 Interpretação lexical e revisão de hipóteses

**Objetivo:** oferecer uma ferramenta opcional para transformar texto em
hipóteses revisáveis, segundo
[requirement-text-interpretation.md](./requirement-text-interpretation.md).

**Painel de entrada:** texto original, ação `Interpretar` e indicação da
versão de vocabulário/política retornada. O processamento ocorre localmente no
produto, sem chamada de inferência externa. Não enviar texto a logs ou
telemetria por padrão.

**Resultado:** cada menção apresenta trecho destacado e offsets/evidências
quando inspecionados, tag candidata, polaridade, estado (`NeedsConfirmation`,
`Ambiguous`, `Conflict`, `Negated`, `Confirmed` ou `Rejected`) e pergunta de
esclarecimento se disponível. Mostrar texto não coberto e avisos para deixar
claro que a ausência de match não significa ausência de requisito.

**Ações:** confirmar hipótese não ambígua, rejeitar, selecionar alternativa
semântica quando ambígua ou informar esclarecimento. Confirmação deve
identificar usuário e momento no domínio. Não oferecer `Confirmar todas` para
hipóteses ambíguas, em conflito ou negadas. Confirmação produz requisito
somente por caso de uso e não altera o texto original da execução.

**Proteções:** hipótese pendente, ambígua, negada ou rejeitada não entra na
análise. A UI não decide escopo de negação nem converte menção em regra.

### 5.5 Questionário arquitetural

**Objetivo:** elicitar dados sem transformar pergunta em recomendação.

**Layout:** progresso descritivo por seção, uma pergunta por cartão, ajuda
contextual, resposta e ações `Salvar resposta`, `Não sei`, `Não se aplica` e
`Prefiro não responder` quando aplicáveis. Manter a origem e o estado exigidos
pelo contrato da resposta. Exibir unidade, cenário e horizonte em campos
separados para respostas quantitativas.

**Ramificação:** catálogo e lógica da condição são avaliados pela
Application. `showWhen: Unknown` nunca é interpretado pela UI como false; a
pergunta fica em seção de contexto insuficiente ou é oferecida para
aprofundamento, conforme política de elicitação. Perguntas condicionais sem
condição explícita não são exibidas automaticamente.

**Resumo antes da análise:** revisar fatos confirmados, provisórios,
desconhecidos, não aplicáveis e recusados separadamente. Respostas não se
convertem silenciosamente em requisitos; transformação e vínculo de
proveniência seguem o modelo de domínio.

### 5.6 Conhecimento: arquiteturas, patterns e tecnologias

**Objetivo:** explorar itens do catálogo versionado antes ou durante análise.

**Lista/detalhe:** tipo, descrição, versão, critérios de uso, benefícios,
limitações, riscos, relações e proveniência disponíveis. Comparar tipos
distintos não significa que pertençam ao mesmo ranking. Relações usam somente
`DependsOn`, `Recommends`, `ConflictsWith` e `AlternativeTo` com semântica da
[especificação do grafo](./graph-dependency-model.md).

**Ações:** pesquisar/filtrar catálogo, abrir item e explorar relações. A
visualização do catálogo não confirma seleção no Projeto. Um arquivo de
conhecimento inválido gera diagnóstico explícito; não mostrar catálogo parcial
como catálogo válido.

**Grafo visual:** nós e arestas são projeções consultivas; diferenciar
dependência de sugestão, conflito e alternativa. Não converter grau,
proximidade ou posição do nó em recomendação/score e não selecionar nós por
clique simples.

### 5.7 Análise e recomendações

**Objetivo:** consultar resultado determinístico sobre um retrato identificado
do projeto, regras e catálogo.

**Antes da execução:** apresentar quais requisitos/fatos confirmados serão
usados, quais estão desconhecidos e as versões do catálogo/regras disponíveis.
Alterações não salvas ou entrada inválida impedem executar com estado
ambíguo; explicar como corrigir ou salvar.

**Resultado:** separar arquiteturas, patterns e tecnologias em grupos
comparáveis; mostrar score de adequação se definido, cobertura, elegibilidade,
regras aplicadas, evidências favoráveis/desfavoráveis, desconhecidos,
dependências, conflitos, alternativas e trade-offs conhecidos. Score ausente
é `Não avaliado`, não zero/50. Cobertura não é probabilidade de sucesso.

**Ações:** abrir explicação, comparar candidatos do mesmo tipo, explorar
dependência, registrar intenção de decisão e solicitar nova análise. Nenhum
resultado altera o Projeto até ação explícita do usuário.

**Atualidade:** cada resultado informa revisão do projeto e versões de
conhecimento/regras. Resultado de revisão antiga fica visível como histórico,
marcado `Desatualizado`; não é apresentado como atual.

### 5.8 Comparação de alternativas

**Objetivo:** tornar trade-offs inspecionáveis sem ocultar critérios.

**Layout:** opções selecionadas pelo usuário em colunas; critérios/evidências
em linhas, com dados ausentes como `Não conhecido` e relações de alternativa
em área separada. Mostrar versões e contexto de análise. Não mesclar scores de
tipos diferentes nem calcular score agregado sem perfil de análise
versionado.

**Ações:** incluir/remover candidato da comparação, abrir evidências e
registrar decisão humana em fluxo separado. Selecionar uma coluna para
visualização não registra decisão.

### 5.9 Diagrama C4

**Objetivo:** pré-visualizar e salvar uma projeção semântica conforme
[diagram-generation.md](./diagram-generation.md).

**Controles:** nível Context/Container/Component, escopo explícito, preview,
avisos e lacunas. Selecionar nível mais detalhado sem decomposição suficiente
retorna `Incomplete`; não sintetiza containers, componentes ou protocolos.

**Preview reativo:** alterações relevantes podem ser coalescidas e calculadas
em segundo plano. Exibir revisão/fingerprint solicitado. Se uma resposta
atrasada chegar, descartá-la em favor da revisão mais nova; manter preview
anterior marcado como defasado enquanto calcula. Erro nunca vira diagrama
vazio com sucesso.

**Apresentação:** separar elementos `Confirmed`, `Proposed` e `Unresolved`;
cada nó/conexão tem proveniência. Sinalizar direção, tipo de interação,
protocolo/evento somente quando conhecidos. Controles de layout não alteram
semântica.

**Persistência:** preview não é salvo em cada alteração. Salvar/publicar
snapshot é explícito. Reutilizar apenas posições/overrides ligados a IDs
estáveis e apresentar órfãos para revisão. Dados persistidos seguem
[diagram.schema.json](../schemas/diagram.schema.json); a UI não inventa formato
alternativo.

### 5.10 Decisões arquiteturais

**Objetivo:** registrar escolha humana separada da recomendação.

**Conteúdo:** alternativa escolhida, alternativas consideradas, contexto,
justificativa, consequências, benefícios/riscos conhecidos ou informados pelo
usuário, autoria/data e referência à análise usada quando aplicável. Identificar
o que é informação do catálogo e o que foi escrito pelo usuário.

**Ações:** registrar, revisar ou substituir decisão conforme transições
definidas no domínio. Revisão não apaga silenciosamente histórico. Não
preencher justificativa, benefício ou consequência com fatos não fornecidos.

### 5.11 ADR e projetos anteriores

ADRs são preparados somente a partir de uma decisão existente, um por decisão,
e cada revisão JSON preserva autoria, fontes e lacunas. A revisão editorial cria nova revisão;
aceitar exige revisor explícito e não altera o status da decisão. O usuário pode
marcar um ADR como substituído por outro registro existente no mesmo projeto;
o sucessor deve documentar a decisão explicitamente ligada como substituta no
Projeto. Uma nova decisão tem seu próprio ADR, enquanto edições editoriais
continuam no mesmo registro. A transição cria uma revisão imutável e não altera
a decisão. Markdown é exportado da revisão salva por download no navegador.

Arquivar exige consentimento explícito e cria snapshot imutável com hash e
índice reconstruível. Busca textual é literal. Similaridade só é exibida se
houver uma política JSON válida; `Unknown`, unidades e cenários incompatíveis
são excluídos. Reutilização exige abrir/inspecionar a origem e cria uma nova
proposta com referência ao arquivo histórico, sem transferir status, autoria
ou análise.

## 6. Contrato de interação e arquitetura em camadas

### Web / Blazor Server

- Renderizar view models/resultados retornados pelos casos de uso.
- Manter apenas estado transitório de interação: filtro, aba, expansão, foco,
  texto ainda não submetido e seleção de comparação não persistida.
- Fazer binding e validação de formato útil ao usuário; encaminhar comandos e
  consultas pela fronteira Application.
- Não ler/escrever JSON, consultar diretamente catálogo ou implementar
  regras, matching lexical, lógica tri-state, pontuação, ordenação de domínio,
  grafo, topologia ou transições de decisão.
- Não esconder erro com fallback de sucesso. Preservar dados digitados após
  erro retornado.

### Application

- Expor casos de uso por handlers/serviços de aplicação, usando MediatR onde
  adotado pelo projeto.
- Orquestrar carregamento, validação de entrada com FluentValidation, acesso a
  portas de repositório, avaliação, geração de projeção e persistência.
- Compor view models/DTOs de consulta próprios da tela sem torná-los entidades
  de domínio ou arquivos persistidos.
- Retornar `Result` tipado para validação, regra de negócio, estado inválido,
  concorrência, catálogo inválido, recurso inexistente e falha recuperável.
- Não duplicar regra de domínio para conveniência de binding.

### Domain

- Ser dono das entidades, value objects, invariantes, semântica de estados,
  transições e serviços de domínio determinísticos.
- Não depender de Blazor, MediatR, schemas de UI, arquivos ou componentes
  visuais.
- Usar Result Pattern para resultados previstos de negócio; exceções não são
  fluxo normal de decisão.

### Infrastructure

- Implementar repositórios/ports com arquivos JSON, validação de schema e
  consistência de leitura/gravação.
- Carregar catálogo e regras versionados como dados; rejeitar conteúdo inválido
  com diagnósticos explícitos, sem publicar snapshot parcial.
- Isolar formato físico e paths da UI e do domínio.
- Registrar falhas técnicas em Serilog com contexto operacional suficiente,
  sem registrar texto livre de requisitos, conteúdo sensível ou payloads
  completos por padrão.

### Mapeamento de ações

| Intenção da UI | Responsabilidade primária |
|---|---|
| Criar/editar/remover requisito ou contexto | Application valida/orquestra; Domain aplica invariantes |
| Interpretar texto e revisar hipótese | Application invoca interpretação determinística; Domain mantém confirmação e proveniência |
| Responder pergunta | Application valida versão/tipo e aplica regra de elicitação; Domain mantém estado/origem |
| Abrir catálogo ou analisar | Application carrega snapshots validados; Domain avalia regras/relações |
| Renderizar grafo/diagrama | Application obtém projeção; Domain define semântica; Web apenas apresenta |
| Salvar/exportar/importar | Application chama porta; Infrastructure lê/escreve JSON |
| Filtrar, expandir, alternar aba | Web mantém estado transitório sem comando de domínio |

## 7. JSON, salvamento e falhas

1. Conhecimento e projetos são arquivos JSON locais; nenhum banco ou serviço
   externo é introduzido.
2. Catálogo de conhecimento é conteúdo versionado separado dos projetos. Uma
   análise fixa versões/hashes usados e não muda quando o catálogo é atualizado.
3. O formato persistido do agregado Projeto ainda precisa de schema, estratégia
   de migração, diretório e política de concorrência formalizados na etapa 12.
   Não usar estado de componente como substituto desse contrato.
4. Salvar é explícito e ocorre em ações de domínio significativas. Filtros,
   expansão de painel e previews transitórios não incrementam revisão do
   Projeto.
5. Escrita concorrente não sobrescreve silenciosamente versão mais nova:
   Application/Infrastructure detectam revisão divergente e retornam conflito
   com opções de recarregar ou revisar diferenças, sem descartar o rascunho.
6. Escrita de arquivo deve evitar apresentar sucesso antes de confirmar a
   persistência; estratégia atômica/recuperação é definida na etapa técnica.
7. Importação/exportação valida versão e estrutura. Erro informa arquivo,
   caminho, código e orientação; não corrige dados automaticamente sem
   confirmação.
8. Erros previstos de domínio retornam `Result` e são mostrados junto à ação.
   Erros técnicos inesperados são registrados e apresentados com identificador
   de suporte, sem detalhes internos sensíveis.

Categorias mínimas de estado na UX:

| Categoria | Apresentação |
|---|---|
| Validação de entrada | Campo/valor e correção necessária; manter demais dados |
| Regra de domínio | Mensagem acionável, sem stack trace |
| JSON/schema/referência inválidos | Arquivo, caminho JSON, item/ID quando disponível e correção |
| Catálogo indisponível ou inválido | Bloquear análise dependente e indicar que não há resultado atual |
| Conflito de revisão | Manter rascunho e oferecer revisão/recarregamento |
| Falha de leitura/gravação | Indicar estado não salvo; nunca mostrar sucesso |
| Resultado stale/incompleto | Manter resultado identificável, avisar limitações e permitir nova ação |
| Falha inesperada | Mensagem segura e ID de correlação; diagnóstico técnico em log |

## 8. Estados transversais e acessibilidade

- **Carregando:** ação que está em andamento e possibilidade de cancelar quando
  segura; impedir submissões duplicadas.
- **Vazio:** explicar ausência e ação seguinte; não criar conteúdo fictício.
- **Parcial/incompleto:** mostrar quais dados faltam e o que não foi avaliado.
- **Desatualizado:** mostrar revisão/fingerprint antiga e separar de atual.
- **Inválido:** bloquear ações dependentes, apresentar diagnósticos e preservar
  entrada.
- **Sucesso:** somente após confirmação do caso de uso (persistência inclusive).
- **Confirmação explícita:** descrever efeito de domínio; cancelar não altera
  estado.

Componentes de status incluem texto além de cor/ícone. Erros e conclusão de
operações são anunciados sem roubar foco indevidamente. Formulários usam
labels, instruções persistentes, ordem de tabulação previsível e associação
programática entre campo e erro. Diagramas oferecem alternativa textual
acessível de elementos, relações, estados e proveniência; a cor do nó não é a
única indicação de status.

## 9. Wireframes essenciais

### Requisitos

```text
+ Requisitos -------------------------------- [Adicionar requisito] -----+
| Contexto: ...                                                          |
| Filtros: [Todos] [Confirmados] [Provisórios] [Hipóteses]              |
| ---------------------------------------------------------------------- |
| Tipo | Requisito | Prioridade | Estado | Origem | [Abrir]              |
| ---------------------------------------------------------------------- |
| [Entrada manual] [Interpretar texto] [Questionário guiado]             |
+------------------------------------------------------------------------+
```

### Revisão da interpretação

```text
+ Interpretação lexical -----------------------------------------------+
| Texto original / editar entrada                     [Interpretar]     |
| Vocabulário vX | Política vY | Execução determinística                |
| --------------------------------------------------------------------- |
| Trecho destacado | Tag | Polaridade | Estado | Evidência | Ação        |
| ...             | ... | Negada     | Revisar| span      | [Detalhes]  |
| Ambiguidade: alternativas e pergunta; nenhuma selecionada            |
| Texto sem cobertura e avisos                                         |
| [Confirmar hipótese elegível] [Rejeitar] [Esclarecer]                 |
+------------------------------------------------------------------------+
```

### Análise e comparação

```text
+ Análise | revisão do projeto | catálogo | regras | [Executar análise] -+
| Avisos / fatos desconhecidos / limitações                             |
| Arquiteturas | Patterns | Tecnologias (listas independentes)           |
| Candidato | Score ou N/A | Cobertura | Elegibilidade | [Explicar]       |
| Evidências: regra, requisito, relação, efeito e proveniência           |
| Dependências / conflitos / alternativas / trade-offs                   |
| [Comparar selecionados]             [Registrar decisão]                |
+------------------------------------------------------------------------+
```

### Diagrama

```text
+ Diagrama C4 | [Context v] [Escopo v] [Atualizar preview] [Salvar] ----+
| revisão/fingerprint | Atualizando/Atual/Desatualizado | Incompleto     |
| ---------------------------------------------------------------------- |
| Nós e conexões com estado textual, direção, tipo e proveniência        |
| ---------------------------------------------------------------------- |
| Lacunas / conflitos de estrutura / truncamento / avisos                |
| Layout (posição/colapso) separado do modelo semântico                  |
+------------------------------------------------------------------------+
```

### Grafo de conhecimento

```text
+ Grafo | catálogo/hash | [Buscar] [Tipo] -------------------------------+
| Nós: Arquitetura / Pattern / Technology                                |
| [Nó selecionado]                                                      |
| Relações direcionadas ou simétricas | nó relacionado [Explorar]         |
| Justificativa | relação | versões | fonte | condição não avaliada        |
+------------------------------------------------------------------------+
```

Selecionar um nó altera somente o foco de navegação da página. A lista e as
relações vêm da projeção validada na Application/Domain; a UI não calcula
aplicabilidade, dependências transitivas ou scores. Relações simétricas podem
ser exploradas por qualquer extremidade sem duplicar sua declaração canônica.

Wireframes descrevem hierarquia e estados, não compromisso com controles
visuais específicos ou biblioteca de componentes.

## 10. Critérios de aceite verificáveis

1. Um usuário consegue criar/abrir projeto, navegar e distinguir alterações
   salvas de não salvas.
2. Entrada manual e questionário mantêm origem, estado, unidade/contexto e não
   tratam `Unknown`, `Declined` ou `NotApplicable` como negativo.
3. A tela lexical mostra evidências e versões; confirmar uma hipótese elegível
   exige ação explícita e uma hipótese não confirmada não afeta a análise.
4. Catálogo inválido não é mostrado como carregado parcialmente; o erro
   identifica local do problema.
5. A análise diferencia tipos de candidato, score ausente, cobertura,
   evidências, regras desconhecidas e resultado desatualizado.
6. Comparação não combina escalas incompatíveis e seleção visual não registra
   decisão.
7. Grafo não converte recomendação em dependência/score e estados Unknown não
   são tratados como verdadeiros.
8. Preview C4 mantém proveniência e estado; Container/Component insuficientes
   são incompletos; resposta obsoleta não substitui preview novo.
9. Edição de layout não altera elementos/conexões semânticos.
10. Falha de gravação ou conflito não reporta sucesso nem descarta rascunho.
11. Nenhuma regra de negócio necessária para esses comportamentos reside em
    código de componente Blazor.
12. ADR e projetos anteriores são operacionais somente com Projeto aberto,
    revisão/proveniência visíveis e os limites das etapas 10 e 11 respeitados.
13. Os fluxos são utilizáveis por teclado e o status não depende apenas de
    cor.
14. A navegação do grafo permite explorar extremidades e relações tipadas por
    teclado, com direção, proveniência e condições contextuais não avaliadas
    claramente indicadas.

## 11. Trade-offs e alternativas

| Escolha | Benefício | Custo / risco | Alternativa rejeitada |
|---|---|---|---|
| Páginas por tarefa com visão geral resumida | Mantém foco e comporta explicações complexas | Mais navegação | Todos os painéis sempre abertos numa tela só |
| Revisão explícita de hipóteses | Preserva segurança semântica e autoria humana | Mais passos antes da análise | Converter tags automaticamente em requisitos |
| Preview derivado e snapshots explícitos | Evita histórico de ruído e preserva versão atual | Preview pode mostrar estado stale durante cálculo | Persistir/regenerar a cada tecla |
| Result Pattern para resultados previstos | UI apresenta erros de domínio de forma verificável | Mais contratos de resultado | Exceções como fluxo normal de negócio |
| JSON local com revisão otimista | Portabilidade sem banco e detecção de edição concorrente | Concorrência/recuperação exigem cuidado de Infrastructure | Sobrescrita cega ou adoção precoce de banco |

## 12. Pendências antes da implementação

- Evoluir o schema inicial do Projeto conforme migração e persistência segura
  forem implementadas/testadas; a decisão de workspace self-hosted local foi
  aprovada para o MVP.
- Definir o perfil de análise/catálogo padrão e comportamento de falha se
  versões de conhecimento estiverem inválidas.
- Confirmar quais partes do questionário e interpretação entram no primeiro
  corte funcional; não pressupor que todos os recursos especificados sejam
  MVP.
- Definir política de autenticação/implantação do Blazor Server antes de
  expor arquivos ou projetos em ambiente compartilhado.
- ADR e histórico dependem das etapas 10 e 11; não preencher suas lacunas na
  UI por conveniência.
