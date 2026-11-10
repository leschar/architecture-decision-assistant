# Geração e Gestão de ADRs

## 1. Objetivo e limites

Um ADR registra uma decisão arquitetural humana de forma rastreável. O sistema
pode preparar um rascunho a partir de uma decisão registrada, suas alternativas
e evidências; não decide, não completa lacunas com conhecimento presumido e não
usa IA em runtime, modelos generativos, OpenAI ou Azure OpenAI.

O documento estruturado em JSON é a fonte de verdade. Markdown é uma projeção
de leitura/exportação do ADR, renderizada a partir do registro. Editar o
Markdown exportado não altera o ADR canônico.

O ADR não é uma recomendação nem a própria decisão de domínio. O estado do
documento (`Draft`, `NeedsReview`, `Accepted`, `Superseded`) é independente do
estado da decisão arquitetural. Aceitar um ADR não confirma ou substitui uma
decisão e vice-versa.

## 2. Conteúdo e semântica

Seções requeridas:

1. **Título:** nome curto da decisão.
2. **Contexto:** fatos de negócio, organizacionais e técnicos conhecidos no
   momento.
3. **Problema:** pergunta ou força que exige uma decisão.
4. **Alternativas:** opções efetivamente consideradas, sem listar opções
   inventadas para completar um formato.
5. **Decisão:** alternativa selecionada na decisão humana vinculada.
6. **Consequências:** resultados esperados ou observados, positivos e negativos.
7. **Riscos:** riscos conhecidos; campo vazio não significa ausência de risco.
8. **Benefícios:** benefícios sustentados pela decisão, por evidência ou por
   conteúdo de conhecimento identificado.
9. **Justificativa:** motivos registrados pelo decisor.
10. **Trade-offs:** compromissos explícitos entre atributos ou consequências.

Cada seção registra texto, completude (`Complete`, `Partial`, `NotProvided`),
atribuição (`GeneratedFromDecision`, `UserAuthored`,
`KnowledgeCatalog`) e referências de origem. Campo desconhecido permanece
vazio e sinalizado como `NotProvided`; não usar frases genéricas como se fossem
fatos do projeto.

Alternativas e justificativas oriundas do catálogo conservam IDs e versões. A
ausência de evidência não deve ser preenchida pela descrição geral de um item,
salvo quando o conteúdo exato, sua versão e a atribuição forem exibidos como
conteúdo do catálogo, não como fato contextual do projeto.

## 3. Fluxo

1. O usuário registra uma decisão arquitetural com alternativa escolhida,
   alternativas consideradas, justificativa e proveniência disponível.
2. O caso de uso de geração recebe o identificador/revisão da decisão e,
   opcionalmente, a análise/recomendação que a contextualizou.
3. A Application valida a entrada e compõe um rascunho determinístico. Seções
   sem fonte ficam `NotProvided` e geram itens de revisão.
4. O usuário revisa, edita ou completa seções; a autoria de cada conteúdo é
   mantida. A interface nunca aplica correção sem consentimento.
5. Salvar uma revisão do ADR grava JSON local com vínculos de origem. Aceitar
   o documento é uma ação explícita independente da decisão.
6. Alterar a decisão não reescreve silenciosamente o ADR anterior. O usuário
   pode gerar nova revisão ou novo ADR, com relação explícita de substituição.
7. Exportar Markdown gera uma representação somente de leitura do estado
   salvo, incluindo IDs de fontes/versões e sinalização de lacunas quando
   apropriado.

## 4. Ciclo de vida e proveniência

- `Draft`: rascunho editável, ainda não aprovado como documento.
- `NeedsReview`: existe conteúdo ou fonte insuficiente que requer ação humana.
- `Accepted`: revisão do ADR aprovada explicitamente por uma pessoa; não
  altera a decisão de domínio.
- `Superseded`: substituído por outro registro/revisão identificado; preserva
  histórico.

Cada revisão referencia o projeto, a decisão e a revisão da decisão usadas,
além da revisão/hash do Projeto e, quando aplicável, da análise e dos itens de
conhecimento. Conteúdo editado manualmente é preservado em novas revisões; uma
regeneração não sobrescreve texto autoral sem uma operação explícita de
substituição/revisão.

O ciclo de vida da decisão arquitetural é independente do ciclo do ADR:
`Proposed` pode ser aceita, rejeitada ou substituída; `Accepted` pode ser
substituída; `Rejected` e `Superseded` são terminais. A transição para
`Superseded` referencia a decisão substituta e guarda autoria/data da mudança.
Essa transição não aceita automaticamente o ADR nem altera seu estado.
As revisões e o histórico de estado da decisão permanecem no Projeto JSON; um
ADR continua vinculado à revisão exata que o originou.

O schema [adr.schema.json](../schemas/adr.schema.json) valida estrutura. A
Application/Domain também verifica que o ADR pertence ao projeto, a decisão e
as fontes referenciadas existem, a alternativa selecionada é coerente com a
decisão, transições de estado são permitidas e a revisão de aceitação tem
revisor/data. Essas relações entre arquivos não são garantidas por JSON
Schema.

## 5. Persistência e exportação

- Persistir o registro estruturado como JSON em diretório do projeto, conforme
  o contrato geral de Projeto a ser definido na etapa 12.
- Não usar banco, serviço externo ou execução de template/código vindo dos
  dados.
- Manter template Markdown como formato de exportação legível; o JSON e suas
  revisões continuam sendo a fonte canônica.
- Uma falha de escrita não deve indicar sucesso nem perder texto digitado.
- Migração de versão de schema é explícita; arquivo incompatível não deve ser
  reinterpretado silenciosamente.

## 6. Exemplos de aplicação por assunto

Os itens a seguir são **roteiros editoriais**, não decisões aprovadas nem
afirmações universais. Os campos entre colchetes só podem ser preenchidos com
dados confirmados do Projeto, decisão humana ou fonte de conhecimento
identificada. Em todos os exemplos, a decisão selecionada e sua justificativa
permanecem sob autoria do decisor.

| Tema | Contexto/problema que o ADR pode registrar | Alternativas a considerar | Lacunas que não se devem inventar |
|---|---|---|---|
| Microservices | Necessidade documentada de limites e implantação/autonomia, se presentes; problema concreto que motiva decomposição | Monólito, monólito modular, serviços separados ou manter arquitetura existente, conforme escopo real | Quantidade de serviços, autonomia de equipes, capacidade operacional, disponibilidade e ganhos de escala |
| RabbitMQ | Fluxo de integração/mensageria e garantias realmente requeridas | Opções de mensageria já avaliadas, processamento síncrono ou mecanismo atual | Throughput, durabilidade, ordenação, topologia, custo ou adequação do broker sem evidência/versionamento |
| Kafka | Necessidade confirmada de fluxo de eventos/dados e requisitos de retenção/consumo se conhecidos | Kafka, outra tecnologia catalogada, solução existente ou não introduzir plataforma de eventos | Volume, retenção, latência, número de consumidores e necessidade de event streaming |
| Redis | Necessidade específica de cache, estado efêmero ou outro uso explicitamente escolhido | Sem cache, cache em processo ou tecnologias catalogadas aplicáveis | Persistência, tolerância a perda, consistência, TTL, dimensionamento e uso de Redis como banco principal |
| Saga | Processo distribuído com etapas e falhas/compensações que foram modeladas | Transação local, coordenação síncrona ou abordagem Saga; variantes só entram se consideradas | Existência de serviços distribuídos, passos, compensações, broker ou garantia de consistência eventual |
| Outbox | Necessidade confirmada de persistir mudança e intenção de publicação de forma coerente | Publicação atual, Outbox com relay apropriado ou alternativas realmente avaliadas | Broker específico, exactly-once, ordem, retentativas e garantia final não demonstrada no modelo |

Cada ADR concreto deve registrar: título, status e projeto; contexto/problema
completo ou explicitamente parcial; alternativas realmente consideradas;
referência à decisão humana selecionada; consequências, benefícios, riscos e
trade-offs sustentados por fontes; justificativa do decisor; e revisão/hash das
entradas. Se o decisor não escolheu alternativa, não produzir ADR `Accepted`.

## 7. Critérios de aceite e testes futuros

1. Gerar o mesmo rascunho para a mesma decisão, revisão, fontes e versão do
   gerador, desconsiderando IDs de execução e timestamps.
2. Toda afirmação derivada aponta para uma decisão, análise, requisito,
   relação, item e versão ou autoria humana.
3. Seção sem fonte é `NotProvided`, não prosa fabricada ou inferência de
   ausência.
4. Recomendações, score e popularidade não selecionam a decisão.
5. ADR e decisão têm estados e revisões independentes; aceitar um não aceita o
   outro.
6. Regenerar não apaga edição autoral silenciosamente.
7. Exportação Markdown reflete o snapshot salvo e identifica lacunas e
   proveniência de forma legível.
8. JSON inválido, referência inexistente e falha de arquivo retornam
   diagnóstico acionável; nenhum arquivo parcial é anunciado como sucesso.
9. Exemplos de Microservices, RabbitMQ, Kafka, Redis, Saga e Outbox não
   codificam escolhas ou topologias universais.

## 8. Trade-offs e alternativas

| Escolha | Benefício | Custo / risco | Alternativa |
|---|---|---|---|
| ADR canônico em JSON, Markdown derivado | Revisões, validação e proveniência estruturadas | Markdown exportado não é a fonte editável | Markdown como único arquivo é simples, mas dificulta validação e vínculos |
| Rascunho determinístico somente de decisão/fontes | Evita atribuir decisão ao sistema | Usuário precisa completar contexto e consequências | Gerar texto livremente seria incompatível com as restrições e não rastreável |
| Revisão do ADR separada da decisão | Mantém autoria e ciclos de vida claros | Mais estados/ações para entender | Uma operação única mistura decisão e documentação |
| Lacunas explícitas | Evita fatos inventados | Rascunho pode estar incompleto | Preencher com padrões genéricos parece completo, mas é enganoso |

## 9. Pendências

- Definir o agregado e o schema completos da Decisão no contrato geral do
  Projeto.
- Definir se uma mudança de decisão cria nova revisão do ADR ou recomenda novo
  ADR conforme política editorial; ambas devem preservar histórico.
- Definir template e convenções de identificadores de ADR/exportação antes da
  implementação.

## 10. Estado da implementação local

A aplicação compõe rascunhos determinísticos das revisões de decisões
existentes, preserva cada revisão em JSON validado pelo schema, permite editar
seções sem sobrescrever versões anteriores e mantém a aceitação do ADR
independente da decisão. A exportação Markdown lê a revisão salva e mantém
lacunas e referências legíveis. A UI oferece uma prévia e inicia o download
de um arquivo Markdown da revisão salva; não grava o Markdown no workspace,
que continua contendo o registro JSON canônico.

O catálogo não é usado para inventar contexto ou consequências na composição
inicial. Campos que exigem conteúdo não fornecido permanecem `NotProvided` e
bloqueiam o aceite até revisão. Cada decisão tem um ADR; alterações editoriais
criam revisões do mesmo registro. Um ADR não substituído pode receber uma nova
revisão imutável com status `Superseded` e referência a outro ADR existente do
mesmo projeto; o ADR substituto deve documentar a decisão que explicitamente
substituiu a decisão original no Projeto. Uma decisão nova recebe seu próprio
ADR. As transições de ADR não alteram as decisões arquiteturais.
