# Interpretação Determinística de Requisitos em Texto

## 1. Objetivo e limites

Esta capacidade transforma texto livre em **hipóteses estruturadas revisáveis**,
para ajudar Product Owners e arquitetos a explicitar requisitos. Não determina
sozinha o significado do texto e não cria requisitos confirmados por encontrar
uma palavra.

O processamento é lexical e determinístico: normalização, segmentação,
correspondência de expressões em vocabulário JSON, aplicação de escopo
conservador de negação e apresentação de ambiguidades e conflitos. Não há IA em
runtime, modelos generativos, treinamento, serviços OpenAI ou Azure OpenAI.
Também não se executam expressões ou código contidos nos arquivos.

O resultado é uma proposta para revisão humana. Só fatos confirmados pelo
usuário podem alimentar regras e recomendações. O mecanismo deve poder dizer
“não identifiquei evidência suficiente”; não deve preencher lacunas com
suposições.

## 2. Avaliação crítica dos volumes solicitados

Não estabelecer mínimos de 500 palavras-chave, 500 sinônimos ou 100
categorias. Essas quotas incentivam duplicatas, variantes sem valor semântico,
categorias artificiais e associações frágeis; volume não demonstra cobertura,
precisão ou utilidade.

Começar com uma taxonomia pequena, baseada em conceitos úteis ao modelo de
domínio e às regras existentes. Incluir apenas expressões para as quais há
intenção e comportamento revisados. Uma forma lexical pode ter mais de uma
interpretação contextual; contá-la como sinônimo universal apagaria essa
ambiguidade.

Trade-offs:

| Escolha | Benefício | Custo / risco |
|---|---|---|
| Vocabulário inicial pequeno e revisado | Curadoria viável, rastreabilidade e menos associações artificiais | Cobertura inicial limitada; mais texto fica sem classificação |
| Não inferir por similaridade ou stemming | Correspondência reproduzível e controlável | Mais variantes precisam ser cadastradas explicitamente |
| Confirmar candidatos antes do motor de regras | Evita transformar uma ocorrência lexical em fato de negócio | Acrescenta uma etapa de revisão ao fluxo |
| Expandir por evidência observada | Cada adição atende a uma lacuna real e pode ser avaliada | Crescimento mais lento que uma lista gerada em massa |

O vocabulário cresce por propostas vindas de falsos negativos, revisão de
usuários e novos domínios. Cada alteração exige: definição de intenção,
expressões de inclusão e exclusão, tag canônica, exemplos positivos e
negativos, análise de colisões, revisão editorial, aumento de versão e execução
do conjunto de regressão. Sinônimos só são aceitos quando preservam o mesmo
sentido no contexto definido. Nenhuma meta de tamanho substitui esses critérios.

## 3. Modelo de processamento

### 3.1 Normalização

Aplicar uma sequência versionada e reproduzível:

1. validar entrada e preservar o texto original imutável;
2. normalizar Unicode para NFC e produzir uma cópia de comparação em minúsculas
   invariantes;
3. produzir uma chave lexical sem diacríticos (por exemplo, `integração` e
   `integracao`), mantendo offsets referidos ao texto original;
4. normalizar variantes tipográficas de apóstrofo e hífen, sem apagar a
   pontuação que separa cláusulas;
5. tokenizar por limites Unicode de palavra e preservar offsets em unidades de
   código UTF-16, compatíveis com índices de string .NET;
6. não remover acentos do texto exibido, não traduzir, não aplicar stemming,
   lematização ou correção ortográfica implícitos.

A chave sem diacríticos pode causar colisões. Todas as entradas que casarem
devem permanecer disponíveis para desambiguação, nunca ser resolvidas por ordem
de arquivo. Mudanças na normalização ou tokenização exigem nova versão da
política e testes de regressão.

### 3.2 Correspondência lexical

- Casar expressões por sequência exata de tokens e limite de palavra; não usar
  substring arbitrária (`fila` não deve casar dentro de `filamento`).
- Permitir expressões compostas, como `alta disponibilidade` e `integração com
  ERP`, como uma única evidência com span próprio.
- Em sobreposição, preferir a expressão mais longa **para apresentar o
  agrupamento**, mas conservar menções menores e alternativas quando puderem
  mudar o significado. A precedência não confirma requisito nem apaga conflito.
- Não usar fuzzy matching na primeira versão. Grafias não cadastradas ficam
  sem correspondência; o usuário pode sugerir uma variante para curadoria.
- Cada ocorrência guarda texto original, offsets, forma normalizada,
  identificador da entrada lexical e tags associadas.

### 3.3 Taxonomia de tags

IDs estáveis em `kebab-case` formam uma hierarquia acíclica com `parentId`
opcional. Tags descrevem **conceitos de requisito** ou tópicos, não tecnologia
recomendada. A hierarquia deve permanecer navegável e evitar categorias
redundantes.

Um conjunto inicial ilustrativo:

| Tag | Significado |
|---|---|
| `business.sales` | Domínio de vendas explicitamente mencionado |
| `integration.external-system` | Integração com sistema externo |
| `integration.erp` | ERP mencionado como sistema integrado |
| `quality.scalability` | Necessidade de escala ou capacidade |
| `quality.concurrency` | Concorrência explicitamente mencionada |
| `quality.availability` | Disponibilidade explicitamente mencionada |
| `quality.performance` | Desempenho/latência explicitamente mencionado |
| `constraint.technology` | Tecnologia obrigatória ou proibida, após clarificação |
| `requirement.functional` | Necessidade de comportamento funcional, após clarificação |
| `requirement.unknown` | Classificação ainda insuficiente; não é fato para regra |

Tags podem ser acrescentadas em JSON sem recompilar, mas paths de fatos e
operadores consumidos pelo motor são um contrato separado. Até haver uma
semântica aprovada e suportada pelo registro de fatos, tag desconhecida ao
motor serve apenas para exibição e busca, sem avaliação por regras.

### 3.4 Sinônimos e formas

Armazenar cada forma como texto explícito associada a uma entrada e locale.
Diferenciar sinônimo, abreviação e variante ortográfica, sem presumir que
formas próximas sejam equivalentes. Termos polissêmicos podem apontar para
mais de uma entrada/tag; nesses casos, registrar o grupo de ambiguidade e uma
pergunta de esclarecimento. Não usar uma lista global de stopwords para
descartar palavras que possam afetar negação, escopo ou intenção.

### 3.5 Negação e escopo

Manter um conjunto pequeno e curado de pistas negativas e exceções por locale.
Na primeira versão:

- avaliar negação apenas dentro da mesma oração/cláusula, delimitada por
  pontuação forte e conectores contrastivos definidos;
- reconhecer construções simples próximas, como `não precisa de X` e `sem X`,
  como evidência **negada**, não como ausência de frase nem requisito positivo;
- tratar dupla negação, negação distante, `não só`, citação, hipótese,
  condição, pergunta retórica e escopo não resolvido como ambíguos ou
  necessitando revisão;
- não inferir que uma negação de requisito implica o requisito oposto.

O escopo é uma regra deliberadamente conservadora, não um analisador sintático
completo. Cada resultado de negação inclui a pista, o trecho de escopo e a
regra de versão aplicada. Casos que escapam a essas construções são exibidos
para confirmação em vez de forçar uma polaridade.

### 3.6 Ambiguidade e conflitos

- **Ambiguidade lexical:** uma forma corresponde a múltiplos sentidos/tags.
  Exibir alternativas e uma pergunta curta; não escolher por peso.
- **Ambiguidade de requisito:** a tag pode ser clara, mas faltam atributo,
  cenário, unidade, limite, prioridade ou prazo. Solicitar o dado necessário.
- **Conflito entre menções:** tags declaradas incompatíveis no vocabulário ou
  afirmações positiva e negativa sobre o mesmo conceito no texto. Preservar
  ambas, apontar spans e pedir resolução; não usar a ocorrência mais recente
  como vencedora.
- **Expressões sobrepostas:** apresentar a interpretação composta e as
  interpretações concorrentes, quando semanticamente distintas.

Conflitos são editoriais e explícitos; não se deduz incompatibilidade apenas
porque duas tags aparecem juntas. Cada par conflitante declara sua razão e
pergunta de resolução.

### 3.7 Evidência, estado e confirmação

Cada hipótese inclui tag, polaridade (`Affirmed`, `Negated` ou `Uncertain`),
estado, uma ou mais menções e proveniência. Estados propostos:

- `NeedsConfirmation`: interpretação legível, ainda não confirmada;
- `Ambiguous`: há mais de uma leitura plausível;
- `Conflict`: evidências incompatíveis precisam de resolução;
- `Negated`: evidência explícita contrária; não criar requisito positivo;
- `Confirmed`: confirmação explícita do usuário;
- `Rejected`: usuário rejeitou a hipótese.

`Confirmed` e `Rejected` registram ator e momento da revisão. Uma ocorrência
lexical, quantidade de sinônimos, comprimento da expressão ou prioridade
editorial nunca altera o estado automaticamente. Não usar “confiança” ou
probabilidade sem avaliação e calibração; a versão inicial expõe evidências e
questões abertas, não um score de certeza.

## 4. Relação com domínio e motor de regras

O texto submetido é uma entrada do caso de uso de elicitação, não uma regra de
negócio na UI. Normalização, matching, negação, conflitos, validação,
conversão e confirmação pertencem à Application/Domain; a interface apenas
apresenta candidatos e encaminha escolhas do usuário.

Fluxo:

```text
texto original + versão do vocabulário
  -> menções lexicais com offsets
  -> hipóteses/tags com polaridade e proveniência
  -> revisão, confirmação, rejeição ou esclarecimento pelo usuário
  -> Requisito do Projeto, somente após confirmação
  -> retrato de fatos consumido pelo motor de regras
```

Um requisito confirmado usa o conceito já definido no modelo de domínio:
descrição, classificação, prioridade quando conhecida, origem, estado e
proveniência. A origem é `UserProvided`; a confirmação e a ligação às
evidências da interpretação ficam registradas separadamente. Requisitos
numéricos mantêm valor, unidade, cenário e horizonte explícitos. Expressões
como “milhares de pedidos” não viram números de throughput nem requisitos de
latência.

Tags em hipóteses pendentes, ambíguas, negadas ou rejeitadas não são fatos
positivos. O motor consome apenas requisitos/fatos confirmados segundo o
registro de fatos e usa `Unknown` para dados ausentes. Não avalia o texto
original, os termos não confirmados ou o resultado lexical bruto. Uma tag só
pode participar de regra quando sua semântica e caminho tipado estiverem
documentados no catálogo de fatos e na regra correspondente.

Uma nova execução de análise registra também a revisão/hash do texto de
entrada, do vocabulário, da política de normalização e do motor lexical. A
revisão dos requisitos invalida a atualidade de análises derivadas, conforme
as invariantes do Projeto.
O hash do texto é calculado sobre seus bytes UTF-8 exatos (prefixo
`sha256:` seguido do digest hexadecimal); não substitui controles de retenção
ou proteção do texto original.

## 5. Contratos JSON

- [lexical-knowledge.schema.json](../schemas/lexical-knowledge.schema.json):
  contrato extensível da taxonomia, formas lexicais, política de matching,
  pistas de negação e conflitos.
- [requirement-interpretation.schema.json](../schemas/requirement-interpretation.schema.json):
  resultado auditável de uma execução, suas menções, hipóteses e revisões.
- [lexical-knowledge.pt-BR.json](../schemas/lexical-knowledge.pt-BR.json):
  amostra pequena para validar o contrato; não é uma promessa de cobertura.

O schema valida estrutura. O carregador também deve validar IDs únicos,
referências de tags existentes, ausência de ciclos na taxonomia, grupos de
ambiguidade referenciados, pares de conflito distintos e regras de
normalização suportadas. Conteúdo lexical inválido deve ser reportado com
arquivo, ID e caminho; não pode ser ignorado silenciosamente.

Conteúdo JSON pode acrescentar tags e expressões dentro do contrato suportado
sem recompilação. Alterar a semântica do normalizador, adicionar operador ou
novo tipo de interpretação requer mudança versionada do motor/contrato; JSON
não pode instalar código.

## 6. Avaliação e critérios de aceite

Não prometer precisão antes de medir em textos representativos. Criar corpus
versionado e revisado por pessoas, separado do vocabulário de produção quando
usado para avaliar alterações. Rotular spans, tags esperadas, polaridade,
ambiguidade e decisão humana esperada. Incluir exemplos de português
brasileiro, variações de acentuação e pontuação, texto curto e longo, domínios
distintos e texto sem termos do vocabulário.

Métricas por tag e agregadas:

- precisão e recall de menções/tag com definição de correspondência de span;
- falsos positivos por tag e por mil palavras;
- falsos negativos por tag;
- acerto na identificação de polaridade e conflitos;
- taxa de casos corretamente enviados para confirmação/esclarecimento;
- taxa de confirmação/rejeição pelos usuários em teste, como sinal de
  usabilidade, não como verdade de referência isolada.

Reportar suporte (quantidade de exemplos), divisão do corpus e intervalos de
incerteza quando o tamanho da amostra permitir. Não combinar métricas numa
“confiança” única nem ocultar resultados por tag. Limiares numéricos de
qualidade só devem ser definidos após baseline e revisão de risco; mudanças no
vocabulário não são aceitas apenas por melhorar uma métrica agregada se
introduzirem falsos positivos críticos.

### Casos de avaliação obrigatórios

| Texto | Resultado esperado |
|---|---|
| “Precisamos de integração com ERP.” | Menções compostas e tags de integração/ERP pendentes de confirmação; sem inferir API, frequência ou tecnologia. |
| “O sistema deve suportar milhares de pedidos simultâneos.” | Escalabilidade e concorrência como hipóteses; pedir volume mensurável, cenário e unidade; não converter “milhares” em throughput. |
| “Não precisamos de alta disponibilidade.” | Tag de disponibilidade negada, span de negação preservado; não criar requisito afirmativo nem assumir que disponibilidade é irrelevante em todo contexto. |
| “Não só o checkout, mas também o catálogo precisa continuar disponível.” | Não aplicar negação a `disponível`; reconhecer possível escopo positivo e exigir revisão se a política não separar as cláusulas com segurança. |
| “O sistema não deve ficar indisponível.” | Evidência positiva de disponibilidade por construção negativa; se essa construção não estiver curada, marcar ambígua, não inventar equivalência. |
| “Precisamos de um banco.” | “Banco” ambíguo (instituição financeira ou sistema de dados); perguntar o sentido antes de criar tag. |
| “Usaremos Kafka; não queremos mensageria.” | Conflito entre menções, spans e polaridade expostos; não resolver automaticamente. |
| “Precisamos de um sistema de vendas online.” | Tag de domínio funcional proposta; não inferir pagamento, estoque, ERP ou arquitetura. |
| “A solução deve ser rápida.” | Possível desempenho, mas sem métrica; pedir latência/jornada e manter pendente. |
| “A tela não deve usar IA.” | Preservar negação e escopo local; não converter em proibição de tecnologia global sem confirmação do alcance. |
| Texto sem termos conhecidos | Nenhuma hipótese positiva; informar cobertura lexical limitada e permitir registro manual. |
| “Não é impossível manter o sistema disponível.” | Dupla negação/ambiguidade; solicitar confirmação. |

Os exemplos são casos de teste do contrato, não evidência de qualidade. Casos
de falso positivo e falso negativo devem ser adicionados ao corpus com contexto
e decisão editorial. Ex.: `banco` casando indevidamente com `banco de dados` é
falso positivo; “fila de atendimento” casando com mensageria é falso positivo;
“processamento em segundo plano” não detectado pode ser falso negativo se o
vocabulário tiver essa intenção. Corrigir pela regra lexical e pela curadoria,
nunca por uma exceção opaca na UI.

### Aceite da etapa de especificação

1. Uma mesma entrada, vocabulário e versões produzem as mesmas menções,
   hipóteses, polaridades, ordem e offsets; identificador da execução e
   timestamp podem variar.
2. Correspondência respeita tokens e acentos, expressões compostas e
   pontuação, sem match parcial arbitrário.
3. Nenhuma hipótese não confirmada alimenta fatos do motor.
4. Negação simples é preservada; escopo duvidoso, dupla negação e conflito são
   explicitamente encaminhados para revisão.
5. Quantidade, unidade, prioridade e prazo ausentes continuam desconhecidos.
6. Cada hipótese pode ser rastreada ao texto, span, entrada lexical, tag,
   versão do vocabulário e política aplicada.
7. Uma forma ambígua nunca é resolvida por ordem, peso ou escolha silenciosa.
8. Alterar JSON válido dentro do contrato muda o vocabulário sem recompilar;
   conteúdo inválido produz diagnóstico acionável.
9. O corpus cobre positivos, negativos, polissemia, negação, variantes
   ortográficas, expressões compostas, conflitos e textos sem cobertura.
10. Resultados quantitativos são reportados por tag e com baseline; não há
    promessa de precisão não medida.

## 7. Decisões ainda abertas

- Aprovar o conjunto inicial de tags e os caminhos tipados expostos ao motor.
- Definir o limite exato de cláusula e conectores de negação para `pt-BR`, a
  partir do corpus, mantendo comportamento conservador.
- Definir formato, consentimento, retenção e proteção do texto livre do usuário.
- Definir processo de curadoria, autoria, revisão e política de publicação de
  versões do vocabulário.
- Definir limiares de avaliação com usuários e risco, após obter baseline.

Essas decisões devem ser fechadas antes de implementação e não devem ser
resolvidas por quotas de conteúdo ou heurísticas sem avaliação.
