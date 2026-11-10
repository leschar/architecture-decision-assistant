# Grafo de Conhecimento e Dependências

## 1. Objetivo e limites

O grafo relaciona itens versionados de Arquitetura, Pattern e Technology para
explicar dependências, combinações úteis, conflitos e alternativas. Ele
complementa regras e requisitos; não substitui a avaliação de adequação, não
escolhe arquitetura e não altera as seleções do projeto.

O grafo é derivado do catálogo JSON validado e do contexto confirmado do
projeto. Para as mesmas versões e entradas, a projeção e os resultados são
determinísticos. A UI apenas apresenta nós, arestas, caminhos e avisos;
validação, expansão e resolução de relações pertencem a Application/Domain.

## 2. Avaliação crítica dos tipos de relação

Usar somente os quatro tipos canônicos já definidos pelo produto:

| Tipo | Semântica | Direção |
|---|---|---|
| `DependsOn` | O item de origem requer o alvo para satisfazer a relação no contexto descrito. É a única relação que expande dependências obrigatórias. | Direcionada: origem → requisito |
| `Recommends` | O alvo é uma combinação potencialmente útil para a origem; não é obrigatório nem, por si só, uma recomendação do motor para o projeto. | Direcionada: origem → complemento |
| `ConflictsWith` | Os dois itens não devem coexistir quando as condições declaradas se aplicam. | Simétrica na projeção |
| `AlternativeTo` | Os itens são alternativas para uma necessidade/contexto comum; não implica compatibilidade nem equivalência. | Simétrica na projeção |

`Requires` é redundante com `DependsOn` e não deve ser introduzido como quinto
tipo. Em migração, `requires` só vira `DependsOn` após confirmar que a
necessidade é obrigatória e que o alvo é um item concreto. `Recommends` nunca
deve ser promovido a dependência.

Os exemplos originais são hipóteses para curadoria, não verdades universais:

- Microservices não requer sempre API Gateway; pode recomendá-lo sob uma
  necessidade confirmada de entrada/roteamento centralizado.
- Saga pode usar orquestração, coreografia por eventos ou combinação; não
  requer universalmente um message broker.
- Outbox requer uma estratégia de publicação/relay para entregar eventos, mas
  não exige um broker específico. Um broker pode ser recomendação contextual.
- Kafka não requer uma arquitetura event-driven, nem event-driven implica
  Kafka. A relação deve representar uma combinação contextual se houver
  evidência editorial suficiente.

`Supports` também não é um tipo de aresta: capacidade declarada em um item só
vira relação se sua semântica for explicitamente `Recommends` ou outra relação
canônica.

## 3. Modelo do grafo

### Nós

O grafo arquitetural inicial projeta apenas itens com `kind`:
`Architecture`, `Pattern` ou `Technology`. Cada nó usa `id`, `kind`, nome e
versão do item no catálogo fixado. Princípios, práticas, regras, templates e
TradeOffs podem influenciar regras ou explicações, mas não viram nós desse
grafo sem decisão de produto e contrato separados.

Nós do catálogo não são escolhas do projeto. Uma análise mantém papéis de
execução separados:

- `Root`: item selecionado/confirmado pelo usuário ou candidato sob análise;
- `Required`: alvo encontrado por `DependsOn`, ainda não necessariamente
  selecionado;
- `Suggested`: alvo `Recommends`, sempre opcional;
- `Alternative`: alvo de `AlternativeTo`;
- `Conflict`: item relacionado por conflito.

Um nó pode ter mais de um papel. Papel `Required` não significa que o item foi
aceito ou instalado.

### Arestas

Uma aresta persistida pertence ao item de origem. Contém tipo, `targetId`,
justificativa e, opcionalmente, aplicabilidade declarativa e fonte específica.
As relações permanecem direcionadas no armazenamento. Na projeção, conflitos e
alternativas são consultáveis dos dois lados sem duplicar sua declaração.

Chave estável de aresta calculada para rastreio:

```text
relationKey = sourceId + "/" + type + "/" + targetId + "/" + conditionKey
```

`conditionKey` é a serialização canônica de `appliesWhen`, ou `always` quando
ausente. A serialização canônica ordena propriedades de objetos, preserva a
ordem semântica de arrays e não inclui texto de justificativa. Relações
duplicadas com a mesma chave no mesmo item são erro; relações iguais com
condições diferentes podem coexistir.

As arestas referenciam o catálogo por IDs; o snapshot da análise preserva hash
do catálogo e versões dos nós usados, evitando que atualização de conteúdo
reescreva análises anteriores.

### Aplicabilidade

`appliesWhen` reutiliza a DSL limitada e a lógica de três valores do motor:
`True`, `False` ou `Unknown`. Os caminhos devem ser fatos tipados registrados,
como `project.*` ou `candidate.*`; nenhum script, expressão livre ou acesso
externo é permitido.

- `True`: relação considerada no contexto.
- `False`: relação não aplicável; não expande, recomenda nem sinaliza conflito.
- `Unknown`: resultado inconclusivo, exibido com fatos faltantes; não é tratado
  como `True` ou `False`.
- Ausência de `appliesWhen`: relação declarada como incondicional.

O campo legado `conditions` (lista de descrições textuais) não é executável.
Pode permanecer para compatibilidade editorial, mas jamais controla
expansão automática; relações contextuais devem migrar para `appliesWhen`.

## 4. Invariantes e validação do catálogo

Antes de construir o grafo:

1. Validar sintaxe JSON, schema, versões e IDs únicos do catálogo.
2. Confirmar que cada `targetId` existe. A projeção contém arestas apenas se
   origem e alvo forem Architecture, Pattern ou Technology. Relações entre
   outros tipos continuam válidas no catálogo, mas ficam fora deste grafo; se
   uma relação pretendida para o grafo cruzar esse limite, emitir diagnóstico
   explícito em vez de descartá-la silenciosamente.
3. Normalizar itens legados `kind: "Dependency"` em relações em memória,
   validando IDs de origem/destino e emitindo aviso de migração. Rejeitar
   duplicação com uma relação já declarada no item de origem.
4. Rejeitar auto-arestas, tipos desconhecidos, justificativa vazia, relação
   duplicada e fatos/operadores inválidos em `appliesWhen`.
5. Conferir se as referências de fatos existem no registro tipado do motor e
   se os valores têm tipos compatíveis; fatos ausentes no projeto em runtime
   são `Unknown`, não erro de schema.
6. Detectar ciclos em `DependsOn`. Ciclos são diagnóstico de integridade do
   catálogo; não executar expansão recursiva através deles nem ocultar o erro.
7. Consolidar a consulta reversa de `ConflictsWith` e `AlternativeTo` sem
   duplicar arestas persistidas. Declaração redundante recíproca deve ser
   reportada e normalizada apenas se tipo, alvo e condições forem idênticos;
   divergências exigem curadoria.
8. Uma referência inválida impede a publicação do grafo afetado; não fazer
   carregamento parcial silencioso.

`DependsOn` pode ter grafos acíclicos com mais de um caminho para o mesmo alvo.
Não rejeitar redundância transitiva: preservá-la como declarações distintas,
mostrar caminho(s) e evitar duplicar o nó expandido.

## 5. Algoritmo de análise e expansão

Entrada: snapshot do catálogo, raízes confirmadas/candidatas, seleção atual do
projeto, fatos confirmados e desconhecidos, versão do avaliador e limites
operacionais registrados.

1. Validar e fixar hashes/versões do projeto, catálogo e avaliador.
2. Criar uma fila determinística com raízes ordenadas por ID.
3. Para cada nó removido da fila, consultar relações de saída válidas,
   ordenadas por tipo, `targetId` e `relationKey`.
4. Avaliar `appliesWhen` em três valores. Guardar resultado e fatos
   desconhecidos. `False` não prossegue; `Unknown` gera resultado inconclusivo
   e não é assumido como aplicável.
5. Para `DependsOn` aplicável, registrar aresta e nó com papel `Required`;
   comparar o alvo com as seleções existentes (`Satisfied` se confirmado,
   `Missing` se ausente). Continuar expansão transitiva do alvo, sem
   selecioná-lo automaticamente.
6. Para `Recommends`, registrar o alvo como `Suggested`; não atravessar por
   padrão as relações de recomendação do alvo. Expansão posterior exige que
   esse nó seja promovido explicitamente a raiz/candidato ou uma opção
   identificada de exploração.
7. Para `ConflictsWith`, consultar a seleção e candidatos do mesmo cenário.
   Registrar conflito quando ambos forem selecionados e a relação aplicável;
   caso contrário, mostrar risco/relevância sem excluir um lado.
8. Para `AlternativeTo`, agrupar os itens para comparação sem expandir como
   dependência nem pontuar um pela existência do outro.
9. Deduplicar nós por ID e arestas pela `relationKey`; preservar cada caminho
   de dependência que explica como uma raiz alcança o nó.
10. Ordenar nós, arestas, avisos e caminhos de forma estável. Se limites
    operacionais impedirem expansão completa, marcar resultado truncado e
    explicar o limite; nunca apresentar grafo parcial como completo.

Os limites de profundidade/quantidade protegem recursos e devem ser definidos
pela implementação a partir de testes de carga, não escolhidos para inflar o
modelo. Ciclos já devem ser barrados na validação; proteção de runtime ainda
mantém conjunto de visitados.

Pseudocódigo:

```text
fila = raízes ordenadas
visitados = {}
enquanto fila não vazia e dentro dos limites:
    origem = retirar(fila)
    se origem já expandida: continuar
    marcar origem expandida
    para aresta de saída ordenada:
        aplicabilidade = avaliar(appliesWhen, fatos, 3 valores)
        registrar evidência e provenance
        se aplicabilidade != True: continuar
        conforme tipo:
            DependsOn:
                marcar alvo Required e avaliar atendimento
                enfileirar alvo
            Recommends:
                marcar alvo Suggested; não enfileirar automaticamente
            ConflictsWith:
                reportar somente conflito contextual/seleção aplicável
            AlternativeTo:
                agrupar alvo para comparação
retornar snapshot, caminhos, desconhecidos e avisos
```

## 6. Recomendação e ranking

O grafo sozinho não calcula adequação. A recomendação resulta da combinação
dos requisitos confirmados, regras e relações:

1. O motor de regras calcula score/elegibilidade de candidatos separados por
   tipo, conforme [rule-engine.md](./rule-engine.md).
2. `Recommends` pode acrescentar candidato complementar para inspeção quando
   a relação é aplicável e tem proveniência. Isso é sugestão de exploração,
   não recomendação final nem score positivo.
3. A relação não altera score por grau, número de vizinhos, popularidade ou
   quantidade de caminhos. Um efeito no score exige regra explícita e
   justificativa independente.
4. `DependsOn` não aumenta score: dependências não selecionadas aparecem como
   ausentes e podem tornar a proposta incompleta. A política de elegibilidade
   deve distinguir candidato de arquitetura versus dependência ainda não
   escolhida; nunca afirmar satisfação automática.
5. `ConflictsWith` é alerta/exclusão somente quando a seleção conjunta e a
   condição são verdadeiras e a semântica autoral é clara. O grafo reporta o
   conflito; não escolhe qual item remover. `Unknown` gera estado inconclusivo.
6. `AlternativeTo` organiza comparação; os scores individuais permanecem
   separados e não se herdam.
7. Inbound/outbound, transitividade, quantidade de caminhos e posição visual
   não implicam preferência.

O usuário pode confirmar/rejeitar itens e responder lacunas; somente essa ação
atualiza o Projeto e pode iniciar nova análise.

## 7. JSON canônico

Relações são declaradas no item de conhecimento. Exemplo conceitual (os IDs
alvo devem existir no catálogo; o caminho de fato é ilustrativo e só pode ser
usado se registrado no motor):

```json
{
  "id": "microservices",
  "kind": "Architecture",
  "schemaVersion": "1.0",
  "version": "1.1.0",
  "name": "Microservices",
  "description": "Arquitetura de serviços implantáveis independentemente.",
  "category": "Distributed",
  "advantages": ["Implantação independente quando limites e equipes permitem."],
  "disadvantages": ["Aumenta custos de operação distribuída."],
  "useWhen": ["Há autonomia e limites de domínio estáveis."],
  "avoidWhen": ["A organização não pode sustentar operação distribuída."],
  "complexity": 9,
  "scalability": 9,
  "relations": [
    {
      "type": "Recommends",
      "targetId": "api-gateway",
      "rationale": "Pode centralizar políticas de entrada quando o sistema expõe múltiplos serviços.",
      "appliesWhen": {
        "fact": "project.context.exposesMultipleServices",
        "operator": "Equals",
        "value": true
      }
    }
  ]
}
```

O exemplo não declara `DependsOn`: API Gateway não é requisito universal de
Microservices. A relação é apenas ilustrativa e exige revisão do fato e da
fonte antes de entrar em conteúdo de produção.

`requires` não é aceito no formato canônico. Formato mínimo de aresta:

```json
{
  "type": "DependsOn",
  "targetId": "message-publisher",
  "rationale": "O padrão precisa de um mecanismo que publique os eventos gravados."
}
```

Esse exemplo só descreve a necessidade de publicação do padrão Outbox; não
afirma que Message Broker seja obrigatório. Relação contextual adiciona
`appliesWhen`, nunca uma expressão em texto livre.

Schemas:

- [knowledge-item.schema.json](../schemas/knowledge-item.schema.json): item e
  relações de catálogo;
- [graph-analysis.schema.json](../schemas/graph-analysis.schema.json):
  resultado reprodutível de expansão e análise;
- [recommendation.schema.json](../schemas/recommendation.schema.json):
  recomendações e achados resumidos da análise.

## 8. Resultado de análise do grafo

O resultado imutável registra:

- IDs das raízes e papéis no cenário;
- hash/revisão do projeto e do catálogo, versão do avaliador e momento da
  execução;
- nós encontrados e papéis (`Required`, `Suggested`, `Alternative`,
  `Conflict`);
- cada aresta considerada, chave estável, tipo, versões dos itens, condição,
  aplicabilidade, achado (`Satisfied`, `Missing`, `Conflict`, `Alternative`,
  `Unknown` ou `NotEvaluated`), justificativa e proveniência declarada;
- caminhos de dependência da raiz até cada alvo;
- fatos desconhecidos, avisos e eventual truncamento.

Não copiar descrições mutáveis do catálogo sem versão/hash que permita
reconstruí-las. O resultado não grava uma alteração de seleção no Projeto.

### Recorte implementado no MVP

O snapshot de análise 2.0 inclui uma projeção estável identificada pelo hash do
catálogo, com nós ordenados por ID, relações ordenadas por tipo/alvo/chave,
versões, arquivo de origem e proveniência editorial. `relationKey` usa a
serialização determinística de `appliesWhen` (ou `always`). Declarações
recíprocas idênticas de conflito/alternativa são representadas uma vez, com
ambas as declarações e fontes preservadas; condições recíprocas divergentes
impedem a projeção. Para avaliar uma relação simétrica recíproca, cada
declaração é avaliada com os fatos do seu candidato de origem; `True` em
qualquer declaração torna a relação aplicável, e `Unknown` permanece quando
nenhuma é `True` e pelo menos uma é desconhecida. Referências inválidas,
endpoints fora dos três tipos
permitidos e ciclos `DependsOn` retornam erros sem projeção parcial.

A análise avalia relações diretas para candidatos, reutilizando o avaliador
tri-state da Fase 2. Predicados `candidate.*` são avaliados contra o candidato
de origem da declaração. Cada achado guarda chave, versões, arquivo de
catálogo, fonte declarada, justificativa, aplicabilidade e fatos desconhecidos.
Dependências são `Satisfied` somente para uma decisão `Accepted` existente e
`Missing` caso contrário; conflitos só são afirmados quando ambos os itens têm
decisão `Accepted`. Recomendações, alternativas e relações inconclusivas
permanecem sugestões/avisos, não pontuam e não alteram decisões.

O snapshot também contém expansão transitiva de `DependsOn` para cada raiz
candidata (ou seleção já aceita), com caminhos de chaves de relação,
deduplicação de nós e estado de seleção explícito. Uma aresta `Unknown` registra
lacuna e `PotentialRequired`, mas não é atravessada. `Recommends` não é
percorrida pela expansão. A execução limita cada projeção a 200 nós, 32 níveis
de dependência e 5.000 caminhos de relação avaliados no total; o atingimento de
limite marca o resultado como truncado e inclui aviso. Esses limites são
proteções iniciais do MVP e precisam ser revisitados com medições em catálogos
reais.

Uma consulta de comparação recebe o projeto, o snapshot e dois IDs de
candidatos distintos. Ela retorna os achados de relação explícita entre o par,
as dependências expandidas de cada raiz e os IDs de dependências aplicáveis
compartilhadas ou exclusivas. Scores e cobertura permanecem separados por
candidato; a comparação não os agrega nem os recalcula. Relações `Unknown` e
seus fatos desconhecidos permanecem visíveis nos achados, mas não entram nos
conjuntos de requisitos aplicáveis. O estado informa expansão completa,
truncada, apenas direta ou indisponível; snapshots históricos sem dados de grafo
continuam comparáveis apenas pelos achados que já armazenam, com a limitação
explícita. A consulta é somente leitura: não grava decisões, não escolhe uma
alternativa, não confirma seleções e não transforma relações em topologia.

A navegação textual/acessível do catálogo está implementada: nós e relações
podem ser filtrados e explorados por qualquer extremidade, com direção,
justificativas e proveniência explícitas. Condições contextuais não são
avaliadas nessa consulta de navegação. Navegação contextual por projeto e
diagramas C4 ainda não foram implementados. `Missing` descreve somente a
ausência de seleção confirmada do alvo no cenário atual, e não escolhe nem
confirma esse alvo.

## 9. Critérios de aceite

1. `Requires` não é um tipo executável separado; migração só o converte após
   verificar obrigatoriedade.
2. Relações inválidas, duplicadas, autorreferentes, referências quebradas e
   ciclos `DependsOn` são diagnosticados com arquivo, item e caminho JSON.
3. As mesmas entradas e versões retornam mesmos nós, arestas, estados e ordem;
   IDs/timestamps de execução podem variar.
4. `Unknown` em `appliesWhen` nunca expande dependência nem confirma conflito.
5. Uma dependência transitiva mostra caminho e estado de atendimento sem
   selecionar o nó automaticamente.
6. Uma recomendação não vira dependência, score nem escolha por aparecer no
   grafo.
7. `AlternativeTo` permite comparação sem score herdado ou adoção conjunta
   implícita.
8. Conflitos só são afirmados quando condição e coexistência no cenário são
   conhecidas; o motor não escolhe qual lado remover.
9. Relações reversas simétricas são consultáveis sem persistência duplicada.
10. Conteúdo JSON válido estende nós/arestas sem recompilar; JSON inválido ou
    incompatível não publica grafo parcial.
11. Limites operacionais e truncamento são visíveis; nenhum resultado
    incompleto é apresentado como expansão completa.
12. Diagramas e recomendações apontam para as mesmas identidades, estados e
    proveniência do resultado de grafo.

## 10. Decisões em aberto

- Aprovar o registro tipado de fatos `project.*` usado por `appliesWhen`.
- Definir se itens fora de Architecture/Pattern/Technology entram em uma
  visualização de conhecimento futura.
- Definir política de seleção/elegibilidade para dependência ausente por tipo
  de candidato.
- Medir limites operacionais com tamanho de catálogo e cenários esperados.
- Curar fontes e relações do catálogo; os exemplos do pedido não são
  automaticamente aceitos como relações corretas.
