# Modelo de Domínio

## 1. Objetivo e premissas

Este documento define o vocabulário e os limites de domínio que orientam as
próximas etapas de modelagem da base de conhecimento, motor de regras e
experiência do produto. É um modelo conceitual, não uma especificação de
classes, APIs ou implementação.

Premissas:

- A plataforma apoia decisões; o arquiteto continua responsável pela decisão
  final.
- Recomendações são determinísticas, explicáveis e produzidas a partir de
  requisitos, regras e conhecimento estruturado. Não há IA em runtime.
- Projetos e conhecimento são persistidos inicialmente em arquivos, sem banco
  de dados.
- O conhecimento deve ser extensível por arquivos JSON sem recompilar a
  aplicação.
- Diagramas são projeções gráficas das decisões e relações do projeto; não são
  fonte independente da verdade.
- Pontuações e confiança representam critérios do modelo, não garantias de
  qualidade ou sucesso arquitetural.

## 2. Linguagem ubíqua

| Termo | Definição no domínio |
|---|---|
| Projeto | Espaço de trabalho persistido que reúne contexto, requisitos, análises, decisões e artefatos arquiteturais. |
| Requisito | Necessidade, restrição ou atributo de qualidade informado ou confirmado pelo usuário. |
| Item de conhecimento | Definição versionada de uma arquitetura, tecnologia, pattern, princípio, boa prática, antipattern ou template. |
| Relação de conhecimento | Vínculo tipado entre itens, como dependência, recomendação, conflito ou alternativa. |
| Regra | Critério declarativo que avalia dados do projeto e conhecimento para produzir uma contribuição à análise. |
| Análise | Execução identificável do motor sobre uma versão do projeto e um conjunto de versões do conhecimento e regras. |
| Recomendação | Resultado explicável de uma análise, associado a um item candidato e às evidências que o sustentam ou desfavorecem. Não é uma decisão. |
| Decisão arquitetural | Escolha humana registrada no projeto, incluindo alternativas consideradas, justificativa e consequências. |
| Trade-off | Conjunto de consequências favoráveis e desfavoráveis associado a uma alternativa ou decisão. |
| Grafo arquitetural | Representação de itens e relações relevantes para visualizar dependências, compatibilidades e alternativas. |
| Diagrama | Projeção editável ou gerada de elementos e relações confirmados do projeto. |
| ADR | Registro versionável de uma decisão arquitetural, seu contexto, alternativas e consequências. |

### Vocabulário controlado

O grafo utiliza os tipos de relação definidos pelo produto:

- `DependsOn`: um item exige outro para ser adotado adequadamente.
- `Recommends`: um item é frequentemente apropriado em conjunto com outro,
  sem que a relação seja obrigatória.
- `ConflictsWith`: os itens não devem ser selecionados juntos sob as condições
  descritas.
- `AlternativeTo`: os itens são opções substitutas para um contexto ou
  necessidade comum.

`Requires` não é um tipo de relação separado: migra para `DependsOn` somente
quando a obrigatoriedade for confirmada. `ConflictsWith` e `AlternativeTo`
são simétricas na projeção do grafo, sem duplicação obrigatória no catálogo.
Campos legados `recommendedWith`, `supports` e `alternatives` precisam de
revisão semântica; não se convertem automaticamente em relações. A especificação
de relações, expansão e conflito está em
[graph-dependency-model.md](./graph-dependency-model.md).

## 3. Bounded Contexts

### 3.1 Projeto e Requisitos

Responsável por criar e manter o contexto de um projeto arquitetural, seus
requisitos, restrições e decisões humanas.

- Possui o agregado `Projeto`.
- Define a origem e o estado de revisão de cada requisito.
- Registra metas opcionais de contexto, como domínio, tamanho da equipe,
  criticidade e alvo de escalabilidade; fatos não informados permanecem
  desconhecidos para a avaliação.
- Não calcula recomendações nem contém catálogo de tecnologias.

### 3.2 Catálogo de Conhecimento

Responsável por carregar, validar, identificar, versionar e consultar itens e
relações de conhecimento fornecidos por arquivos JSON.

- Trata conteúdo como dados de domínio versionados, não como lógica
  compilada.
- Expõe um catálogo consistente para uma análise, incluindo versão ou hash dos
  arquivos utilizados.
- Erros de estrutura, identificadores duplicados ou referências inválidas
  devem ser reportados; conteúdo inválido não deve ser silenciosamente aceito.

### 3.3 Análise e Recomendação

Responsável por avaliar requisitos e restrições contra regras e conhecimento,
produzindo resultados reproduzíveis e explicáveis.

- Uma análise é vinculada a um retrato identificável do projeto, às versões
  das regras e à versão do catálogo consultado.
- Uma recomendação conserva evidências e contribuições usadas na avaliação.
- Uma recomendação não modifica nem decide pelo projeto.

### 3.4 Visualização Arquitetural

Responsável por projetar decisões e relações do projeto em grafos e diagramas.

- Diagramas gerados podem ser regenerados a partir da fonte de domínio.
- Ajustes visuais não devem alterar requisitos ou decisões sem uma operação
  explícita de domínio.
- Diagramas C4 preservam níveis distintos (Context, Container e Component),
  proveniência, revisão de origem e diferença entre elementos confirmados,
  propostos e não resolvidos.
- Preview reativo é derivado e não cria uma persistência/histórico a cada
  alteração; snapshots salvos são identificados por revisão e fingerprint.
- Este contexto pode ser desenvolvido depois do MVP.

### 3.5 Registro de Decisões

Responsável por registrar decisões humanas e seus ADRs.

- A decisão humana é distinta da recomendação que a motivou.
- ADRs são documentos JSON versionáveis associados a uma decisão; Markdown é
  projeção/exportação, não fonte concorrente.
- A geração produz rascunho determinístico a partir de decisão e fontes
  identificadas. Lacunas permanecem explícitas; aceitar um ADR não confirma ou
  altera a decisão.
- Edições humanas e proveniência são preservadas entre revisões.
- Pode iniciar como parte do contexto Projeto e ser separado apenas se regras
  próprias de ciclo de vida ou colaboração justificarem essa divisão.

### 3.6 Histórico Arquitetural (futuro)

Responsável por arquivar snapshots JSON locais, compará-los por atributos
estruturados e apoiar a inspeção/reutilização explícita de decisões.

- Arquivamento exige ação explícita; o snapshot é imutável e o índice é
  reconstruível.
- Similaridade é determinística, versionada, acompanhada de coverage e
  explicação por dimensão; dados desconhecidos não são valores negativos.
- Não altera regras, scores ou decisões do projeto atual automaticamente.
- Uma decisão histórica reutilizada vira proposta nova com proveniência e
  confirmação humana.
- Não compartilhar dados automaticamente; retenção, acesso e colaboração
  permanecem fora do escopo local inicial.
- Detalhes estão em
  [architectural-knowledge-history.md](./architectural-knowledge-history.md).

### Relações entre contextos

1. Projeto e Requisitos fornece entradas e decisões explícitas à Análise.
2. Catálogo de Conhecimento fornece itens, regras e relações versionadas à
   Análise.
3. Análise devolve resultados explicáveis ao Projeto; o usuário decide quais
   recomendações considerar.
4. Visualização projeta dados confirmados do Projeto e relações conhecidas do
   Catálogo.
5. Registro de Decisões associa escolhas humanas a análises e gera ADRs.
6. Histórico consulta projetos persistidos e só influencia o projeto atual
   mediante ação explícita do usuário.

## 4. Agregados e limites de consistência

### 4.1 Agregado Projeto

**Raiz:** Projeto.

**Contém conceitualmente:**

- Identidade, nome, propósito, contexto e metadados.
- Requisitos e restrições do projeto.
- Respostas estruturadas de elicitação, com referência à pergunta/versão,
  origem e estado de confirmação; respostas desconhecidas não equivalem a
  requisitos negativos.
- Referências aos resultados de análise utilizados.
- Decisões arquiteturais registradas.
- Referências a diagramas e ADRs associados.

**Invariantes:**

- Cada requisito e decisão pertence a exatamente um projeto.
- Alterar requisitos, contexto ou escolhas manuais invalida a atualidade dos
  resultados derivados; resultados anteriores podem ser preservados como
  histórico, mas não devem ser apresentados como análise atual.
- Uma decisão aceita deve identificar a alternativa selecionada e, quando
  existir, a análise ou justificativa que a contextualizou.
- A recomendação não pode alterar o estado de uma decisão sem confirmação
  explícita do usuário.

Os artefatos derivados podem ser persistidos em arquivos separados por razões
de tamanho ou exportação, mas devem manter identificadores e relações
consistentes com a raiz Projeto.

### 4.2 Agregado Item de Conhecimento

**Raiz:** Item de Conhecimento, com subtipo discriminado, por exemplo
Arquitetura, Tecnologia, Pattern, Princípio, Boa Prática, Antipattern ou
Template.

**Contém conceitualmente:**

- Identificador estável, nome, descrição e categoria.
- Critérios de uso, benefícios, limitações, riscos e metadados pertinentes ao
  subtipo.
- Versão do conteúdo e proveniência.
- Relações declaradas com outros itens.

**Invariantes:**

- Identificador é único no catálogo e estável entre revisões do mesmo item.
- Referências devem apontar para itens conhecidos ou ser reportadas como
  inválidas.
- Cada relação possui tipo e origem identificáveis.
- Revisar um item gera nova versão de conteúdo; uma análise existente continua
  apontando para a versão que efetivamente usou.
- Campos específicos não devem ser presumidos iguais para todos os subtipos;
  o contrato detalhado será definido na etapa de modelo de conhecimento.

Os arquivos podem ser organizados individualmente ou por coleção. Essa escolha
de armazenamento não altera a identidade conceitual do agregado.

### 4.3 Agregado Regra

**Raiz:** Regra.

**Contém conceitualmente:**

- Identificador, versão, descrição e estado.
- Condições, escopo de avaliação, efeito e parâmetros de pontuação, conforme o
  contrato definido para o motor.
- Referências semânticas a requisitos ou itens de conhecimento.

**Invariantes:**

- A regra deve ser interpretável de forma determinística.
- Uma regra inválida não pode contribuir parcialmente para a análise.
- A versão exata da regra usada deve ser rastreável.
- A regra descreve um critério; não deve conter uma recomendação hardcoded para
  um item específico fora do contrato declarativo definido.

### 4.4 Agregado Análise

**Raiz:** Análise.

**Contém conceitualmente:**

- Identificador e momento da execução.
- Identidade/revisão ou retrato de entrada do Projeto.
- Referências às versões do catálogo e do conjunto de regras.
- Recomendações, evidências, resultados de avaliação e avisos.

**Invariantes:**

- O resultado é imutável como registro de execução; nova execução gera nova
  análise.
- Cada recomendação pode ser explicada pelas evidências preservadas.
- Pontuação deve ter escala, fatores e significado documentados.
- Resultado com dados ausentes, conhecimento incompleto ou regras inválidas
  deve expor a limitação e não simular certeza.

**Limite de consistência:** o Projeto pode manter a referência à análise
considerada atual, sem incorporar todos os resultados ao mesmo documento.

### 4.5 Agregado Decisão Arquitetural

**Raiz:** Decisão Arquitetural.

**Contém conceitualmente:**

- Alternativa escolhida e alternativas consideradas.
- Estado da decisão, contexto, justificativa e consequências.
- Referência opcional à análise e às recomendações relacionadas.
- Referência ao ADR associado.

**Invariantes:**

- Uma recomendação não equivale a uma decisão.
- A autoria/confirmação humana e o momento da decisão devem ser preservados.
- Alterar uma decisão já registrada cria uma revisão ou nova decisão vinculada,
  em vez de apagar silenciosamente o histórico.
- Benefícios, riscos e trade-offs podem ser conhecidos ou informados pelo
  usuário; sua origem deve ser distinguível.

### 4.6 Artefatos subordinados

Requisito, Relação, Evidência, Trade-off, Diagrama e ADR podem ser modelados
como entidades ou value objects conforme identidade e ciclo de vida detalhados
nas próximas etapas. No nível conceitual:

- Requisitos e decisões têm identidade e ciclo de vida próprios dentro do
  Projeto.
- Evidências e itens de pontuação pertencem a uma Análise e não precisam de
  identidade global.
- Uma relação pertence ao Item de Conhecimento ou à versão do catálogo.
- Um diagrama é um artefato identificado, com referência à revisão de projeto
  que o originou.
- Um ADR tem identidade, estado e revisões; sua forma documental é apresentada
  ao usuário.

## 5. Entidades

### Projeto

Unidade principal de trabalho. Agrega contexto, requisitos e referências para
análises e decisões. É persistido em arquivo(s) e pode ser exportado.

### Requisito

Necessidade ou restrição individual. Deve permitir classificação (funcional,
qualidade ou restrição), descrição, prioridade, origem e estado de confirmação.
O mecanismo de captura não muda sua semântica: texto livre e questionário
devem resultar em requisitos explícitos revisáveis.

Uma resposta do questionário é um fato de entrada; só se torna requisito
confirmado quando sua semântica for suficientemente clara e o usuário puder
revisá-la. A resposta deve manter sua proveniência e não ser sobrescrita por
uma interpretação automática.

Texto livre passa por interpretação lexical determinística e produz hipóteses
com trechos de evidência, tags, polaridade, estado de revisão e versão do
vocabulário. Hipóteses não são Requisitos nem fatos do motor. Após confirmação
explícita, o Projeto pode registrar um Requisito com origem `UserProvided` e
referência à proveniência da interpretação. A etapa e seus contratos estão em
[requirement-text-interpretation.md](./requirement-text-interpretation.md).

### Decisão Arquitetural

Registro de uma escolha feita pelo arquiteto. Mantém alternativa selecionada,
opções consideradas, justificativa, consequências, autoria, revisão e vínculo
opcional com uma análise. Não se confunde com recomendação. Alterar uma decisão
registrada cria nova revisão ou decisão vinculada, sem apagar seu histórico.

### Item de Conhecimento

Definição catalogada e versionada. Seus subtipos descrevem arquiteturas,
tecnologias, patterns, princípios, boas práticas, antipatterns ou templates.
Não representa uma instância selecionada em um projeto; a seleção é uma
referência mantida por uma decisão ou projeto.

### Regra

Critério declarativo e versionado que pode ser avaliado contra uma entrada.
Regras devem ser independentes de interface e produzir resultados
rastreáveis.

### Análise

Execução identificável do motor. Vincula entradas, versões de conhecimento e
regras aos resultados obtidos. Análises anteriores são mantidas para
explicabilidade, não confundidas com o estado corrente do projeto.

### Recomendação

Resultado associado a um item candidato, incluindo adequação calculada,
justificativas e sinais favoráveis ou desfavoráveis. Pode existir sem ser
aceita. Seu estado de revisão pelo usuário não deve mudar o fato histórico do
resultado calculado.

### Evidência

Explicação específica de uma recomendação: requisito relacionado, regra
avaliada, relação do grafo ou dado que contribuiu. Indica se favorece,
desfavorece ou limita a conclusão e mantém proveniência.

### Relação de Conhecimento

Aresta tipada entre dois itens de conhecimento. Declara direção quando
aplicável, condições e proveniência. A validação de combinações, ciclos e
conflitos é responsabilidade do catálogo/análise, conforme a semântica
definida na etapa do grafo.

### Diagrama

Representação visual identificada de elementos e conexões de uma revisão do
projeto. Pode ser gerada e depois ajustada visualmente, sem que a edição
puramente visual altere decisões de domínio.

A geração C4 requer estrutura suficiente para o nível solicitado. Context
mostra o sistema e suas interações externas; Container exige containers
identificados; Component exige decomposição explícita de um container. Falta de
dados produz estado incompleto e lacunas, não elementos inventados. Cada
conexão deve declarar sentido, modo de interação e proveniência conhecida.
Layout é separado do modelo semântico. Ver
[diagram-generation.md](./diagram-generation.md).

### ADR

Registro documental versionado associado a uma decisão humana. Contém título,
contexto, problema, alternativas, decisão, consequências, riscos, benefícios,
justificativa e trade-offs. Rascunho é composto determinísticamente apenas a
partir de fontes identificadas; seção sem fonte permanece `NotProvided`. Estado
do ADR é independente da decisão e suas edições humanas são preservadas. JSON é
a fonte canônica e Markdown é exportação. Ver
[adr-generation.md](./adr-generation.md).

### Projeto Histórico

Registro local opt-in de uma revisão imutável do Projeto, seu snapshot JSON,
fingerprint, perfil estruturado de comparação, referências a decisões e
proveniência. Similaridade não implica recomendação ou transferência de
decisão. O schema e os limites de comparação estão em
[architectural-knowledge-history.md](./architectural-knowledge-history.md).

## 6. Value Objects

- **Identificador:** valor estável, único no escopo declarado e independente
  do nome de exibição.
- **Versão de Conhecimento:** identifica a revisão do item ou conjunto de
  conhecimento efetivamente utilizado.
- **Contexto do Projeto:** conjunto de atributos relevantes à análise, como
  domínio, equipe, criticidade e restrições operacionais.
- **Prioridade:** nível ordenável atribuído a um requisito, com escala
  documentada.
- **Categoria de Requisito:** classificação controlada para orientar captura e
  avaliação.
- **Pontuação:** valor calculado associado a uma escala e método identificados;
  não representa probabilidade por padrão.
- **Confiança:** medida calculada conforme método explícito e distinta da
  pontuação/adequação. Não deve ser exibida como probabilidade sem calibração.
- **Trade-off:** consequências favoráveis e desfavoráveis, qualificadas por
  contexto e origem.
- **Evidência:** referência imutável ao elemento que contribuiu para um
  resultado, com contribuição e explicação.
- **Referência a Item:** identificador e versão opcional de um item no
  catálogo, sem duplicar seus dados.
- **Estado de Decisão:** estado do ciclo de vida, como proposta, aceita,
  rejeitada ou substituída; transições precisam ser definidas.
- **Metadados de Proveniência:** autor/origem, data, versão do formato e
  demais dados necessários à rastreabilidade.

### Ciclo de vida da decisão

| Estado atual | Próximo estado permitido | Requisito |
|---|---|---|
| `Proposed` | `Accepted` ou `Rejected` | Ação explícita e autoria/data da mudança |
| `Proposed` ou `Accepted` | `Superseded` | Referência à decisão substituta, distinta da decisão atual |
| `Rejected` ou `Superseded` | Nenhum | Estado terminal; uma nova escolha cria outra decisão |

Uma decisão registra sua autoria/data originais, revisão, alternativa escolhida
e alternativas consideradas. A referência à análise é opcional; quando
presente, identifica o snapshot que contextualizou a escolha. A versão da
alternativa escolhida é preservada quando conhecida. Cada transição válida
produz uma nova revisão e registra autoria/data da mudança de estado; não
reescreve a autoria original. Vincular/verificar a análise e candidatos contra
o projeto e o snapshot é responsabilidade do caso de uso da Application.
Aceitar uma recomendação não produz essa transição implicitamente.
O histórico de transições é mantido no JSON junto com a decisão atual para que
cada estado anterior, autoria e momento permaneçam inspecionáveis.

## 7. Serviços de Domínio

### Avaliador de Regras

Avalia regras válidas em relação ao retrato de entrada e ao catálogo carregado.
Deve ser determinístico para as mesmas versões e entradas, retornando
contribuições rastreáveis em vez de modificar o projeto.

### Motor de Recomendação

Agrupa avaliações e relações para produzir recomendações ordenáveis,
justificativas e alternativas. O ranking não oculta conflitos nem converte
pontuação em decisão automática.

### Analisador de Grafo

Expande dependências e recomendações relevantes, identifica alternativas e
conflitos e detecta referências inválidas. Deve preservar os tipos e sentidos
das relações definidos pelo catálogo. Expansão transitiva automática percorre
apenas `DependsOn`; recomendações, alternativas e conflitos não escolhem nós
nem se tornam score por proximidade. Arestas condicionais usam a DSL
declarativa do motor e lógica de três valores.

### Validador do Catálogo

Verifica schemas, identificadores, versões, campos obrigatórios e integridade
referencial do conjunto carregado. Deve informar cada problema com sua origem
e impedir que conteúdo inválido seja tratado como válido.

### Gerador de Diagramas

Projeta requisitos, decisões confirmadas e relações pertinentes em um formato
visual. Deve distinguir elementos confirmados de itens apenas recomendados.

### Gerador de ADR

Compõe rascunho estruturado a partir da decisão e fontes versionadas. Não
preenche como fato dados ausentes; campos incompletos permanecem identificados
para revisão humana. A aceitação do documento não atualiza a decisão.

### Comparador de Projetos (futuro)

Compara snapshots arquivados segundo política JSON determinística e explica
dimensões, pesos e coverage. Uma similaridade não implica equivalência,
qualidade ou transferência automática de decisão. Ver
[architectural-knowledge-history.md](./architectural-knowledge-history.md).

## 8. Eventos de Domínio

Eventos representam fatos ocorridos, não comandos ou intenções.

- `ProjetoCriado`
- `ContextoDoProjetoAtualizado`
- `RequisitoAdicionado`
- `RequisitoAtualizado`
- `RequisitoRemovido`
- `AnaliseSolicitada`
- `AnaliseConcluida`
- `AnaliseConcluidaComAvisos`
- `CatalogoCarregado`
- `CatalogoRejeitado`
- `DecisaoArquiteturalRegistrada`
- `DecisaoArquiteturalRevisada`
- `DiagramaGerado`
- `AdrGerado`

Uma implementação baseada em arquivos não precisa adotar infraestrutura de
mensageria ou event sourcing para usar esses conceitos. Eventos podem ser
notificações internas ou registros de auditoria quando houver necessidade.
Persistir todos os eventos como histórico imutável é uma decisão futura, não
requisito do MVP.

## 9. Casos de uso

### MVP

1. Criar projeto e registrar contexto inicial.
2. Adicionar, editar e remover requisitos.
3. Carregar e validar a base de conhecimento JSON.
4. Consultar itens de conhecimento e suas relações.
5. Solicitar análise do projeto.
6. Consultar recomendações e suas evidências, limitações e trade-offs.
7. Comparar alternativas recomendadas.
8. Registrar, revisar ou rejeitar uma decisão arquitetural.
9. Salvar, reabrir e exportar o projeto em arquivos.

### Evolução posterior

10. Responder a questionário e confirmar requisitos extraídos.
11. Inserir texto livre, revisar hipóteses lexicais determinísticas, esclarecer
    ambiguidades e convertê-las em requisitos confirmados somente após
    confirmação explícita.
12. Navegar grafo e inspecionar dependências e conflitos.
13. Gerar e revisar diagramas.
14. Gerar, editar e exportar ADRs.
15. Encontrar projetos similares e reutilizar decisões explicitamente.
16. Validar e publicar nova versão da base de conhecimento sem recompilar a
    aplicação.

## 10. Diagramas verbais

### 10.1 Contexto do sistema

```text
Arquiteto / Tech Lead / Product Owner
    -> informa contexto e requisitos
    -> solicita análise
    <- inspeciona recomendações explicadas
    -> confirma, rejeita ou registra decisão

Architecture Decision Assistant
    -> lê conhecimento e regras JSON
    -> valida versões e referências
    -> avalia requisitos e relações
    -> salva projeto e resultados em arquivos
    -> apresenta comparações e artefatos
```

### 10.2 Agregados e referências

```text
Projeto
  ├── Contexto
  ├── Requisitos*
  ├── Referências a Análises*
  ├── Decisões Arquiteturais*
  │     └── Referência a ADR
  └── Referências a Diagramas*

Análise
  ├── Retrato de entrada do Projeto
  ├── Versão do Catálogo
  ├── Versões das Regras
  └── Recomendações*
        └── Evidências*

Catálogo versionado
  ├── Itens de Conhecimento*
  ├── Relações tipadas*
  └── Regras*
```

`*` representa zero ou mais elementos. A disposição física dos dados em
arquivos pode diferir do desenho, desde que preserve identidade, versão e
consistência.

### 10.3 Ciclo de análise e decisão

```text
Projeto e requisitos
    -> capturar retrato de entrada
    -> combinar com versões identificadas do catálogo e regras
    -> avaliar regras e relações
    -> produzir recomendações com evidências e avisos
    -> apresentar alternativas e trade-offs
    -> arquiteto revisa e registra decisão
    -> decisão pode originar diagrama e ADR
```

### 10.4 Grafo de conhecimento

```text
[Arquitetura] --DependsOn------> [Tecnologia]
[Pattern]     --Recommends-----> [Tecnologia]
[Tecnologia]  --ConflictsWith--> [Tecnologia]
[Pattern]     --AlternativeTo--> [Pattern]
```

Os nós de arquitetura, tecnologia e pattern são os tipos centrais do grafo.
Princípios, boas práticas e antipatterns podem compor o catálogo e influenciar
regras; sua participação como nós ou atributos do grafo deve ser decidida na
etapa específica de conhecimento e grafo.

## 11. Persistência, versionamento e reprodutibilidade

- O modelo de domínio não exige banco de dados.
- O Projeto é a unidade lógica para salvar, reabrir e exportar trabalho do
  usuário.
- O Catálogo é carregado de arquivos JSON externos à lógica compilada; a
  aplicação valida o conjunto antes de disponibilizá-lo.
- ADRs e registros de histórico são JSON locais versionados. O histórico
  guarda snapshot imutável opt-in; seu índice é reconstruível e não substitui
  snapshots.
- Uma análise deve identificar a revisão do projeto, do catálogo e das regras
  que utilizou, para que seus resultados sejam auditáveis.
- As alterações do catálogo não reescrevem análises ou decisões anteriores.
- A estratégia de migração do formato JSON, compatibilidade e descoberta de
  arquivos será especificada junto aos schemas e à solução técnica.
- Escrita segura, concorrência entre processos, recuperação de arquivos e
  localização dos dados são preocupações de infraestrutura a definir antes da
  implementação de persistência.

## 12. Decisões em aberto para as próximas etapas

1. Definir o esquema comum e os campos específicos de cada tipo de item.
2. Definir se regras compartilham o catálogo ou são carregadas por coleção
   independente, preservando referência de versão.
3. Formalizar significado, escala e normalização de pontuação e confiança.
4. Formalizar transições e histórico de decisões.
5. Definir se referências e relações podem ser condicionais e como tratar
   dependências cíclicas.
6. Especificar formato dos arquivos de projeto e política de migração.
7. Determinar como diagramas editados pelo usuário convivem com regeneração.
8. Definir política operacional de retenção, backup e workspace antes de
   implementar o histórico local; colaboração segue fora do escopo até haver
   requisitos próprios.

Esses pontos devem ser resolvidos nas etapas de modelo de conhecimento, motor
de regras, grafo, diagramas e ADR, evitando antecipar decisões de implementação
sem requisitos validados.
