# Architectural Knowledge History

## 1. Objetivo e limites

O Architectural Knowledge History mantém projetos anteriores como arquivos
locais consultáveis para comparação e aprendizado organizacional. Histórico é
evidência contextual; não é fonte de verdade de uma decisão nova, modelo de
sucesso ou mecanismo para selecionar arquitetura por popularidade.

Escopo inicial: uso local por um usuário, sem banco, sem compartilhamento
automático e sem serviço externo. O usuário escolhe explicitamente arquivar
uma revisão do projeto e pode remover o registro e seus arquivos. O histórico
não ingere diretórios arbitrários nem observa projetos automaticamente.

Não utilizar IA em runtime, modelos generativos, OpenAI ou Azure OpenAI. Busca
e comparação usam metadados estruturados e operações determinísticas
versionadas; consulta textual, se incluída, é literal e limitada aos campos
indexados.

## 2. Modelo e armazenamento JSON

O armazenamento mantém:

1. **Snapshot imutável do projeto:** cópia JSON da revisão arquivada, com
   fingerprint/conteúdo e versões de schema/catalog/regras conhecidas. O
   arquivo original do Projeto não é alterado.
2. **Registro de histórico:** metadados de arquivamento, nome seguro do arquivo
   snapshot, fingerprint, perfil de atributos comparáveis e referências às
   decisões. Schema: [historical-project.schema.json](../schemas/historical-project.schema.json).
3. **Índice derivado:** lista JSON de registros arquivados e checksums para
   descoberta. É reconstruível a partir dos registros válidos e nunca é fonte
   exclusiva; sua atualização deve ser segura e diagnóstico de corrupção
   explícito. Schema:
   [history-index.schema.json](../schemas/history-index.schema.json).
4. **Política de similaridade versionada:** dimensões, pesos, comparadores e
   cobertura mínima suportados. Schema:
   [history-similarity-policy.schema.json](../schemas/history-similarity-policy.schema.json).

Estrutura lógica sugerida:

```text
<workspace>/
  projects/<project-id>.json
  history/
    index.json
    records/<archive-id>.json
    snapshots/<archive-id>.json
  policies/
    history-similarity/<policy-id>.json
```

O formato do Projeto e seu esquema definitivo são especificados na etapa 12;
este modelo referencia snapshot/fingerprint e não substitui esse contrato.
Identificadores usados em nomes de arquivo são gerados/validados pela
Application; Infrastructure restringe resolução de paths ao diretório
configurado. Não aceitar path arbitrário contido nos JSON.

Arquivar é uma operação explícita: valida projeto e vínculos, cria snapshot
imutável e registro, e atualiza índice com estratégia de escrita segura. A
falha de qualquer etapa é reportada; índice não deve listar snapshot ausente
como sucesso. Verificação de integridade pode reconstruir índice, mas não
reparar silenciosamente o snapshot original.

## 3. Entrada e privacidade

- Antes de arquivar, mostrar escopo do snapshot e permitir revisar conteúdo
  sensível e metadados. O usuário confirma inclusão na coleção histórica.
- O sistema não envia projetos, requisitos, decisões, nomes ou estatísticas
  para fora do workspace.
- Logs não devem registrar conteúdo completo de projetos, requisitos ou ADRs.
- Exclusão remove registro e snapshot correspondentes após confirmação e
  atualiza índice; falha parcial fica explícita e pode ser reparada.
- Exportar/importar histórico usa arquivos JSON versionados e valida todos os
  registros e hashes. Não executar texto, scripts ou templates incluídos.
- Retenção, criptografia e acesso multiusuário são requisitos não definidos;
  não afirmar segurança contra acesso ao sistema operacional ou backup local.

## 4. Comparação de projetos

Comparar snapshots compatíveis em atributos estruturados configurados na
política. Para cada dimensão, mostrar os valores conhecidos em ambos os
projetos, método, peso, contribuição e eventual unidade/cenário. Requisitos,
versões, decisões e itens selecionados podem ser inspecionados lado a lado,
mas valores qualitativos só se comparam quando existe método explícito.

Projetos com schema incompatível, valor inválido, unidade incompatível ou
contexto de comparação diferente são sinalizados/omitidos nas dimensões
afetadas; nunca coerção ou conversão implícita. Provisório, desconhecido e
não aplicável não equivalem a zero ou falso.

## 5. Busca e similaridade determinísticas

A política JSON define dimensões conhecidas pelo contrato e um dos comparadores
suportados:

- `Exact`: coincidência de valor canônico em enum/string/boolean compatível.
- `NumericLinear`: proximidade normalizada entre valores numéricos na mesma
  unidade e cenário; limites de normalização são explícitos na política.
- `SetJaccard`: similaridade entre conjuntos não vazios com IDs normalizados.

Não aceitar expressões, código, scripts, pesos dinâmicos ou fórmulas
arbitrárias no JSON. Método novo exige atualização versionada do avaliador.
Pesos são positivos e versionados; não são probabilidade nem medição de
qualidade arquitetural.

No perfil local 1.0, as dimensões elegíveis são domínio de negócio, tamanho da
equipe, criticidade, meta de escala, IDs de alternativas aceitas e tipos de
alternativas aceitas. Nome e texto de propósito não são dimensões de
similaridade. Uma dimensão só pode aparecer uma vez por política e seu
comparador deve corresponder ao tipo de valor conhecido pelo perfil. O loader
rejeita caminhos ou comparadores não suportados, em vez de aceitar dimensões
que nunca poderiam pontuar.

Para um projeto de consulta `q` e um projeto histórico `h`:

```text
comparableWeight = soma dos pesos das dimensões com valores Confirmed
                   válidos nos dois projetos
similarity = soma(peso * similaridadeDaDimensão) / comparableWeight
coverage = comparableWeight / soma dos pesos configurados na política
```

Se `comparableWeight` for zero, similaridade é `NotComparable` (sem número).
Resultados abaixo da cobertura mínima configurada são `InsufficientCoverage`,
não itens confiáveis. Mostrar score e cobertura separados, a política/versão,
dimensões usadas e dimensões excluídas com motivo. A mesma entrada e política
geram a mesma ordem, com desempate por identificador estável.

Busca de projetos similares não equivale a busca semântica de texto. Sem
política válida, os usuários ainda podem filtrar/abrir projetos por metadados
exatos, mas não mostrar ranking de similaridade.

## 6. Reutilização de decisões

1. Exibir decisão histórica com título, estado no snapshot, projeto de origem,
   revisão, data disponível, análise/fonte relacionada e itens/versões
   selecionados.
2. Explicar dimensões e valores comparáveis que motivaram a proximidade.
3. Permitir abrir o snapshot e comparar contexto, requisitos, alternativas,
   trade-offs e versão do conhecimento.
4. Reutilizar somente após ação explícita de copiar/referenciar. A cópia no
   projeto atual é uma nova proposta com proveniência para projeto/decisão
   histórica; não transfere autoria, confirmação, resultado de análise nem
   status.
5. A nova proposta precisa de revisão e confirmação local. Nenhuma decisão é
   aplicada nem requisito inferido automaticamente.

Um projeto similar pode ter escolhido alternativa diferente ou ter resultado
insatisfatório; similaridade não implica recomendação. Não há retroalimentação
do motor de regras pelo histórico no escopo inicial. Se houver futura
integração, deverá ser opt-in, versionada, explicável e não pode mudar score
sem regra de domínio explícita.

## 7. Estatísticas

Estatísticas são descritivas e devem exibir população, janela/filtros,
denominador e dados ausentes. Exemplos permitidos: quantidade de projetos
arquivados por período/domínio declarado; distribuição das escolhas
registradas; quantidade de decisões sem resultado de acompanhamento.

Não inferir sucesso ou qualidade a partir da frequência de uma arquitetura,
pattern ou tecnologia. Métrica de sucesso só pode ser exibida se medida pelo
usuário, com definição, fonte, período e amostra conhecidos. Amostras pequenas
devem exibir contagem e não ser generalizadas. Nenhuma estatística altera
recomendação ou score.

## 8. Critérios de aceite e testes futuros

1. Arquivar exige confirmação explícita, snapshot/revisão íntegros e
   proveniência do projeto.
2. Snapshot histórico é imutável; mudanças no Projeto atual não o reescrevem.
3. JSON/schema/hash inválido é reportado e não participa de comparação como se
   válido fosse.
4. Arquivos e paths são limitados ao workspace configurado; dados não
   determinam caminhos arbitrários.
5. `Unknown`, `NotApplicable`, provisório, unidade ou cenário incompatível não
   pontuam como igualdade ou zero.
6. Similaridade registra dimensões, pesos, método, versão, cobertura e
   desempate determinístico; sem base comparável retorna `NotComparable`.
7. Threshold de cobertura insuficiente é visível e impede apresentação como
   comparação suficiente.
8. Reutilização gera proposta rastreada e requer confirmação; não altera
   projeto por semelhança.
9. Frequência histórica não vira score, probabilidade ou alegação de sucesso.
10. Falha de gravação/exclusão não mostra sucesso e deixa estado recuperável e
    diagnóstico explícito.
11. Busca/consulta é local, sem IA ou serviço externo, e pode ser usada sem
    ativar similaridade quando a política não estiver configurada.

## 9. Trade-offs e alternativas

| Escolha | Benefício | Custo / risco | Alternativa |
|---|---|---|---|
| Arquivar snapshot JSON com índice reconstruível | Portabilidade, auditoria e sem dependência de banco | Índice/arquivo exigem consistência e tratamento de concorrência | Banco central facilita consultas, mas viola restrição inicial |
| Similaridade por atributos estruturados | Determinística e explicável | Depende de atributos comparáveis curados e boa cobertura | Busca vetorial/embeddings não cabe nas restrições e é menos auditável |
| Histórico não influencia análise automaticamente | Evita popularidade e viés de sobrevivência | Usuário precisa acionar comparação/reuso | Ranking por frequência é simples, mas confunde adoção com qualidade |
| Arquivamento opt-in | Controle local e revisão de conteúdo sensível | Acrescenta confirmação/curadoria | Arquivar automaticamente arrisca reter dados inesperados |

## 10. Pendências

- Fechar schema do Projeto, política de retenção e localização do workspace na
  etapa 12.
- Curar dimensões e pesos com dados reais antes de publicar política padrão;
  não fixar pesos ou limites só para produzir um número.
- Definir como usuários informam resultado observado/sucesso, se esse recurso
  for aprovado.
- Definir acesso, consentimento e governança antes de qualquer coleção
  compartilhada.

## 11. Estado da implementação local

O fluxo local arquiva a revisão validada do Projeto após confirmação, grava
snapshot e registro JSON com fingerprint SHA-256 e reconstrói o índice a partir
de registros/snapshots verificados. Busca literal por nome/domínio/ID funciona
sem política de similaridade. Comparação usa somente a política JSON colocada
em `<workspace>/policies/history-similarity/`; nenhuma política ou peso padrão
é fornecido. O usuário pode abrir o snapshot antes de copiar uma decisão como
proposta com proveniência. As estatísticas exibem população e denominadores e
nunca alimentam a análise. Exemplos JSON locais para curadoria podem ser
colocados em [`data/history-curation`](../data/history-curation/README.md);
eles não são carregados pelo runtime nem publicados pelo Git. As políticas
carregadas são validadas semanticamente contra o perfil suportado, incluindo
comparador compatível, caminho único e versão de cada dimensão.

A interface também exibe um relatório agregado da base verificada: quantidade
de snapshots e pares, presença de valores confirmados, pares comparáveis,
valores distintos e faixa observada para dimensões numéricas. O relatório não
mostra valores de projetos, não define cobertura mínima e não gera pesos; seu
objetivo é dar evidência local para uma curadoria posterior sem exportar dados.
Os pares comparáveis respeitam versões do schema/perfil, unidade e cenário; a
faixa numérica é apenas observada e não é usada como limite de normalização.
