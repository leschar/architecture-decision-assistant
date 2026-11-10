# Modelo de Conhecimento

## 1. Objetivo e princípios

A base de conhecimento fornece conteúdo versionado ao catálogo e ao motor de
decisão. Ela não é código de aplicação: novos itens válidos, como uma
tecnologia, devem poder ser adicionados em JSON sem recompilar o produto.

Princípios:

- Conhecimento é declarativo, identificável, validável e versionado.
- Regras e relações não são embutidas na UI nem codificadas como recomendações
  fixas no motor.
- Uma recomendação é um resultado derivado de uma análise, não um item
  autoritativo da base.
- O motor só usa itens e referências válidos; problemas de conteúdo devem ser
  apresentados com o arquivo e o caminho do campo.
- A base pode ser carregada de arquivos JSON individuais ou coleções, desde que
  cada item preserve o contrato. A política de descoberta e atualização será
  detalhada na arquitetura técnica.
- Adicionar conteúdo novo é extensível sem recompilação. Alterar a semântica
  do contrato ou adicionar uma nova classe de conhecimento pode exigir evolução
  versionada do schema e do leitor.

### Descoberta implementada no MVP

O catálogo combina os itens JSON iniciais distribuídos por
`ArchitectureAssistant.KnowledgeBase` com arquivos `*.json` diretamente no
subdiretório `knowledge` do workspace configurado. Cada arquivo representa um
item e precisa validar contra `knowledge-item.schema.json`. A leitura é
atômica: JSON inválido, versão/schema incompatível, ID duplicado ou referência
quebrada impede a publicação do snapshot inteiro. O snapshot inclui versão do
schema e hash SHA-256 determinístico do conjunto carregado. Arquivos de
extensão têm limite de 256 KiB e o catálogo tem limite de 200 itens.

O host reavalia os arquivos quando a consulta do catálogo é executada; a
interface atual os consulta ao carregar a página. Os arquivos são dados
declarativos: regras e relações não são compiladas nem executadas como código.

## 2. Contrato comum

Todo item de conhecimento possui:

| Campo | Obrigatório | Descrição |
|---|---:|---|
| `id` | Sim | Identificador estável, único no catálogo, em `kebab-case`; não usar o nome exibido como chave mutável. |
| `kind` | Sim | Tipo discriminador do item. |
| `schemaVersion` | Sim | Versão do contrato JSON utilizado, inicialmente `1.0`. |
| `version` | Sim | Versão do conteúdo do item em SemVer (`MAJOR.MINOR.PATCH`). |
| `name` | Sim | Nome legível para exibição. |
| `description` | Sim | Descrição concisa e neutra do conceito. |
| `tags` | Não | Termos controlados para busca e classificação. |
| `source` | Não | Referência à fonte, documentação ou responsável editorial. |
| `updatedAt` | Não | Data de revisão do conteúdo em formato ISO 8601. |
| `relations` | Não | Relações tipadas com outros itens do catálogo. |

Identificadores de referência devem usar `id`, não nomes livres. Referência
inexistente, duplicidade de identificador ou relação incompatível com o tipo
dos itens é erro de integridade do catálogo.

Regras também são arquivos declarativos do catálogo e usam o contrato comum,
com campos específicos descritos adiante. Recomendações, ao contrário, são
resultados de uma execução e não itens de conhecimento.

### Relações

`relations` contém objetos com:

- `type`: `DependsOn`, `Recommends`, `ConflictsWith` ou `AlternativeTo`;
- `targetId`: identificador do item relacionado;
- `rationale`: motivo da relação;
- `appliesWhen` (opcional): condição declarativa usando a DSL limitada e a
  lógica de três valores do motor;
- `source` (opcional): proveniência editorial específica da relação;
- `conditions` legado, se presente, é somente descrição textual; não pode ser
  executado nem controlar expansão.

Na projeção do grafo, o snapshot também preserva o caminho do arquivo JSON do
catálogo e a versão dos itens de origem/destino. A fonte editorial da relação
continua separada desse caminho técnico, permitindo rastrear tanto o dado
curado quanto o arquivo local que o forneceu.

Semântica:

- `DependsOn` é uma dependência necessária, não mera preferência.
- `Recommends` indica combinação útil, sem obrigatoriedade.
- `ConflictsWith` descreve incompatibilidade contextual; a justificativa deve
  explicar quando o conflito ocorre.
- `AlternativeTo` identifica uma opção substituta, não uma recomendação de
  adoção simultânea.

Relações devem ser declaradas no item de origem. O loader projeta consultas
inversas apenas para `ConflictsWith` e `AlternativeTo`, que são simétricas para
navegação. Não deve inferir semântica não declarada nem duplicar a relação
persistida. `DependsOn` e `Recommends` preservam direção.

Os campos antigos `requires`, `recommendedWith`, `alternatives` e `supports`
não são parte do contrato canônico. Migração:

- `requires` -> relação `DependsOn`, somente quando o alvo é um item concreto e
  a necessidade é realmente obrigatória;
- `recommendedWith` -> `Recommends`, sem implicar dependência;
- `alternatives` -> `AlternativeTo`;
- `supports` -> revisar manualmente; capacidade ou compatibilidade não é
  automaticamente uma recomendação.

Se um campo antigo se refere a um conceito abstrato ausente do catálogo, não
criar um identificador fictício para satisfazer a validação. Registrar o
conceito ou esclarecer a relação antes de migrar.

## 3. Tipos de conhecimento

### 3.1 Architecture (`kind: "Architecture"`)

Representa um estilo ou estrutura de construção de sistemas.

- **Obrigatórios:** `category`, `advantages`, `disadvantages`, `useWhen`,
  `avoidWhen`, `complexity`, `scalability`.
- **Opcionais:** `relations`, `tags`, `source`, `updatedAt`.
- **Escalas:** `complexity` e `scalability` são inteiros de 1 a 10; o valor
  precisa ser interpretado conforme a convenção documentada, não como medida
  objetiva.
- **Relacionamentos:** pode depender de ou recomendar outros itens, conflitar
  com opções e declarar alternativas.
- **Exemplos:** Monolith, Modular Monolith, Microservices.

### 3.2 Pattern (`kind: "Pattern"`)

Representa uma solução recorrente para um problema arquitetural ou de design.

- **Obrigatórios:** `category`, `problem`, `solution`, `benefits`, `risks`,
  `complexity`.
- **Opcionais:** `relations`, `useWhen`, `avoidWhen`, `tags`, `source`,
  `updatedAt`.
- **Relacionamentos:** normalmente aponta para tecnologias ou patterns
  relacionados; pode declarar pré-condições quando houver dependência real.
- **Exemplos:** CQRS, Saga, Outbox.

### 3.3 Technology (`kind: "Technology"`)

Representa produto, serviço, plataforma, biblioteca ou ferramenta técnica.

- **Obrigatórios:** `category`, `capabilities`, `useWhen`, `avoidWhen`,
  `complexity`, `operationalCost`.
- **Opcionais:** `relations`, `benefits`, `risks`, `tags`, `source`,
  `updatedAt`.
- **Escalas:** complexidade e custo operacional são inteiros de 1 a 10, com
  interpretação editorial definida e não comparável sem contexto.
- **Relacionamentos:** dependências e alternativas devem apontar para
  identificadores presentes no catálogo.
- **Exemplos:** Redis, RabbitMQ, Kafka.

### 3.4 Principle (`kind: "Principle"`)

Representa orientação fundamental que influencia decisões e avaliação.

- **Obrigatórios:** `statement`, `rationale`, `scope`.
- **Opcionais:** `relations`, `tags`, `source`, `updatedAt`.
- **Relacionamentos:** pode fundamentar boas práticas e regras; não é uma
  tecnologia ou uma arquitetura.

### 3.5 BestPractice (`kind: "BestPractice"`)

Representa orientação prática contextual para uma atividade ou decisão.

- **Obrigatórios:** `guidance`, `appliesTo`.
- **Opcionais:** `rationale`, `relations`, `tags`, `source`, `updatedAt`.
- **Relacionamentos:** `appliesTo` referencia itens por ID; referências devem
  ser validadas como as demais relações.

### 3.6 AntiPattern (`kind: "AntiPattern"`)

Representa uma solução recorrente que tende a produzir consequências
indesejáveis em determinado contexto.

- **Obrigatórios:** `symptoms`, `consequences`, `remediation`.
- **Opcionais:** `context`, `relations`, `tags`, `source`, `updatedAt`.
- **Relacionamentos:** pode apontar para práticas corretivas ou itens
  incompatíveis, sempre com contexto e justificativa.

### 3.7 Relações do grafo

Uma aresta é representada canonicamente no campo `relations` do item de
origem, com `type`, `targetId` e `rationale`. `appliesWhen`, quando fornecido,
é uma árvore declarativa da DSL de regras e referencia apenas fatos tipados
suportados. Isso preserva proveniência sem criar um segundo registro
obrigatório para cada aresta.

`relationKey` é derivado deterministicamente de origem, tipo, alvo e condição.
Relação sem condição usa a chave `always`. Relações duplicadas com a mesma
chave no mesmo item são inválidas. Ciclos em `DependsOn` são erros de
integridade; `Recommends` não expande transitivamente por padrão. O grafo
completo, incluindo algoritmo, snapshot e aceite, está em
[graph-dependency-model.md](./graph-dependency-model.md).

O schema ainda aceita `kind: "Dependency"` para compatibilidade com catálogos
anteriores, mas ele está obsoleto e não deve ser criado em conteúdo novo. O
loader pode normalizá-lo em memória para uma relação declarada no `sourceId`,
validando endpoints e duplicatas e emitindo diagnóstico de migração. Ele não é
nó do grafo e não deve coexistir com a mesma relação embutida no item de
origem. Identidade e ciclo de vida de aresta separados só serão reconsiderados
se houver necessidade demonstrada.

### 3.8 TradeOff (`kind: "TradeOff"`)

Descreve consequências favoráveis e desfavoráveis ligadas a uma escolha ou
contexto.

- **Obrigatórios:** `context`, `benefits`, `costs`.
- **Opcionais:** `appliesTo`, `tags`, `source`, `updatedAt`.
- **Relacionamentos:** `appliesTo` contém IDs de itens; benefícios e custos
  devem ser descrições contextuais, não um score oculto.

### 3.9 Rule (`kind: "Rule"`)

Define um critério declarativo para a avaliação determinística do motor.

- **Obrigatórios:** `category`, `enabled`, `weight`, `appliesTo`, `conditions`,
  `effects`.
- **Opcionais:** `appliesWhen`, `tags`, `source`, `updatedAt`.
- **Seleção de candidato:** `appliesTo.kinds` define os tipos avaliados;
  `ids` e `tags` podem restringir candidatos.
- **Condições:** árvore JSON `allOf`/`anyOf`/`not` com fatos namespaced e
  operadores permitidos; não executam código arbitrário.
- **Efeitos:** `Score`, `Exclude` ou `Flag`. `Exclude` só representa restrição
  rígida explícita; preferência negativa usa score ou aviso.
- **Peso:** importância relativa de contribuição, não probabilidade.
- **Semântica:** lógica de três valores (`True`, `False`, `Unknown`) para não
  tratar fatos ausentes como falsos.
- **Especificação:** ver [rule-engine.md](./rule-engine.md).

### 3.10 Recommendation (resultado de análise; não é `kind`)

Recomendação é um resultado serializável produzido por uma análise; não é um
`kind` de item de conhecimento e não influencia análises futuras como conteúdo
curado.

- **Campos:** análise, candidato (incluindo versão e fonte editorial quando
  cadastradas), elegibilidade, score, cobertura de evidências, avaliações de
  regras/fontes, fatos avaliados, relações encontradas e avisos.
- **Score:** pode ser nulo quando não há evidência suficiente para calcular;
  50 não é usado como fallback.
- **Cobertura:** não é probabilidade de acerto nem confiança calibrada.
- **Persistência:** resultados pertencem ao arquivo da análise/projeto, não ao
  catálogo de conhecimento carregável.
- **Schema de saída:** [recommendation.schema.json](../schemas/recommendation.schema.json).
- **Snapshot do MVP:** cada execução é um arquivo imutável separado, validado
  por [analysis-snapshot.schema.json](../schemas/analysis-snapshot.schema.json),
  com grupos de candidatos por tipo e revisão/hash do Projeto, catálogo,
  regras e avaliador. Alterações posteriores são identificadas no histórico;
  não sobrescrevem análises anteriores.

### 3.11 ProjectTemplate (`kind: "ProjectTemplate"`)

Representa um ponto de partida reutilizável para um tipo de projeto.

- **Obrigatórios:** `purpose`, `requirements`, `context`.
- **Opcionais:** `suggestedItemIds`, `tags`, `source`, `updatedAt`.
- **Relacionamentos:** cada ID sugerido deve existir; sugestões não equivalem
  a decisões confirmadas.

### 3.12 Vocabulário lexical (catálogo auxiliar)

O vocabulário de interpretação de texto é conteúdo JSON versionado, mas não é
um `Item de Conhecimento` nem uma nova recomendação. Ele define taxonomia de
tags, expressões e formas explícitas, colisões/ambiguidades, pistas de negação
e conflitos editoriais. Seu contrato é independente do schema de itens e está
especificado em
[requirement-text-interpretation.md](./requirement-text-interpretation.md).

Uma tag lexical classifica uma hipótese, não afirma um requisito por si só.
Referências a tags são validadas no conjunto do vocabulário; caminhos de fatos
e operadores do motor continuam limitados ao registro de fatos suportado. Não
executar código lexical fornecido no JSON.

## 4. Versionamento e compatibilidade

São duas versões independentes:

- **`schemaVersion`:** versão estrutural do contrato JSON. Mudança incompatível
  exige nova versão e estratégia explícita de leitura/migração.
- **`version`:** versão editorial de cada item, em SemVer. Correção de texto
  pode incrementar PATCH; mudança aditiva de conteúdo MINOR; mudança de
  significado ou remoção incompatível MAJOR.

Uma análise deve registrar a versão do catálogo ou hash determinístico do
conjunto carregado e as versões das regras, para permitir auditoria e
reprodução. Alterar a base não reescreve resultados anteriores.

O loader deve:

1. localizar arquivos conforme convenção configurada;
2. validar JSON sintático e `schemaVersion`;
3. validar cada item pelo schema correspondente a `kind`;
4. detectar IDs duplicados, referências quebradas e problemas semânticos;
5. publicar o catálogo apenas quando o conjunto estiver consistente, ou
   apresentar erros e impedir uma análise que dependa do conteúdo inválido;
6. informar arquivo, item e caminho do campo nos erros.

Não implementar carregamento parcial silencioso, valores padrão que ocultem
erros ou execução de expressões/código arbitrário provenientes dos arquivos.

## 5. Relação com recomendações e projetos

- O catálogo contém fatos e critérios reutilizáveis.
- As regras avaliam o contexto de um projeto.
- As recomendações são geradas por execução e guardam evidências e versões.
- Projetos armazenam requisitos e decisões humanas, não cópias editáveis do
  catálogo.
- A decisão do arquiteto pode referenciar uma recomendação, mas permanece
  distinta dela.
- Um template pode iniciar um projeto; os valores copiados tornam-se dados do
  projeto e não referências vivas que mudam silenciosamente com o template.

## 6. Schemas e exemplos

O contrato canônico é [knowledge-item.schema.json](../schemas/knowledge-item.schema.json).
Os schemas específicos são referências reutilizáveis para os tipos principais:

- [architecture.schema.json](../schemas/architecture.schema.json)
- [pattern.schema.json](../schemas/pattern.schema.json)
- [technology.schema.json](../schemas/technology.schema.json)
- [rule.schema.json](../schemas/rule.schema.json)
- [recommendation.schema.json](../schemas/recommendation.schema.json) (resultado
  gerado, fora do catálogo)
- [graph-analysis.schema.json](../schemas/graph-analysis.schema.json) (snapshot
  de expansão e avaliação do grafo, fora do catálogo)
- [diagram.schema.json](../schemas/diagram.schema.json) (snapshot de diagrama
  C4 do projeto, fora do catálogo de conhecimento)
- [lexical-knowledge.schema.json](../schemas/lexical-knowledge.schema.json) e
  [requirement-interpretation.schema.json](../schemas/requirement-interpretation.schema.json)
  (vocabulário auxiliar e resultado da elicitação, fora dos itens de
  conhecimento arquitetural)

Exemplos JSON do catálogo:

- [monolith.json](../schemas/monolith.json)
- [modular-monolith.json](../schemas/modular-monolith.json)
- [microservices.json](../schemas/microservices.json)
- [cqrs.json](../schemas/cqrs.json)
- [saga.json](../schemas/saga.json)
- [outbox.json](../schemas/outbox.json)
- [redis.json](../schemas/redis.json)
- [rabbitmq.json](../schemas/rabbitmq.json)
- [kafka.json](../schemas/kafka.json)
- [rule-small-team-microservices.json](../schemas/rule-small-team-microservices.json)
- [rule-scalability-fit.json](../schemas/rule-scalability-fit.json)

Esses dados são exemplos iniciais para validar formato, não autoridade
definitiva sobre adequação de produtos. Conteúdo técnico deve ser revisado
quanto a atualidade, contexto e fontes antes de ser usado em produção.
