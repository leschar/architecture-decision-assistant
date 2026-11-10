# Questionário Arquitetural

## 1. Objetivo

O questionário ajuda a descobrir requisitos arquiteturais, restrições e
incertezas do projeto. Ele não determina sozinho uma “melhor arquitetura” e
não substitui conversas com stakeholders, análise de domínio, protótipos ou
decisões do arquiteto.

O resultado de uma resposta é um fato estruturado do projeto, acompanhado de
origem e estado de confirmação. O motor de regras avalia esses fatos depois;
as perguntas não atribuem diretamente pontos às arquiteturas.

## 2. Princípios de desenho

1. **Adaptativo, não uma entrevista obrigatória de 150 itens.** O catálogo
   contém 150 perguntas para descoberta e aprofundamento. Uma sessão normal
   começa com um conjunto essencial curto e apresenta perguntas condicionais
   conforme as respostas.
2. **Começar pelo negócio e atributos de qualidade.** Não perguntar primeiro
   “qual banco”, “qual cloud” ou “qual broker”; perguntar sobre necessidade,
   restrição, volume, latência, disponibilidade, recuperação e operação.
3. **Perguntar uma coisa por vez.** Evitar perguntas que combinem escala,
   disponibilidade e volume numa única resposta.
4. **Opções neutras e unidades explícitas.** Respostas numéricas incluem unidade
   e horizonte (por exemplo, pedidos por segundo no pico); termos como “alto”
   devem ter descrição e exemplos.
5. **“Não sei” é válido.** Não tratado como “não”, zero ou requisito satisfeito.
   Pode gerar uma pergunta de descoberta, um aviso ou uma lacuna a validar.
6. **Separar fato, estimativa e meta.** Registrar se o dado veio de medição,
   previsão, compromisso de negócio ou estimativa.
7. **Evitar falso rigor.** O peso da pergunta indica sua importância para a
   análise, não probabilidade de sucesso nem pontuação da alternativa.
8. **Rastreabilidade.** Resposta, requisito derivado, regra influenciada e
   recomendação afetada devem poder ser inspecionados.
9. **Não recomendar fornecedor por resposta isolada.** Cloud, linguagem,
   banco ou mensageria são consequências de requisitos e restrições, não
   perguntas enviesadas de preferência.
10. **Permitir revisão.** Usuário pode alterar respostas, indicar origem,
    marcar incerteza e confirmar requisitos derivados antes da análise.

## 3. Modelo de pergunta

Uma pergunta versionada deve conter ao menos:

| Campo | Descrição |
|---|---|
| `id` | Identificador estável como `sca-01`, independente do texto. |
| `category` | Categoria controlada do questionário. |
| `prompt` | Texto curto, neutro e sem jargão desnecessário. |
| `objectiveAndImpact` | O que se pretende descobrir e quais decisões ou atributos podem ser influenciados. |
| `affectedAreas` | Famílias de conhecimento potencialmente relacionadas, não recomendações. |
| `weight` | Importância de 1 a 5 para priorização da elicitação e transparência. |
| `responseType` | `boolean`, `singleChoice`, `multiChoice`, `text` ou `structured`; números, datas e medições estruturadas precisam declarar unidade/formato. |
| `factPath` | Caminho estável onde a resposta é exposta ao motor, por exemplo `project.questionnaire.answers.avl-01`. |
| `phase` | `core` ou `conditional`; define a etapa sugerida, não a obrigatoriedade nem a condição de exibição. |
| `showWhen` | Condição declarativa opcional sobre fatos já respondidos; sem código executável. |
| `version` | Versão editorial da pergunta. |

Cada resposta persistida deve incluir: valor tipado, unidade se aplicável,
origem (`Measured`, `Committed`, `Estimated`, `UserProvided`), estado
(`Confirmed`, `Provisional`, `Unknown`, `NotApplicable`, `Declined`), data e
referência à pergunta/versão. Respostas `Confirmed` e `Provisional` exigem
valor; `Unknown`, `NotApplicable` e `Declined` não recebem valor fictício.
Não persistir apenas o rótulo visual da opção.

### Peso

| Peso | Interpretação |
|---:|---|
| 1 | Contextual; raramente altera uma decisão por si só. |
| 2 | Relevante para refinamento ou uma decisão específica. |
| 3 | Influencia escolhas arquiteturais comuns. |
| 4 | Pode restringir significativamente alternativas ou operação. |
| 5 | Requisito crítico, obrigação ou restrição que deve ser validada explicitamente. |

O peso **não multiplica o score da arquitetura**. O motor usa regras declarativas
com seus próprios pesos e evidências. Essa separação evita que uma resposta
seja contabilizada duas vezes (na pergunta e na regra). Pesos podem priorizar
perguntas em sessões curtas; não implicam que resposta “sim” seja positiva.

## 4. Respostas, fatos e ramificação

- Questões categóricas usam opções com códigos estáveis e descrições.
- Questões numéricas validam unidade, limites e período de referência.
- Questões booleanas distinguem `false` de `Unknown` e `NotApplicable`.
- Questões multi-seleção não podem ser convertidas em uma escolha única sem
  regra explícita.
- Uma resposta só alimenta regras depois de confirmada ou explicitamente
  marcada como provisória; regras podem tratar provisórias e confirmadas de
  forma distinta.
- `showWhen` usa os mesmos operadores limitados e a lógica de três valores do
  motor: fato desconhecido não dispara ramificação como se fosse falso.
- Dependências entre perguntas formam um DAG; ciclos de visibilidade são erro
  de validação.
- Perguntas marcadas como `conditional` sem `showWhen` explícito só devem ser
  exibidas após o usuário escolher aprofundar sua categoria ou assunto; não
  devem aparecer automaticamente como se houvesse uma condição implícita.
- “Prefiro não responder” deve permanecer distinto de “não sei” e de “não se
  aplica”.
- Não derivar requisito definitivo de texto livre ou de uma resposta ambígua
  sem revisão do usuário.

### Perfis de resposta sugeridos

- **Sim/não/desconhecido:** `true`, `false`, `unknown`, opcionalmente
  `notApplicable`.
- **Maturidade/capacidade:** `Low`, `Medium`, `High`, `Unknown`, com descrições
  que devem ser calibradas para a organização.
- **Prioridade:** `Must`, `Should`, `Could`, `Unknown`; não confundir com
  severidade ou probabilidade.
- **Volume/latência/tempo:** número, unidade, cenário (médio/pico) e janela.
- **Opção única/múltipla:** enumerações próprias à pergunta, sem usar
  identificadores de fornecedor quando a decisão ainda não exige fornecedor.
- **Texto curto:** contexto adicional; texto não é processado pelo motor como
  expressão ou código.

## 5. Fluxo recomendado

1. Identificar tipo de sistema, objetivo de negócio, usuários, restrições e
   estágio do produto.
2. Capturar atributos de qualidade críticos e requisitos mensuráveis.
3. Perguntar sobre cenários de pico, falha, recuperação, segurança e operação
   relevantes aos atributos escolhidos.
4. Capturar capacidade da equipe e restrições de implantação/organização.
5. Exibir resumo de fatos confirmados, estimativas e lacunas.
6. Deixar o usuário editar e confirmar os requisitos derivados.
7. Só então executar a análise e explicar quais respostas influenciaram quais
   regras.

### Conjunto inicial sugerido

Para a primeira experiência, testar cerca de 20 a 30 perguntas essenciais
selecionadas por cobertura, e não por quota fixa. Perguntas de detalhe ficam
condicionais. A seleção exata deve ser validada com arquitetos e Product Owners;
as 26 perguntas marcadas `core` no catálogo são uma hipótese inicial de teste;
as demais são um banco de aprofundamento, não um formulário obrigatório.

## 6. Banco de perguntas

As 150 perguntas abaixo são um catálogo inicial para revisão. `Área afetada`
indica itens que podem ser influenciados, não uma recomendação de tecnologia.
`Resposta` descreve o formato de dado esperado. O texto e os pesos são
hipóteses iniciais a validar com usuários do produto.

O catálogo JSON versionado para consumo pela aplicação é
[architectural-questionnaire.json](../schemas/architectural-questionnaire.json);
seu contrato está em
[architectural-questionnaire.schema.json](../schemas/architectural-questionnaire.schema.json).
As respostas persistidas usam
[questionnaire-response.schema.json](../schemas/questionnaire-response.schema.json),
separado do catálogo imutável de perguntas.
As tabelas a seguir são a representação legível para revisão de produto.

### Negócio — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| biz-01 | Qual problema de negócio o sistema deve resolver? | Delimitar domínio e evitar arquitetura sem contexto. | Arquitetura, princípios, patterns | 4 | Texto curto |
| biz-02 | Qual é o resultado mensurável esperado com o sistema? | Identificar métricas de sucesso e requisitos verificáveis. | Arquitetura, observabilidade | 3 | Texto + métrica |
| biz-03 | O sistema é novo, uma substituição ou uma evolução? | Entender legado, migração e coexistência. | Arquitetura, integração, dados | 4 | Opção única |
| biz-04 | Qual é o prazo para a primeira versão útil? | Tornar explícito o trade-off prazo versus escopo e operação. | Arquitetura, plataforma | 3 | Data/período |
| biz-05 | Quais processos de negócio são críticos para a operação? | Priorizar disponibilidade, consistência e recuperação. | Arquitetura, dados, padrões | 5 | Lista |
| biz-06 | Quais processos podem ser executados de forma assíncrona? | Identificar tolerância a processamento eventual e filas. | Patterns, mensageria | 4 | Lista/Unknown |
| biz-07 | Há períodos sazonais ou eventos que alteram a demanda? | Capturar picos previsíveis e planejamento de escala. | Arquitetura, infraestrutura | 3 | Calendário/estimativa |
| biz-08 | Qual é o custo de uma hora de indisponibilidade? | Quantificar criticidade e orientar resiliência. | Disponibilidade, operação | 5 | Valor/faixa |
| biz-09 | Existem metas de receita, conversão ou redução de custo ligadas ao sistema? | Relacionar decisões a impactos e custo de operação. | Arquitetura, cloud | 3 | Métrica/faixa |
| biz-10 | Quais restrições legais ou contratuais de negócio se aplicam? | Identificar obrigações não negociáveis. | Segurança, dados, governança | 5 | Lista/Unknown |
| biz-11 | Quais capacidades de negócio devem permanecer independentes? | Apoiar identificação de limites de domínio e autonomia. | Arquitetura, DDD | 4 | Lista |
| biz-12 | Como o sucesso do sistema será avaliado após a implantação? | Definir medidas operacionais e critérios de aceitação. | Observabilidade, governança | 3 | Texto/métricas |

### Usuários — 10 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| usr-01 | Quem são os principais perfis de usuário? | Identificar jornadas, permissões e canais. | Arquitetura, identidade | 3 | Lista |
| usr-02 | Quantos usuários ativos são esperados no primeiro ano? | Estimar carga e capacidade inicial. | Escalabilidade, infraestrutura | 3 | Número/período |
| usr-03 | Quantos usuários simultâneos são esperados em condições normais e de pico? | Diferenciar base cadastrada de concorrência. | Escalabilidade, performance | 4 | Dois números |
| usr-04 | De quais regiões geográficas os usuários acessarão o sistema? | Avaliar latência, residência de dados e distribuição. | Cloud, rede, dados | 4 | Regiões/Unknown |
| usr-05 | O sistema precisa funcionar em dispositivos móveis ou conexões instáveis? | Identificar requisitos de cliente, sincronização e tolerância à rede. | Arquitetura, cache, sincronização | 3 | Opção + contexto |
| usr-06 | Há requisitos de acessibilidade ou tecnologias assistivas? | Incluir acessibilidade nos critérios de interface e conformidade. | UI, governança | 3 | Requisito/Unknown |
| usr-07 | Quais idiomas, fusos ou formatos regionais são necessários? | Avaliar internacionalização e localização. | Arquitetura, dados, UI | 2 | Lista |
| usr-08 | O acesso é público, autenticado ou restrito a organizações? | Delimitar identidade, autorização e isolamento. | Segurança, identidade | 4 | Opção única |
| usr-09 | Diferentes clientes precisam de isolamento lógico de dados? | Identificar multitenancy e fronteiras de autorização. | Dados, segurança, arquitetura | 5 | Sim/não/Unknown |
| usr-10 | Quais jornadas de usuário não podem ser interrompidas por falhas parciais? | Priorizar degradação graciosa e fluxos críticos. | Disponibilidade, UI, arquitetura | 4 | Lista |

### Escalabilidade — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| sca-01 | Qual volume de requisições é esperado em média e no pico? | Dimensionar throughput e cenários de carga. | Arquitetura, runtime, rede | 5 | Número/unidade/janela |
| sca-02 | Qual crescimento de usuários, tráfego ou dados é esperado em 1, 3 e 5 anos? | Planejar evolução sem superdimensionar o MVP. | Arquitetura, dados, cloud | 4 | Estimativas |
| sca-03 | Os picos de carga são previsíveis, sazonais ou imprevisíveis? | Avaliar elasticidade e capacidade reservada. | Cloud, escalabilidade | 4 | Opção única |
| sca-04 | Quais operações concentram a maior carga? | Encontrar hotspots funcionais e de dados. | Arquitetura, performance | 4 | Lista/Unknown |
| sca-05 | Há componentes que precisam escalar independentemente? | Avaliar particionamento, modularidade ou serviços independentes. | Arquitetura, plataforma | 4 | Lista/Unknown |
| sca-06 | O sistema precisa aumentar capacidade automaticamente? | Identificar autoscaling e sinais de escala. | Cloud, infraestrutura | 3 | Sim/não/Unknown |
| sca-07 | Qual tempo máximo aceitável para absorver um pico repentino? | Avaliar filas, aquecimento e elasticidade. | Escalabilidade, mensageria | 3 | Tempo |
| sca-08 | Há limite máximo de custo mesmo durante picos? | Explicitar trade-off entre capacidade, atraso e custo. | Cloud, governança | 4 | Valor/faixa |
| sca-09 | Existe processamento em lote? Qual volume e janela? | Dimensionar jobs, filas e isolamento de carga. | Dados, compute, mensageria | 3 | Volume + duração |
| sca-10 | O tráfego de leitura é muito diferente do tráfego de escrita? | Avaliar modelos de leitura, cache ou separação de carga. | Dados, CQRS, cache | 3 | Estimativa comparativa |
| sca-11 | O sistema precisa particionar dados ou carga por cliente, região ou domínio? | Identificar sharding, tenancy e limites de dados. | Dados, arquitetura | 4 | Opção + contexto |
| sca-12 | Qual é o maior tamanho esperado de arquivo, mensagem ou transação? | Evitar limites de payload e bloqueios de processamento. | Integração, armazenamento | 3 | Número/unidade |

### Disponibilidade e recuperação — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| avl-01 | Qual disponibilidade mensal é exigida para o sistema? | Estabelecer SLO e orçamento de erro. | Arquitetura, operação, cloud | 5 | Percentual/Unknown |
| avl-02 | Quais períodos de indisponibilidade são aceitáveis para manutenção? | Planejar deploy, manutenção e janelas operacionais. | Implantação, operação | 4 | Janela/Unknown |
| avl-03 | Qual é o RTO para cada processo crítico? | Definir tempo-alvo de recuperação. | Recuperação, infraestrutura | 5 | Tempo por processo |
| avl-04 | Qual é o RPO para cada conjunto de dados crítico? | Definir perda de dados aceitável e estratégia de backup/replicação. | Dados, recuperação | 5 | Tempo por conjunto |
| avl-05 | O sistema deve continuar operando quando uma dependência externa falhar? | Definir degradação graciosa e isolamento de falha. | Resiliência, integração | 4 | Cenário por dependência |
| avl-06 | Quais funções podem ser temporariamente desabilitadas durante uma falha? | Definir modos degradados e prioridade de capacidades. | Arquitetura, operação | 4 | Lista |
| avl-07 | É necessário operar em mais de uma zona de disponibilidade? | Entender tolerância a falhas de infraestrutura. | Cloud, deployment | 4 | Sim/não/Unknown |
| avl-08 | É necessário operar em mais de uma região? | Avaliar recuperação regional, consistência e custo. | Cloud, dados, rede | 4 | Sim/não/condições |
| avl-09 | Como será verificado que backups podem ser restaurados? | Incluir testes de recuperação, não apenas cópia de dados. | Dados, governança | 5 | Processo/frequência |
| avl-10 | Qual frequência máxima de falhas ou perda de serviço é aceitável? | Refinar objetivos de confiabilidade por jornada. | Operação, arquitetura | 4 | Taxa/Unknown |
| avl-11 | Há requisito de continuidade de negócio ou plano de desastre? | Descobrir processos, responsáveis e cenários de contingência. | Governança, recuperação | 4 | Sim/não/documento |
| avl-12 | Quais operações precisam ser idempotentes ou repetíveis após falha? | Reduzir duplicidades e inconsistência em retentativas. | Dados, integração, patterns | 4 | Lista/Unknown |

### Observabilidade — 10 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| obs-01 | Quais indicadores mostram que o sistema está saudável para o negócio? | Definir métricas técnicas ligadas a resultados do usuário. | Observabilidade, negócio | 4 | Lista |
| obs-02 | Quais jornadas precisam ser rastreadas ponta a ponta? | Identificar correlação de chamadas e processos assíncronos. | Tracing, integração | 4 | Lista |
| obs-03 | Há requisitos de retenção para logs, métricas e traces? | Avaliar custo, privacidade e diagnóstico. | Observabilidade, dados | 3 | Período por sinal |
| obs-04 | Quais eventos de segurança devem ser auditados? | Definir trilha de auditoria e requisitos de investigação. | Segurança, observabilidade | 5 | Lista |
| obs-05 | Quem será responsável por responder a alertas fora do horário comercial? | Planejar operação, escalonamento e SLOs. | Equipe, operação | 4 | Papel/processo |
| obs-06 | Quais limites de alerta evitam ruído sem ocultar incidentes? | Evitar alertas não acionáveis e fadiga operacional. | Operação, monitoramento | 3 | Processo/Unknown |
| obs-07 | É necessário correlacionar logs entre componentes ou serviços? | Determinar necessidade de IDs de correlação. | Logging, arquitetura | 3 | Sim/não |
| obs-08 | Quais indicadores de negócio precisam ser acompanhados em tempo real? | Integrar telemetria de produto e operação. | Observabilidade, dados | 3 | Lista |
| obs-09 | Há restrições sobre dados pessoais em logs e telemetria? | Evitar vazamento e definir mascaramento/minimização. | Segurança, privacidade | 5 | Lista/Unknown |
| obs-10 | Qual prazo máximo para detectar uma falha crítica? | Definir objetivos de detecção e monitoramento. | Operação, disponibilidade | 4 | Tempo |

### Equipe e capacidade operacional — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| tem-01 | Quantas pessoas desenvolverão o sistema inicialmente? | Avaliar carga cognitiva e custo de coordenação. | Arquitetura, organização | 3 | Número |
| tem-02 | Quais competências técnicas já existem na equipe? | Evitar arquitetura incompatível com capacidade disponível. | Tecnologia, arquitetura | 4 | Lista |
| tem-03 | Há equipe dedicada à operação ou plataforma? | Avaliar capacidade de operar componentes distribuídos. | Operação, cloud | 5 | Sim/não/parcial |
| tem-04 | Quantas equipes precisam entregar mudanças de forma independente? | Identificar necessidade real de autonomia e limites de serviço. | Arquitetura, governança | 4 | Número/contexto |
| tem-05 | Qual experiência existe com sistemas distribuídos em produção? | Avaliar risco operacional de distribuição. | Arquitetura, observabilidade | 4 | Maturidade |
| tem-06 | Quem será responsável por manter cada componente após o lançamento? | Estabelecer ownership e custo total. | Operação, governança | 4 | Papéis |
| tem-07 | Qual é a capacidade de suporte e resposta a incidentes? | Relacionar disponibilidade a cobertura operacional. | Disponibilidade, equipe | 5 | Nível/horário |
| tem-08 | A equipe consegue manter mais de uma tecnologia de dados ou mensageria? | Evitar fragmentação excessiva do stack. | Dados, tecnologia | 4 | Sim/não/limites |
| tem-09 | Quais práticas de revisão, testes e entrega contínua já são adotadas? | Avaliar prontidão para automação e frequência de deploy. | CI/CD, qualidade | 3 | Lista/maturidade |
| tem-10 | Há restrições de contratação ou dependência de especialistas? | Considerar disponibilidade de competências e risco de fornecedor. | Tecnologia, governança | 3 | Lista |
| tem-11 | Qual frequência de implantação a equipe pretende alcançar? | Influenciar estratégia de release, modularidade e automação. | Deployment, arquitetura | 3 | Frequência |
| tem-12 | Qual complexidade operacional a equipe está disposta a assumir? | Tornar explícito o limite de operação aceitável. | Arquitetura, tecnologia | 4 | Baixa/média/alta |

### Infraestrutura e operação — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| inf-01 | Quais ambientes são necessários para desenvolver, testar e operar? | Planejar separação e promoção de artefatos. | Deployment, governança | 3 | Lista |
| inf-02 | Existem padrões corporativos obrigatórios de runtime ou plataforma? | Capturar restrições técnicas existentes. | Tecnologia, infraestrutura | 4 | Lista |
| inf-03 | Quais limites de CPU, memória ou armazenamento se aplicam? | Identificar restrições de capacidade. | Runtime, infraestrutura | 4 | Valores/unidades |
| inf-04 | Há dependência de hardware, rede ou instalação local específica? | Avaliar portabilidade e deployment. | Infraestrutura, arquitetura | 4 | Descrição |
| inf-05 | Como os segredos e certificados serão provisionados e rotacionados? | Definir integração com gestão de segredos. | Segurança, plataforma | 5 | Processo/sistema existente |
| inf-06 | Quais mecanismos de deployment e rollback estão disponíveis? | Avaliar estratégias de release e recuperação. | CI/CD, operação | 4 | Descrição |
| inf-07 | É necessário executar componentes em ambientes desconectados? | Identificar restrições de conectividade e operação offline. | Infraestrutura, sincronização | 4 | Sim/não/contexto |
| inf-08 | Há restrições de sistema operacional ou arquitetura de CPU? | Evitar escolhas incompatíveis com o ambiente alvo. | Runtime, tecnologia | 3 | Lista |
| inf-09 | Qual nível de automação de infraestrutura é permitido ou exigido? | Definir provisionamento repetível e controle de mudanças. | Infraestrutura, governança | 3 | Nível |
| inf-10 | Como serão coletadas e aplicadas atualizações de segurança? | Planejar patching de runtime, imagens e dependências. | Segurança, deployment | 4 | Processo |
| inf-11 | Quais dependências de rede e DNS podem limitar comunicação entre componentes? | Identificar latência, segmentação e falhas de rede. | Rede, arquitetura | 3 | Restrições |
| inf-12 | Qual orçamento mensal de infraestrutura está aprovado? | Delimitar escolhas e trade-offs de operação. | Cloud, infraestrutura | 4 | Valor/faixa |

### Segurança e privacidade — 14 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| sec-01 | Quais tipos de dados sensíveis serão tratados? | Classificar dados e aplicar controles proporcionais. | Dados, segurança | 5 | Categorias |
| sec-02 | Quais dados pessoais são coletados e para qual finalidade? | Apoiar minimização, privacidade e retenção. | Dados, governança | 5 | Lista/finalidade |
| sec-03 | Existem requisitos legais de localização ou residência de dados? | Restringir regiões e replicação. | Cloud, dados, governança | 5 | Jurisdição/Unknown |
| sec-04 | Como usuários e serviços devem ser autenticados? | Delimitar identidade, federação e credenciais. | Identidade, segurança | 5 | Requisitos existentes |
| sec-05 | Como serão definidas permissões para usuários, administradores e serviços? | Avaliar autorização e segregação de funções. | Segurança, arquitetura | 5 | Papéis/atributos |
| sec-06 | É necessário isolamento forte entre clientes ou organizações? | Identificar limites de tenant e impacto de falha cruzada. | Dados, segurança | 5 | Nível |
| sec-07 | Quais ameaças ou cenários de abuso são prioritários? | Orientar threat modeling e controles. | Segurança, arquitetura | 4 | Lista/Unknown |
| sec-08 | Quais objetivos de criptografia são exigidos em trânsito e em repouso? | Definir controles criptográficos e gestão de chaves. | Segurança, dados | 5 | Requisito |
| sec-09 | Quais dados devem ser mascarados, anonimizados ou removidos? | Definir proteção em logs, ambientes e exportações. | Dados, observabilidade | 5 | Lista |
| sec-10 | Quanto tempo cada categoria de dado pode ser mantida? | Definir retenção, exclusão e arquivamento. | Dados, governança | 4 | Prazo por categoria |
| sec-11 | Como deve ser atendida uma solicitação de exclusão de dados pessoais? | Identificar propagação de exclusão e backups. | Dados, integração | 4 | Processo/prazo |
| sec-12 | Quais trilhas de auditoria são obrigatórias e por quanto tempo? | Definir registros imutáveis e controles de acesso. | Segurança, dados | 4 | Eventos + período |
| sec-13 | Há exigência de segregação entre desenvolvimento, teste e produção? | Impedir exposição ou alteração indevida entre ambientes. | Segurança, deployment | 4 | Nível |
| sec-14 | Qual processo de resposta a vulnerabilidades e incidentes deve ser atendido? | Alinhar arquitetura a detecção, contenção e evidências. | Segurança, operação | 4 | Processo/SLA |

### Integrações — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| int-01 | Quais sistemas externos precisam ser integrados? | Mapear fronteiras, proprietários e dependências. | Integração, arquitetura | 4 | Lista |
| int-02 | Para cada integração, há API síncrona, troca de arquivos ou eventos disponíveis? | Identificar contratos e estilos de comunicação suportados. | API, mensageria | 4 | Por sistema |
| int-03 | Qual volume e tamanho de mensagens são esperados por integração? | Dimensionar payload, throughput e particionamento. | Mensageria, dados | 4 | Número/unidade |
| int-04 | Qual latência máxima cada integração pode adicionar à jornada? | Definir limites de chamadas síncronas. | Performance, rede | 4 | Tempo por fluxo |
| int-05 | O sistema deve continuar operando quando uma integração estiver indisponível? | Definir filas, cache, degradação e retomada. | Resiliência, integração | 5 | Por integração |
| int-06 | Como serão tratadas retentativas e mensagens duplicadas? | Definir idempotência e entrega. | Mensageria, dados | 4 | Política |
| int-07 | É necessário preservar ordem entre mensagens ou eventos? | Identificar escopo de ordenação. | Mensageria, arquitetura | 4 | Por chave/fluxo |
| int-08 | Por quanto tempo mensagens ou eventos precisam ser retidos ou reprocessados? | Diferenciar fila transitória de log de eventos. | Mensageria, dados | 4 | Duração |
| int-09 | Quem controla a evolução dos contratos de integração? | Planejar compatibilidade e versionamento. | Governança, API | 4 | Responsáveis/processo |
| int-10 | Há requisitos de entrega imediata ou o processamento pode ser eventual? | Separar resposta síncrona de conclusão de negócio. | Arquitetura, mensageria | 4 | Por fluxo |
| int-11 | As integrações exigem autenticação mútua, assinatura ou allowlists? | Identificar controles de confiança entre sistemas. | Segurança, rede | 5 | Por integração |
| int-12 | Como falhas e divergências entre sistemas serão reconciliadas? | Definir compensação, reconciliação e suporte operacional. | Dados, patterns | 4 | Processo |

### Dados — 14 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| dat-01 | Quais são as principais entidades e conceitos do domínio? | Descobrir modelo conceitual e limites de consistência. | DDD, dados | 4 | Lista/diagrama |
| dat-02 | Qual é o volume inicial e o crescimento anual de dados? | Planejar armazenamento, retenção e custo. | Dados, infraestrutura | 4 | Tamanho/período |
| dat-03 | Quais operações exigem consistência imediata? | Identificar fronteiras transacionais críticas. | Dados, arquitetura | 5 | Por operação |
| dat-04 | Onde a consistência eventual é aceitável? | Identificar oportunidades e limites de processamento assíncrono. | Dados, mensageria | 4 | Por fluxo |
| dat-05 | Existem consultas analíticas ou relatórios que divergem das consultas transacionais? | Avaliar modelos de leitura e separação de cargas. | Dados, CQRS, analytics | 3 | Cenários |
| dat-06 | Qual retenção é necessária para cada classe de dado? | Planejar arquivamento, exclusão e custos. | Dados, governança | 4 | Prazo por classe |
| dat-07 | Quais dados precisam de backup e com qual granularidade? | Definir estratégia de proteção e restauração. | Dados, disponibilidade | 5 | Por conjunto |
| dat-08 | É necessário histórico completo de alterações ou auditoria de estado? | Avaliar trilha de auditoria e event sourcing sem presumir sua adoção. | Dados, governança | 4 | Necessidade |
| dat-09 | Quais regras de integridade precisam ser garantidas entre entidades? | Definir limites transacionais e validação. | Dados, DDD | 4 | Lista |
| dat-10 | Há necessidade de pesquisa textual, geoespacial ou por similaridade? | Identificar requisitos especializados de consulta. | Dados, busca | 3 | Tipos de consulta |
| dat-11 | Há dados que precisam ser compartilhados entre domínios ou equipes? | Descobrir ownership e contratos de dados. | Arquitetura, integração | 4 | Lista/ownership |
| dat-12 | Como ocorrerá a migração de dados de sistemas existentes? | Dimensionar coexistência, qualidade e corte. | Dados, integração | 4 | Estratégia/volume |
| dat-13 | Há requisitos de exportação, portabilidade ou interoperabilidade de dados? | Evitar formatos fechados e dependência indevida. | Dados, governança | 3 | Formatos/padrões |
| dat-14 | Qual nível de perda ou duplicidade de dados é tolerável em falhas? | Definir idempotência, reconciliação e RPO. | Dados, disponibilidade | 5 | Por dado/fluxo |

### Performance — 12 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| per-01 | Qual é o tempo de resposta esperado para cada jornada crítica? | Definir SLOs de latência por operação. | Runtime, API, dados | 5 | Percentis/tempo |
| per-02 | Quais percentis de latência devem ser atendidos? | Evitar metas baseadas apenas em média. | Observabilidade, runtime | 4 | p50/p95/p99 |
| per-03 | Quais operações são limitadas por CPU, memória, I/O ou rede? | Direcionar testes e desenho de capacidade. | Runtime, infraestrutura | 3 | Medição/estimativa |
| per-04 | Há processamento pesado que pode ocorrer fora da requisição do usuário? | Identificar jobs e feedback assíncrono. | Mensageria, arquitetura | 4 | Por operação |
| per-05 | Qual tamanho máximo de resposta é aceitável para clientes e rede? | Definir paginação, streaming e payload. | API, rede | 3 | Tamanho |
| per-06 | Há requisitos de streaming ou atualização contínua para o usuário? | Identificar conexões persistentes e backpressure. | Rede, runtime | 3 | Cenário |
| per-07 | O sistema precisa responder sob carga degradada ou rejeitar excesso? | Planejar limites, filas e proteção contra overload. | Resiliência, API | 4 | Política |
| per-08 | Quais operações podem usar cache sem comprometer correção? | Entender validade, expiração e invalidação. | Cache, dados | 3 | Por dado |
| per-09 | Qual duração de cálculo ou processamento em lote é aceitável? | Definir janela de trabalho e paralelismo. | Compute, dados | 3 | Tempo/volume |
| per-10 | Há metas distintas para leitura, escrita e busca? | Separar perfis de carga e modelo de consulta. | Dados, performance | 4 | Por operação |
| per-11 | Como serão testadas as metas de performance antes de produção? | Tornar metas verificáveis e reproduzíveis. | Testes, observabilidade | 3 | Método/frequência |
| per-12 | Que degradação de latência é aceitável durante picos? | Explicitar trade-off entre capacidade e custo. | Escalabilidade, cloud | 3 | Limite/Unknown |

### Cloud e implantação — 10 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| cld-01 | Existe provedor ou ambiente de hospedagem obrigatório? | Capturar restrição real sem pressupor fornecedor. | Cloud, infraestrutura | 4 | Opção/Unknown |
| cld-02 | Há exigência de executar on-premises, em cloud pública ou em modelo híbrido? | Definir portabilidade e limites operacionais. | Deployment, arquitetura | 4 | Opção |
| cld-03 | Quais serviços gerenciados são permitidos ou proibidos? | Avaliar responsabilidade operacional e lock-in. | Cloud, governança | 4 | Lista |
| cld-04 | Há necessidade de portabilidade entre provedores ou ambientes? | Definir importância e escopo de portabilidade. | Cloud, arquitetura | 3 | Nível/razão |
| cld-05 | Quais zonas ou regiões podem armazenar e processar dados? | Restringir implantação por latência ou legislação. | Cloud, dados | 5 | Lista/Unknown |
| cld-06 | Há conectividade privada obrigatória com redes corporativas? | Identificar topologia, DNS e segurança de rede. | Cloud, segurança | 4 | Requisito |
| cld-07 | Qual modelo de custo (fixo, variável ou híbrido) é aceitável? | Orientar trade-off de elasticidade e capacidade reservada. | Cloud, governança | 3 | Preferência/limites |
| cld-08 | Existem restrições de licenciamento ou aquisição de serviços? | Evitar opções inviáveis por contrato. | Governança, tecnologia | 4 | Lista |
| cld-09 | Como ambientes são provisionados e removidos? | Definir automação e isolamento. | Infraestrutura, CI/CD | 3 | Processo |
| cld-10 | Quais dependências precisam continuar funcionando fora do provedor? | Delimitar estratégia de saída e portabilidade. | Cloud, dados | 3 | Lista |

### Governança e compliance — 8 perguntas

| ID | Pergunta | Objetivo e impacto arquitetural | Área afetada | Peso | Resposta |
|---|---|---|---|---:|---|
| gov-01 | Quais normas, auditorias ou certificações precisam ser atendidas? | Identificar controles e evidências obrigatórias. | Segurança, operação | 5 | Lista/Unknown |
| gov-02 | Quem aprova decisões de arquitetura e exceções? | Definir fluxo de governança sem bloquear responsabilidade. | Arquitetura, processo | 3 | Papéis |
| gov-03 | Quais decisões precisam de aprovação formal antes da implantação? | Identificar gates e rastreabilidade necessária. | Deployment, compliance | 4 | Lista |
| gov-04 | Quais componentes ou versões são proibidos pela organização? | Capturar políticas técnicas existentes. | Tecnologia, segurança | 5 | Lista |
| gov-05 | Como devem ser registradas e revistas exceções às políticas? | Planejar ADRs, validade e responsáveis. | Governança, ADR | 4 | Processo |
| gov-06 | Há exigência de evidência sobre origem e versão das dependências? | Identificar SBOM, proveniência e controles de supply chain. | Segurança, CI/CD | 4 | Requisito |
| gov-07 | Quais métricas de custo precisam ser atribuídas a equipes ou produtos? | Avaliar tagging, limites e accountability de custo. | Cloud, governança | 3 | Modelo |
| gov-08 | Com que frequência requisitos regulatórios e decisões devem ser revisados? | Definir ciclo de revisão e atualização de conhecimento. | Governança, documentação | 3 | Frequência |

## 7. Critérios de aceite do questionário

1. Cada pergunta tem ID estável, categoria, objetivo, impacto, área afetada,
   peso e tipo de resposta.
2. As 150 perguntas são opcionais no conjunto; o fluxo não exige responder
   todas para iniciar uma análise.
3. Usuário consegue marcar `Unknown`, `NotApplicable` ou recusa sem converter
   isso em resposta negativa.
4. Perguntas numéricas exigem unidade e contexto quando relevantes.
5. Respostas confirmadas podem ser alteradas, com revisão das análises
   derivadas.
6. Cada fato derivado permite rastrear pergunta, resposta, origem e versão.
7. Perguntas condicionais são declarativas, determinísticas, sem loops e sem
   avaliação de código.
8. O motor não usa peso do questionário como contribuição direta de score.
9. Um profissional consegue revisar quais respostas acionaram regras e
   influenciaram recomendações.
10. Perguntas e opções podem ser adicionadas/alteradas como conteúdo versionado,
    sem recompilar, dentro do schema suportado pelo leitor.

## 8. Riscos e decisões em aberto

- **Tamanho do questionário:** 150 itens podem intimidar usuários; validar a
  duração do caminho essencial e medir abandono.
- **Ambiguidade:** respostas como “muitos usuários” não são dados de
  capacidade; pedir escala, janela e cenário.
- **Viés de tecnologia:** opções que citam produtos podem induzir decisões
  antes da descoberta de requisitos.
- **Peso subjetivo:** pesos editoriais precisam de revisão com arquitetos;
  não devem parecer precisão científica.
- **Sobreposição:** disponibilidade, RPO/RTO, continuidade e recuperação têm
  relações, mas não devem ser somados como evidências independentes sem regra
  para evitar contagem duplicada.
- **Contexto regulatório:** o catálogo não substitui aconselhamento jurídico ou
  validação de compliance.
- **Personalização:** definir posteriormente como perfis setoriais podem
  adicionar perguntas sem modificar os IDs e dados existentes.
- **Acessibilidade e localização:** definir formato de unidades, traduções e
  leitura assistiva antes da UI final.

## 9. Justificativa das melhorias sobre o pedido inicial

- Mantive as 13 categorias e o mínimo de 150 perguntas, mas tratei as perguntas
  como banco reutilizável e adaptativo, não como formulário universal.
- Troquei “determinar a melhor arquitetura” por “produzir evidências para
  comparar alternativas”: uma arquitetura ótima depende de contexto, restrições
  e julgamento humano.
- Distingo importância da pergunta de peso de regra. Essa separação evita
  dupla contagem e scores artificiais.
- Incluí Unknown, estimativa versus medição, origem e unidades para não fabricar
  certeza.
- Organizei a elicitação em fatos de projeto que alimentam o motor já
  especificado, mantendo perguntas e recomendações desacopladas.
