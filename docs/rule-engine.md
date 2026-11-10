# Motor de Regras e Decisão

## 1. Objetivo

O motor avalia requisitos e contexto de um projeto contra itens de conhecimento
e regras declarativas para produzir uma lista comparável de candidatos. Cada
resultado deve ser reproduzível e explicar as evidências favoráveis,
desfavoráveis, ausentes e as condições que impedem uma recomendação.

O motor não escolhe arquitetura ou tecnologia em nome do usuário, não executa
ações e não usa IA em runtime.

## 2. Escolha de abordagem

Há motores de regras .NET prontos, mas eles não são equivalentes entre si nem
se encaixam automaticamente nas restrições do produto:

- **Microsoft RulesEngine** aceita workflows/regras em JSON e suporta .NET 9.
  Seu formato documentado permite expressões lambda em texto. É uma opção de
  infraestrutura possível, mas executar expressões arbitrárias fornecidas
  pelos arquivos de conhecimento não combina com a exigência de uma DSL
  limitada, segura, determinística e sem código em runtime.
- **NRules** implementa o algoritmo Rete, mas suas regras são escritas em DSL
  interna C#. Isso é útil para regras de produção complexas, porém não atende
  diretamente à extensão de conteúdo por JSON sem recompilar ou alterar código.

**Recomendação para este produto:** começar com uma DSL declarativa pequena e
um avaliador próprio, sem expressões C#, scripts, reflexão ou execução de
código contido nos JSON. Isso mantém o catálogo extensível e torna o conjunto
de operações auditável. Uma biblioteca só deve ser adotada se uma prova de
conceito demonstrar que preserva essa restrição e simplifica a implementação.

Fontes consultadas:

- [Microsoft RulesEngine — README oficial](https://github.com/microsoft/RulesEngine)
- [NRules — README oficial](https://github.com/NRules/NRules)

Essa escolha é uma recomendação de desenho, não uma afirmação de que o produto
precise criar um motor genérico de propósito geral.

## 3. Limites e responsabilidades

### O motor deve

- Receber um retrato validado e identificável do projeto.
- Consumir somente requisitos/fatos confirmados pelo usuário. Hipóteses
  lexicais pendentes, ambíguas, negadas ou rejeitadas não são fatos positivos.
- Avaliar regras determinísticas sobre contexto, requisitos e candidatos do
  catálogo.
- Produzir score, cobertura de evidências, avisos e explicações rastreáveis.
- Identificar exclusões explícitas, dependências, conflitos e alternativas.
- Ordenar candidatos comparáveis de modo estável.
- Expor versões e proveniência para reprodução da análise.

### O motor não deve

- Interpretar texto livre diretamente; a etapa de elicitação transforma texto
  em hipóteses revisáveis e somente depois da confirmação em requisitos/fatos
  antes da análise. Ver
  [requirement-text-interpretation.md](./requirement-text-interpretation.md).
- Executar código, expressões arbitrárias, consultas externas ou scripts
  declarados no JSON.
- Inferir que requisito não informado foi satisfeito ou negado.
- Tratar score como probabilidade, certeza ou qualidade absoluta.
- Resolver conflitos escolhendo silenciosamente um dos lados.
- Converter uma recomendação em decisão confirmada.

## 4. Entradas da análise

Uma execução recebe:

1. **Retrato do projeto:** identificador e revisão/hash, contexto confirmado,
   requisitos estruturados, restrições e escolhas já registradas.
2. **Candidatos:** itens do catálogo elegíveis para avaliação, agrupados por
   tipo. Arquiteturas, tecnologias e patterns não são comparados em um único
   ranking.
3. **Regras habilitadas:** regras válidas, com versão e ordem determinística.
4. **Catálogo e grafo:** itens e relações validados, com versão/hash.
5. **Perfil de análise:** escopo dos tipos de candidato e, futuramente, pesos
   de critérios. No MVP há uma dimensão comum de adequação (`fit`); comparações
   entre critérios ou projetos exigem perfil explicitamente identificado.
6. **Versão do motor e dos contratos:** registradas no resultado.

Dados ausentes permanecem ausentes. Não se aplicam defaults silenciosos a
requisitos ou fatos de domínio.

## 5. Modelo da regra

Uma regra tem:

- **Identidade e versão:** `id`, `schemaVersion`, `version`, `name` e
  `description`.
- **Categoria e estado:** `category` e `enabled`.
- **Seleção de candidatos (`appliesTo`):** tipos obrigatórios e filtros
  opcionais por IDs ou tags.
- Quando ambos os filtros opcionais estão presentes, o candidato precisa
  corresponder ao ID **e** compartilhar pelo menos uma tag; dentro de `tags`,
  basta uma correspondência. IDs explícitos devem referenciar itens do
  catálogo de um dos tipos declarados em `kinds`.
- **Aplicabilidade (`appliesWhen`, opcional):** condições sobre fatos do
  projeto que determinam se a regra é relevante. Deve usar fatos do projeto,
  não expressões de código.
- **Condição (`conditions`):** árvore de predicados que pode consultar fatos
  do projeto e do candidato.
- **Efeitos (`effects`):** um ou mais efeitos auditáveis:
  - `Score`: contribuição favorável ou desfavorável na dimensão `fit`;
  - `Exclude`: deve declarar `hardConstraint: true` e marca candidato como
    inelegível quando um critério rígido é verdadeiro;
  - `Flag`: adiciona risco, alerta ou observação sem alterar o score.
- **Limite no MVP:** no máximo um efeito `Score` por regra; efeitos adicionais
  podem ser `Flag` ou `Exclude`. Isso mantém inequívoco o peso daquela regra
  no cálculo.
- **Peso (`weight`):** importância relativa da contribuição de score, inteiro
  positivo. Não representa probabilidade.
- **Proveniência:** fonte editorial opcional e metadados comuns do catálogo.

Dependências e recomendações entre itens são expressas pelas relações
`DependsOn`, `Recommends`, `ConflictsWith` e `AlternativeTo` do catálogo. Não
duplicar essas relações como efeitos de regra sem necessidade.

### Fatos e operadores

Fatos usam caminhos estáveis com namespace:

- `project.*`: contexto confirmado, como tamanho de equipe e requisitos
  estruturados;
- `candidate.*`: campos do item candidato, incluindo `id`, `kind`, `tags`,
  categoria e atributos tipados definidos pelo catálogo;
- `selection.*`: escolhas já confirmadas pelo usuário, quando disponíveis.

Operadores permitidos na primeira versão:

`Equals`, `NotEquals`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`,
`LessThanOrEqual`, `Contains`, `In`, `Exists` e `NotExists`.

O motor mantém um registro de fatos conhecidos e seus tipos. Caminhos
desconhecidos, operador incompatível com o tipo do fato ou comparações entre
tipos não convergíveis são erros de validação da regra ou condições
indeterminadas, nunca coerções implícitas.

Expressões Booleanas usam `allOf`, `anyOf` e `not`. A gramática não oferece
chamadas de função, indexação dinâmica, atribuição, reflexão, acesso a arquivos,
I/O ou execução de métodos.

## 6. Avaliação determinística

### Fases

1. **Carregar e validar:** validar JSON, schema, versões, IDs, referências,
   operadores e tipos dos fatos. Regra inválida não é executada.
2. **Fixar entrada:** calcular ou registrar hash do retrato de projeto, do
   catálogo e do conjunto de regras usados.
3. **Enumerar candidatos:** selecionar somente os itens dos tipos pedidos e
   que passam pelo filtro `appliesTo`.
4. **Determinar aplicabilidade:** avaliar `appliesWhen`. `False` significa que
   a regra não se aplica; `Unknown` gera aviso de informação insuficiente e
   não produz efeito.
5. **Avaliar condições:** usar lógica de três valores (`True`, `False`,
   `Unknown`).
6. **Aplicar efeitos:** efeitos são aplicados somente quando as condições são
   `True`; `False` não altera o resultado e `Unknown` não pontua nem exclui.
7. **Calcular scores e cobertura:** agregar contribuições conhecidas por
   candidato.
8. **Expandir relações:** identificar dependências e recomendações requeridas,
   conflitos e alternativas relevantes.
9. **Ordenar e explicar:** formar grupos por tipo, preservar avisos e retornar
   evidências por regra e relação.

### Lógica de três valores

- `allOf`: `False` se qualquer filho for `False`; `True` se todos forem
  `True`; caso contrário `Unknown`.
- `anyOf`: `True` se qualquer filho for `True`; `False` se todos forem
  `False`; caso contrário `Unknown`.
- `not`: inverte `True`/`False`; mantém `Unknown`.
- Ausência de um fato referenciado é `Unknown`, não `False`.

Essa semântica evita excluir ou penalizar um candidato apenas porque o usuário
ainda não informou uma resposta.

### Subconjunto executável da Fase 2

A versão atual avalia as árvores `allOf`, `anyOf` e `not` sobre os fatos
tipados definidos em `RuleFactRegistry`: contexto confirmado do Projeto,
contagens de requisitos confirmados e atributos tipados do candidato. Os
operadores executáveis são validados contra o tipo de cada fato antes de o
catálogo ser publicado. `appliesWhen` aceita somente fatos `project.*`.

Fatos opcionais não informados, como tamanho de equipe, domínio, categoria e
atributos de candidato, permanecem `Unknown`; contagens derivadas de coleções
conhecidas podem ser avaliadas como zero. `Unknown` em `appliesWhen` não aplica
efeitos; `Unknown` em `conditions` não pontua, não gera `Flag` e não exclui.
Uma restrição `Exclude` desconhecida impede declarar a elegibilidade como
confirmada. Evidências registram a fase (aplicabilidade ou condição), fato,
operador, valor esperado e valor observado.

Snapshots novos usam schema `2.0`; o repositório continua aceitando snapshots
`1.0` existentes para preservar o histórico imutável. Expansão de relações,
interpretação de texto livre e fatos fora do registro tipado permanecem fora
deste recorte.

### Pseudofluxo

```text
para cada candidato do tipo solicitado:
    obter regras habilitadas cujo appliesTo inclui o candidato
    avaliar aplicabilidade de cada regra
    para regras aplicáveis:
        avaliar conditions em lógica de três valores
        se True: registrar evidência e aplicar efeitos
        se False: registrar avaliação sem contribuição
        se Unknown: registrar fato ausente e não inferir efeito
    calcular score e cobertura
    aplicar relações de dependência/conflito/alternativa
ordenar candidatos elegíveis dentro de cada tipo
retornar elegíveis, excluídos, avisos e explicações
```

## 7. Pontuação

### Score de adequação inicial

Na primeira versão existe uma dimensão agregada `fit`. Cada efeito `Score`
possui `points` entre `-100` e `100`, e sua regra tem peso inteiro positivo
`weight`.

Para candidato `c`, considere somente regras:

- habilitadas;
- cujo `appliesTo` inclui `c`;
- cuja aplicabilidade foi avaliada como verdadeira;
- cuja condição terminou em `True` ou `False`.

Uma condição `True` contribui com seus `points`. Uma condição `False` contribui
com zero. Uma condição `Unknown` não entra no denominador do score, mas reduz
a cobertura de evidência.

```text
score(c) = 50 + 50 * sum(weight(r) * points(r)) / (100 * sum(weight(r)))
```

O resultado é limitado ao intervalo `[0, 100]`. Se não houver regra de score
conhecida aplicável, o score é `null`/“não avaliado”, nunca `50` por padrão.
Com regras aplicáveis e avaliadas, 50 representa evidência neutra; acima de
50, evidência favorável; abaixo de 50, evidência desfavorável.

### Limites da escala

- Pesos são importância relativa configurada editorialmente, não estatística.
- O score só é comparável entre candidatos avaliados com o mesmo perfil,
  conjunto e versões de regras, tipos e critérios.
- O usuário deve conseguir inspecionar cada contribuição e seu peso.
- Não arredondar contribuições intermediárias; arredondar apenas a apresentação.
- Score não substitui restrições rígidas, conflitos ou julgamento humano.
- Pesos iguais podem ser o ponto inicial, mas precisam estar explícitos no JSON
  ou no perfil de análise; não esconder ponderação de produto no código.

### Evolução para múltiplos critérios

Critérios como custo, escalabilidade e operabilidade devem ser exibidos
separadamente antes de uma nota composta. Um score agregado entre dimensões só
deve ser criado quando um perfil de análise versionado especificar pesos,
normalização e política para critérios ausentes. Não somar dimensões
incomensuráveis por conveniência.

## 8. Cobertura de evidências e confiança

O termo **confiança** pode sugerir probabilidade calibrada; por isso, o MVP
deve expor **cobertura de evidências**, não uma probabilidade de acerto.

```text
cobertura(c) =
  peso das regras de score aplicáveis e avaliadas como True ou False
  / peso das regras aplicáveis ou potencialmente aplicáveis
```

- Regras com `appliesWhen: False` não entram no numerador nem denominador.
- Regras com `appliesWhen: Unknown` entram apenas no denominador, pois a
  relevância não pôde ser confirmada.
- Regras cuja aplicabilidade é `True` e condição é `Unknown` entram apenas no
  denominador.
- O score é calculado apenas com regras cuja aplicabilidade é `True` e cujas
  condições são conhecidas (`True` ou `False`).
- Valor no intervalo `[0, 1]`, acompanhado da quantidade de regras e fatos
  desconhecidos.
- `1` indica que todas as regras de score aplicáveis foram avaliadas, não que a
  recomendação é correta.
- `0` ou denominador inexistente deve ser representado como “sem evidência
  suficiente”, não como confiança zero enganosa.
- Regras `Exclude` e `Flag` não entram no denominador do score, mas seus estados
  e fatos ausentes são exibidos separadamente.
- Só chamar esse valor de “confiança” após definição e calibração empírica.

## 9. Exclusões, conflitos e dependências

### Exclusão

`Exclude` torna o candidato inelegível apenas quando uma regra explicitamente
marcada como restrição rígida tem condição `True`. O resultado excluído continua
visível com justificativa e pode exibir seu score informativo, mas não ocupa
posição no ranking recomendável.

Uma preferência desfavorável deve usar `Score` negativo ou `Flag`, nunca
`Exclude`.

Se uma regra rígida `Exclude` puder se aplicar, mas sua aplicabilidade ou
condição for `Unknown`, o candidato recebe estado `NotEvaluated` até que o fato
necessário seja esclarecido. O motor não o declara elegível nem o exclui por
suposição.

### Conflito entre itens

`ConflictsWith` gera um conflito explícito quando ambos os itens estão
selecionados ou são necessários para o mesmo cenário. O motor:

- identifica os dois itens e a relação/proveniência;
- explica condições e consequências;
- não descarta arbitrariamente um dos candidatos;
- pede decisão humana ou apresenta alternativas separadas.

Uma regra `Exclude` pode formalizar uma restrição rígida específica, mas não
substitui o conflito contextual do grafo.

### Dependências

Uma relação `DependsOn` de um candidato recomendado:

- expande a análise para mostrar o item requerido;
- indica se a dependência já está selecionada, pode ser atendida ou está
  ausente;
- marca a recomendação como incompleta/bloqueada conforme a semântica
  declarada da relação;
- não cria automaticamente uma decisão ou instalação.

A semântica detalhada do grafo, expansão, chaves de aresta e snapshot está em
[graph-dependency-model.md](./graph-dependency-model.md). Expansão automática
transitiva é restrita a `DependsOn`; uma `Recommends` não soma score nem causa
expansão recursiva. Condições de arestas usam a mesma avaliação tri-state:
`Unknown` não confirma uma dependência nem um conflito.

Dependências transitivas devem detectar ciclos e apresentar o caminho percorrido.
O limite de profundidade e comportamento para ciclos devem ser configurados e
registrados na análise.

### Alternativas

`AlternativeTo` agrupa candidatos para comparação. Não soma seus scores, não
implica compatibilidade e não recomenda adoção simultânea. Os scores permanecem
por candidato e dentro do mesmo tipo de decisão.

## 10. Ranking

1. Separar candidatos por tipo e perfil de avaliação.
2. Ordenar candidatos elegíveis; apresentar separadamente os não avaliados e
   manter excluídos numa seção visível.
3. Ordenar elegíveis por score decrescente quando há score comparável.
4. Em empate, maior cobertura de evidências vem primeiro.
5. Em novo empate, ordenação por `id` ordinal garante apresentação estável,
   sem significar preferência arquitetural.
6. Exibir conflitos, dependências não atendidas e riscos como dimensões próprias;
   não escondê-los num número único.
7. Sem score conhecido, mostrar candidatos agrupados como “não avaliados”,
   sem inventar ranking.

## 11. Explicabilidade e resultado da análise

Cada candidato deve ter:

- score ou estado “não avaliado”;
- cobertura de evidências e fatos ausentes;
- lista de regras aplicáveis com resultado (`Matched`, `NotMatched`,
  `Unknown`);
- contribuição de cada regra: efeito, peso, pontos e justificativa;
- exclusões, conflitos, dependências e alternativas relevantes;
- benefícios, riscos e trade-offs do item do catálogo;
- versões/hash do projeto, regras, catálogo, perfil e motor;
- limitações que impedem conclusão.

A justificativa não deve ser texto genérico produzido sem proveniência. Cada
afirmação derivada deve apontar para uma regra, requisito, item ou relação.

## 12. Exemplo de regra JSON

Este exemplo reduz a adequação de microservices quando a equipe é pequena. Ele
não exclui a arquitetura: a decisão continua comparável e humana.

```json
{
  "id": "small-team-microservices-cost",
  "kind": "Rule",
  "schemaVersion": "1.0",
  "version": "1.0.0",
  "name": "Equipe pequena e custo operacional de microservices",
  "description": "Registra o custo operacional adicional de microservices para equipes pequenas.",
  "category": "ArchitectureFit",
  "enabled": true,
  "weight": 3,
  "appliesTo": {
    "kinds": ["Architecture"],
    "ids": ["microservices"]
  },
  "conditions": {
    "allOf": [
      {
        "fact": "project.teamSize",
        "operator": "LessThanOrEqual",
        "value": 8
      }
    ]
  },
  "effects": [
    {
      "type": "Score",
      "dimension": "fit",
      "points": -60,
      "rationale": "Uma equipe pequena pode ter dificuldade para sustentar a carga operacional de serviços distribuídos."
    },
    {
      "type": "Flag",
      "severity": "Warning",
      "rationale": "Validar capacidade operacional, observabilidade e autonomia dos times antes de escolher microservices."
    }
  ]
}
```

## 13. Regras de exemplo para cenários distintos

Os exemplos adicionais devem cobrir:

- preferência gradual (`Score`) versus bloqueio real (`Exclude`);
- informação não respondida (`Unknown`);
- condições combinadas com `allOf` e `anyOf`;
- regra que produz `Flag` sem score;
- dependência e conflito provenientes do grafo;
- empate estável e candidato sem nenhuma evidência pontuável.

Adicionar exemplos não deve exigir mudança no código do motor. Já adicionar um
novo operador, tipo de efeito ou fato primitivo é evolução do contrato e requer
revisão de versão e testes.

## 14. Segurança, confiabilidade e limites operacionais

- Rejeitar campos inesperados, operadores não permitidos e referências
  inválidas.
- Não aceitar expressões executáveis, scripts, reflection names, URLs ou
  consultas arbitrárias como parte da regra.
- Definir limites para tamanho do arquivo, profundidade de grupos, quantidade de
  condições/efeitos, quantidade de regras e expansões do grafo.
- Detectar IDs duplicados, dependências cíclicas e referências quebradas.
- Manter avaliação sem I/O e sem mutação de entrada.
- Repetir análise com as mesmas entradas e versões deve produzir resultado
  equivalente e ordenação estável.
- Erros de validação devem apontar arquivo, ID da regra, caminho do campo e
  causa; não converter falha em score neutro ou resultado de sucesso.

Os limites numéricos concretos e metas de desempenho são decisões da
implementação, a serem definidas após o volume esperado de regras e itens ser
conhecido.

## 15. Testes de aceitação do modelo

1. Duas execuções com as mesmas versões e entradas produzem os mesmos scores,
   estados e ordem.
2. Fato ausente resulta em `Unknown`, aviso e ausência de efeito, não em falso.
3. `Unknown` nunca dispara `Exclude`.
4. Uma exclusão rígida mantém visível o motivo e não desaparece do relatório.
5. Regras não correspondentes não pontuam e não geram explicação de benefício.
6. Um candidato sem regras pontuáveis recebe estado não avaliado.
7. Um conflito não escolhe automaticamente qual alternativa manter.
8. Dependências transitivas são apresentadas com caminho e ciclo detectado.
9. Um item novo do mesmo tipo pode ser avaliado após inclusão de JSON válido,
   sem recompilar.
10. Uma regra inválida ou com operador desconhecido é reportada e não executada.

## Subconjunto executável atual

A avaliação determinística da Fase 2 usa árvores `allOf`, `anyOf` e `not`,
alvos por tipo/ID/tag, fatos e operadores validados por tipo no registro do
motor e efeitos `Score`, `Exclude` e `Flag`. `appliesWhen` consulta somente
fatos do Projeto. O catálogo não aceita caminhos ou operadores desconhecidos,
valores incompatíveis, árvores acima dos limites nem uma exclusão sem
`hardConstraint: true`.

Os limites operacionais desta versão são 200 itens JSON no catálogo, 256 KiB
por arquivo, 16 níveis e 64 nós por árvore de condição, 16 efeitos e até 200
IDs/tags/valores por regra.

Fatos consumidos incluem tamanho de equipe, criticidade, domínio, restrições,
contagens de requisitos confirmados e atributos tipados do candidato. Fatos
opcionais ausentes continuam `Unknown`; contagens derivadas de coleções
conhecidas são fatos confirmados. Condição ou aplicabilidade `Unknown` nunca
aplica efeitos. Condições conhecidas falsas contribuem zero ao numerador e
entram no denominador do score e na cobertura; aplicabilidade falsa fica fora
da cobertura, enquanto aplicabilidade desconhecida reduz a cobertura sem
contribuir para o score.

Arquiteturas, patterns e tecnologias são agrupados separadamente e ordenados
por elegibilidade (elegíveis, não avaliados, excluídos), score conhecido
decrescente, cobertura decrescente e ID ordinal; valores sem evidência ficam
depois dos scores conhecidos dentro de cada estado. A fase não classifica um
tipo contra outro. O score e a cobertura seguem as fórmulas acima e não
constituem probabilidade nem escolha humana. Relações do catálogo e
interpretação de requisitos não fazem parte deste recorte.

Cada análise é salva uma única vez em
`analyses/<projectId>/<analysisId>.json`, validada por
[`analysis-snapshot.schema.json`](../schemas/analysis-snapshot.schema.json).
O snapshot captura revisão e hash do Projeto, hash do catálogo, hash do
conjunto de regras e versão do avaliador. A consulta de histórico compara
esses valores com os atuais e marca o resultado como desatualizado, sem
modificar ou remover o arquivo. A tela identifica quais fingerprints ou
revisões diferem. Snapshots novos usam schema `2.0`; snapshots `1.0` anteriores
continuam aceitos no histórico, sem afirmar estado de aplicação de efeitos que
não era registrado nesse formato.
