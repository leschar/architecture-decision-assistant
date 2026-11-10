# Geração de Diagramas C4

## 1. Objetivo e decisões de escopo

Diagramas são projeções versionadas e explicáveis do modelo do projeto, não
fonte independente da arquitetura nem desenhos inferidos livremente pelo
sistema. Esta especificação cobre Context, Container e Component do C4.

O gerador é determinístico e não usa IA em runtime, modelos generativos ou
serviços OpenAI/Azure OpenAI. Ele combina fatos confirmados do projeto,
decisões humanas, um modelo estrutural explícito e relações/regras elegíveis.
Texto bruto, tags lexicais pendentes, scores ou nomes de tecnologias não bastam
para inventar sistemas, containers, componentes ou conexões.

### Limite importante: nível de detalhe disponível

- **Context:** pode projetar pessoas/papéis, o sistema em escopo e sistemas
  externos quando esses elementos estão explicitamente cadastrados ou
  confirmados. Menções a ERP ou usuários não especificam por si sós protocolo,
  direção de fluxo ou sistema externo concreto.
- **Container:** exige containers executáveis/deployáveis e seus
  armazenamentos, clientes ou processos identificados. Arquitetura selecionada
  como `Microservices` não revela quantidade, limites ou nomes de serviços.
- **Component:** exige componentes internos de um container e suas relações
  explicitamente modelados. Arquitetura, pattern, regra ou tecnologia isolados
  não fornecem essa decomposição.

Quando os dados não sustentam um nível, o gerador retorna estado incompleto e
perguntas/avisos para complementação; não produz um diagrama Component
ficticiamente detalhado. A equipe pode registrar estrutura em JSON de projeto
ou adicionar um blueprint estrutural versionado ao conhecimento. Um blueprint
é um ponto de partida revisável, não uma decisão implantada: elementos
derivados dele ficam `Proposed` até confirmação.

## 2. Fontes e hierarquia de autoridade

Ordem de precedência, da maior para a menor:

1. Elementos, conexões e revisões explicitamente confirmados no projeto.
2. Decisões arquiteturais aceitas e seleções humanas vigentes.
3. Relações aplicáveis do catálogo de conhecimento e análise de grafo
   validada.
4. Regras avaliadas sobre fatos confirmados, apenas como evidência/aviso; regra
   ou efeito `Score` não define topologia por si só.
5. Requisitos confirmados, que ajudam a determinar escopo e destacar cobertura
   ou lacunas, mas não viram componentes automaticamente.
6. Recomendações e blueprints não confirmados, apresentados como proposta e
   visualmente distintos.

Rejeições e decisões substituídas não são reaplicadas. Relação `DependsOn`
indica dependência no grafo de conhecimento, mas só pode virar conexão de
arquitetura se houver uma relação estrutural específica confirmada. `Recommends`
jamais vira fluxo automaticamente. `ConflictsWith` e `AlternativeTo` são
avisos/alternativas, não conexões de runtime.

Cada nó e conexão gerados carregam referências de proveniência aos IDs e
versões dos elementos que justificam sua presença. Falta de evidência gera
aviso, não texto de justificativa fabricado.

## 3. Modelo C4 e vistas

O C4 separa níveis de abstração. Uma mesma solução pode ser mostrada em três
diagramas distintos; não se deve misturar containers e componentes na mesma
vista.

| Vista | Nós principais | Escopo |
|---|---|---|
| Context | `Person`, `SoftwareSystem` | Pessoas/sistemas externos e o sistema de software em escopo. Mostra interações relevantes, não componentes internos. |
| Container | `Person`, `SoftwareSystem`, `Container` | Um sistema em foco e seus containers, bancos, jobs, aplicações cliente ou unidades executáveis, além de atores/sistemas externos conectados. “Container” é unidade executável/deployável ou datastore, não Docker por definição. |
| Component | `Person`, `SoftwareSystem`, `Container`, `Component` | Um container em foco, seus componentes internos e dependências entre componentes; sistemas externos aparecem como contexto conectado. Não desce a classes/métodos. |

O sistema/Container em foco é identificado como `scopeId`. Fronteiras visuais
decorrem da associação dos elementos ao sistema/container, não de uma aresta.
Um elemento pode ter nome, descrição, responsabilidade e tecnologia quando
conhecidos; tecnologia em branco significa desconhecida, não “tecnologia
padrão”.

### Nós e identidade

- ID gerado é estável para a mesma identidade semântica, por exemplo
  `projectId:containerId`; não usar nome exibido como chave.
- Identidade do artefato segue `diagramId = projectId:diagramType:scopeId`;
  salvar nova revisão atualiza o snapshot lógico sem confundir diagramas de
  escopos ou níveis distintos.
- Elementos humanos ou externos precisam de ID de projeto estável ou ID
  confirmado no catálogo.
- `elementType` usa os conceitos C4 (`Person`, `SoftwareSystem`, `Container`,
  `Component`). Armazenamento, fila, tópico, serviço externo ou interface
  cliente são especializações por tags/metadados e não novos níveis C4.
- Estado `Confirmed`, `Proposed` ou `Unresolved` é explícito. Itens `Proposed`
  e `Unresolved` são visualmente distinguidos dos confirmados; não são
  apresentados como arquitetura decidida.
- Elementos sem origem/proveniência ficam identificados como fornecidos
  manualmente ou como erro de validação; não recebem proveniência fictícia.

### Conexões, eventos e fluxos

Uma conexão tem ID estável, origem e destino direcionados, descrição, tipo de
interação e proveniência. Cada conexão deve afirmar uma interação e sentido
conhecidos; ausência de protocolo ou detalhe é permitida e permanece explícita.

Tipos de interação:

- `Synchronous`: chamada/consulta com resposta no mesmo fluxo, por exemplo uma
  solicitação HTTP conhecida.
- `AsynchronousCommand`: comando enviado sem aguardar conclusão no fluxo
  chamador.
- `AsynchronousEvent`: evento/fato publicado e consumido de forma assíncrona.
- `BatchTransfer`: transferência em lote/arquivo com frequência ou janela se
  conhecida.
- `Unknown`: há uma interação identificada, mas não se sabe seu modo; exibir
  como desconhecido e pedir esclarecimento.

Eventos são nomeados na conexão (`messageName`/`eventName`) e, quando
explicitamente representados, podem aparecer como rótulos no fluxo. Não são
containers ou componentes implícitos. Apenas `AsynchronousEvent` aceita nome
de evento; consistência semântica é validada além do schema.

Não inferir uso de broker, fila, tópico, API Gateway, protocolo, entrega
exactly-once, ordem, retentativa ou consistência eventual apenas por detectar
uma conexão assíncrona ou selecionar um pattern. Esses detalhes exigem dados
confirmados ou estrutura explicitamente proposta com estado visível.

## 4. Projeção e algoritmos

### Construção da entrada

1. Capturar revisão consistente do Projeto, seleções/decisões vigentes, análise
   de grafo, catálogo/regras e modelo estrutural. Se a revisão mudar durante a
   captura, descartar o snapshot e tentar novamente.
2. Validar IDs, tipos, escopo, endpoints, referências, versões e proveniência.
3. Aplicar relações do grafo somente conforme sua semântica e aplicabilidade;
   não converter toda relação em conexão de runtime.
4. Separar elementos confirmados, propostas e lacunas.

### Context

1. Resolver o sistema em foco a partir do projeto/estrutura confirmada.
2. Incluir pessoas/atores e sistemas externos explicitamente conhecidos que
   possuam relação confirmada ou proposta identificada com o sistema.
3. Criar uma conexão por interação confirmada; agrupar interações redundantes
   apenas se direção, semântica e proveniência continuarem recuperáveis.
4. Quando uma origem de requisito disser “integração com ERP”, mas não indicar
   sistema/protocolo/direção, produzir uma lacuna ou elemento proposto
   explicitamente incerto; não desenhar fluxo confirmado.

### Container

1. Resolver o sistema raiz e seus containers da decomposição do projeto ou de
   uma decisão/blueprint confirmado.
2. Associar cada container a um propósito e, se conhecido, tecnologia,
   responsabilidade e dados persistidos.
3. Incluir atores e sistemas externos já existentes na vista Context apenas
   se tiverem conexão relevante com os containers.
4. Só incluir fronteiras e conexões respaldadas por estrutura ou decisão.
   Regras sobre microservices ou escalabilidade podem produzir avisos e
   perguntas de decomposição, não uma quantidade arbitrária de serviços.

### Component

1. Exigir `scopeId` de um container e uma decomposição explícita de componentes.
2. Incluir componentes internos e dependências direcionadas confirmadas ou
   propostas com estado.
3. Incluir atores/sistemas externos conectados somente como elementos de
   contexto, sem recriar a vista Container.
4. Se faltarem componentes, devolver `Incomplete` com lacunas; não derivar
   componentes de classes, padrões, nomes de produtos ou texto de requisito.

### Consistência entre níveis

Uma conexão Context deve ter uma rota compatível nos níveis inferiores ou ser
marcada “não detalhada nesta vista”. Ausência de detalhe não é inconsistência
se o nível inferior ainda não foi modelado. Contradição (por exemplo,
conexão externa invertida sem evidência que justifique mudança) deve gerar
aviso de validação. Um elemento de um nível pode mapear para um ou mais
elementos de nível inferior por referências explícitas; nomes parecidos não
bastam para criar o mapeamento.

## 5. Geração “em tempo real”

Interpretar “tempo real” como **pré-visualização reativa em sessão**, não como
garantia de latência rígida, atualização global instantânea ou renderização a
cada tecla:

1. A alteração relevante confirmada (ou edição estrutural em rascunho) emite
   evento interno de aplicação, como `ProjectDiagramInputsChanged`. Isso não
   exige banco, broker ou infraestrutura distribuída.
2. A UI pode aplicar debounce/coalescência curta em edições sucessivas e
   solicitar uma nova projeção em segundo plano; regras de negócio permanecem
   na Application/Domain.
3. Cada solicitação inclui revisão/hash de entradas. Resultado antigo que
   chegue após um mais novo é descartado; o preview mostra “atualizando” e
   sinaliza revisão defasada.
4. Cache, se usado, é chaveado por hash de revisão, nível, escopo e versão do
   gerador. Cache nunca pode servir conteúdo de revisão diferente como atual.
5. Preview não persiste histórico a cada alteração. Salvar/publicar um diagrama
   gera snapshot versionado quando usuário solicitar ou aceitar a versão.
6. Erros de validação e nível incompleto são expostos ao usuário; não retornar
   diagrama de sucesso vazio como fallback.

A meta de responsividade deve ser definida e medida depois de conhecer volume
de nós/conexões e ambiente. Se a atualização não terminar no tempo esperado,
preservar a visualização anterior identificada como desatualizada e informar o
estado do cálculo. Uma mudança em fato confirmado, arquitetura escolhida,
dependência relevante ou estrutura pode alterar o diagrama; mudança de score
isolada não deve alterar topologia.

`sourceFingerprint` usa SHA-256 sobre JSON canônico dos inputs semânticos
relevantes: tipo de diagrama, escopo, revisão do Projeto, IDs/versões das
decisões, modelo estrutural, relações aplicáveis, fatos tipados consultados e
versão do gerador. A serialização ordena propriedades de objetos e listas
apenas quando sua ordem não tem significado. Excluir `generatedAt`, ID de
execução, posições/layout e estado transitório do preview. Mudança de layout
não invalida o snapshot semântico; mudança de qualquer input semântico gera
novo fingerprint.

## 6. Edição manual, persistência e extensibilidade

Separar:

- **Modelo semântico:** elementos, conexões, estados e proveniência gerados ou
  confirmados; fonte da verdade do diagrama.
- **Layout/apresentação:** posição, grupos, cores, colapso e anotações visuais.
  Overrides são referenciados por IDs estáveis e não podem modificar endpoint,
  decisão, requisito ou tipo de fluxo.

Ao regenerar, manter overrides somente para elementos/arestas com IDs ainda
válidos; órfãos são reportados para revisão, não descartados silenciosamente.
Um override de posição não é uma nova conexão arquitetural.

Estrutura do projeto e overrides persistem em JSON local. O contrato de saída
está em [diagram.schema.json](../schemas/diagram.schema.json); ele documenta
conteúdo sem impor uma biblioteca de renderização. Formatos como Mermaid,
PlantUML ou Structurizr DSL podem ser avaliados como adaptadores de exportação,
mas não substituem o modelo canônico nem podem perder proveniência/estado.
Adicionar conteúdo ou blueprints JSON dentro de schemas suportados não exige
recompilação; novo tipo semântico, regra de projeção ou formato exige evolução
versionada do contrato/gerador.

O documento do Projeto pode conter uma estrutura explícita versionada pelo
schema do Projeto: elementos C4 têm IDs independentes dos nomes, estado e
proveniência; containers pertencem a sistemas, componentes pertencem a
containers e interações mantêm endpoints direcionados. A primeira captura
local inclui pessoas, sistemas, containers, componentes e os tipos síncrono,
comando/evento assíncrono, transferência em lote e desconhecido. Eventos
exigem nome; detalhes não conhecidos permanecem ausentes.

O preview atual projeta somente conexões explícitas que tocam o escopo
selecionado. Context usa um sistema de software; Container usa o sistema e
seus containers; Component usa um container e seus componentes, mantendo
elementos de contexto conectados. Ausência de decomposição ou de dados
descritivos é sinalizada como `Incomplete`. Snapshots JSON são persistidos
explicitamente pelo host local e podem ser listados e reabertos. O preview é
recalculado após alterações de revisão do Projeto e mudanças de nível/escopo;
uma sequência de requisições e cancelamento cooperativo impedem que respostas
atrasadas substituam o estado mais novo. O snapshot semântico continua em
`diagrams/<project-id>/`; overrides são editados em sessão e persistidos
separadamente em `diagrams/<project-id>/layouts/`, ligados ao `diagramId`.
`diagram.schema.json` valida apenas a semântica e
`diagram-layout.schema.json` valida os overrides. Overrides com IDs órfãos são
retornados como avisos, sem apagar silenciosamente o restante do layout.

## 7. Critérios de aceite

1. Diagramas Context, Container e Component respeitam escopo e nível C4 sem
   misturar abstrações.
2. Mesmo snapshot/versões produzem mesmos elementos, conexões, estados e
   ordem; timestamp e ID de execução podem variar.
3. Toda afirmação derivada aponta a requisito confirmado, decisão, relação,
   regra ou entrada estrutural identificável; propostas estão claramente
   diferenciadas.
4. Requisito ou tag lexical pendente não se transforma em nó/conexão confirmada.
5. Arquitetura selecionada, regra ou aresta de conhecimento não cria
   topologia que sua semântica não declara.
6. Component Diagram sem decomposição suficiente retorna estado incompleto e
   perguntas, não uma estrutura inventada.
7. Fluxos síncronos, comandos/eventos assíncronos, transferência em lote e
   modo desconhecido são distinguíveis; evento requer nome e proveniência se
   afirmado como conhecido.
8. Mudanças de arquitetura, estrutura ou fatos relevantes atualizam preview;
   resultados de revisão obsoleta não substituem revisão mais nova.
9. Edição de layout não muda o modelo semântico e regeneração não apaga
   overrides válidos.
10. Erros de schema/referência e truncamento são visíveis; nenhum diagrama
    parcial é rotulado completo.
11. Testes cobrem os três níveis, falta de contexto/containers/componentes,
    proposta versus confirmação, relações aplicáveis/desconhecidas, mudanças
    concorrentes de revisão e persistência de layout.

## 8. Trade-offs e alternativas

| Decisão | Benefício | Custo / risco | Alternativa |
|---|---|---|---|
| Modelo semântico próprio em JSON, independente do renderizador | Portável, validável, rastreável e compatível com armazenamento em arquivos | Requer adaptadores de exportação e contrato próprio | Persistir apenas Mermaid/PlantUML simplifica desenho, mas mistura semântica, layout e texto e dificulta rastreabilidade |
| Derivação conservadora com lacunas explícitas | Evita topologia fictícia e distingue decisão de recomendação | Geração inicial pode exigir modelagem manual adicional | Inferir nomes/estrutura de padrões parece mais automático, mas promete informação não fornecida |
| Preview reativo coalescido, snapshots sob demanda | UX fluida sem persistir estado efêmero a cada tecla | Preview pode mostrar brevemente estado defasado | Regenerar/persistir em toda tecla aumenta custo e pode criar ruído/histórico inútil |
| Componentes explicitamente modelados | Mantém fidelidade C4 e respeita fronteira de abstração | Component diagram pode ficar incompleto até modelagem | Gerar do código exige que a solução exista e análise de linguagem, fora do escopo de especificação atual |

## 9. Decisões em aberto

- Definir o formato JSON do modelo estrutural do projeto e dos blueprints
  versionados, incluindo identidade/estados de pessoas e sistemas externos.
- Definir se a primeira versão suporta somente um sistema/container em foco
  por diagrama ou cenários com múltiplos sistemas em escopo.
- Definir método de importação/exportação visual e compatibilidade de layout.
- Medir e aprovar metas de responsividade com volume realista, sem prometer
  “tempo real” antes da medição.
- Definir regras para deduplicar interações e mapear elementos entre níveis.
