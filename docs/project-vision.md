# Visão do Produto

## 1. Identificação

- **Nome de trabalho:** Architecture Decision Assistant
- **Categoria:** Plataforma de apoio à decisão arquitetural
- **Público inicial:** Arquitetos de software e equipes de desenvolvimento
- **Plataforma inicial:** Aplicação web com Blazor Server
- **Princípio central:** apoiar, explicar e registrar decisões; nunca substituir o julgamento do arquiteto.

## 2. Visão

Capacitar arquitetos e equipes de software a tomar decisões arquiteturais mais
consistentes e justificáveis, reunindo requisitos, conhecimento arquitetural
estruturado, regras explícitas e relações entre alternativas em um único fluxo
de trabalho.

A plataforma transforma requisitos e escolhas do usuário em recomendações
rastreáveis de arquiteturas, tecnologias e patterns. Para cada recomendação,
apresenta os fatores considerados, benefícios, riscos, dependências, conflitos,
trade-offs e alternativas. O arquiteto permanece responsável por avaliar,
aceitar, ajustar ou rejeitar cada sugestão.

## 3. Propósito e objetivos

### Propósito

Reduzir o esforço necessário para explorar e comunicar decisões arquiteturais,
sem ocultar critérios nem depender de uma inteligência opaca.

### Objetivos do produto

1. **Organizar decisões:** manter requisitos, alternativas, recomendações,
   diagramas e ADRs associados a um projeto.
2. **Tornar recomendações explicáveis:** permitir que o usuário identifique as
   regras, requisitos e relações que contribuíram para cada resultado.
3. **Explicitar consequências:** apresentar benefícios, riscos, dependências,
   incompatibilidades e trade-offs antes de uma escolha.
4. **Apoiar diferentes formas de entrada:** permitir captura manual,
   questionários guiados e, em evolução posterior, interpretação determinística
   de texto livre.
5. **Promover consistência:** aplicar critérios repetíveis a partir de uma base
   de conhecimento versionada e validada.
6. **Facilitar comunicação:** gerar diagramas e ADRs editáveis a partir das
   decisões registradas.
7. **Permitir evolução do conhecimento:** possibilitar a inclusão ou atualização
   de arquiteturas, tecnologias, patterns e regras por arquivos JSON, sem exigir
   recompilação da aplicação.
8. **Preservar portabilidade:** armazenar inicialmente o conhecimento e os
   projetos em arquivos, evitando dependência de banco de dados.

### Indicadores de sucesso propostos

Os valores abaixo são metas iniciais a validar com usuários durante o MVP, não
garantias de desempenho ou precisão:

- Toda recomendação exibida possui justificativa rastreável a requisitos e/ou
  regras avaliadas.
- O usuário consegue revisar e editar as entradas antes de executar a análise.
- O usuário consegue exportar e reabrir um projeto sem perda de seus requisitos
  e decisões registradas.
- Um novo item de conhecimento JSON válido pode ser carregado sem recompilar a
  aplicação.
- Em testes com arquitetos, a maioria dos participantes consegue localizar os
  fatores determinantes e os principais trade-offs de uma recomendação sem
  consultar documentação externa.

## 4. Usuários e personas

### Persona primária — Arquiteta de software

- Define ou avalia arquiteturas para novos sistemas e modernizações.
- Precisa comparar alternativas e registrar os motivos das decisões.
- Valoriza transparência, rastreabilidade e controle sobre o resultado.
- Não aceita recomendações sem evidência ou que escondam limitações.

### Persona secundária — Tech Lead / Engenheiro de software sênior

- Participa das decisões técnicas e traduz requisitos em impactos de
  implementação.
- Precisa alinhar a equipe sobre dependências, riscos e consequências
  operacionais.
- Usa o resultado como insumo para discussão, não como decisão automática.

### Persona secundária — Product Owner / Analista de negócios

- Descreve objetivos, restrições e requisitos funcionais e não funcionais.
- Precisa entender como requisitos como disponibilidade, volume, segurança ou
  prazo afetam as alternativas técnicas.
- Pode responder perguntas guiadas sem precisar escolher tecnologias de
  antemão.

### Persona futura — Curador da base de conhecimento

- Mantém e revisa arquivos JSON de arquiteturas, tecnologias, patterns e regras.
- Precisa validar estrutura, referências e versões antes de disponibilizar
  alterações aos usuários.
- Deve conseguir evoluir o conteúdo sem alterar ou recompilar o motor.

## 5. Problemas que o produto resolve

1. **Decisões dispersas:** requisitos e justificativas ficam em conversas,
   documentos e ferramentas distintas.
2. **Escolhas sem contexto suficiente:** tecnologias populares podem ser
   selecionadas sem verificar aderência à equipe, aos requisitos ou à
   capacidade operacional.
3. **Consequências pouco visíveis:** dependências, conflitos, custos e riscos
   aparecem tarde no projeto.
4. **Comparações inconsistentes:** alternativas são avaliadas com critérios
   diferentes ou sem registrar por que uma opção foi descartada.
5. **Conhecimento difícil de reutilizar:** decisões anteriores não são
   estruturadas para consulta ou comparação.
6. **Documentação manual custosa:** diagramas e ADRs frequentemente ficam
   desatualizados em relação às decisões reais.
7. **Recomendações opacas:** ferramentas que não explicam critérios não oferecem
   base suficiente para revisão humana.

## 6. Proposta de valor e princípios de experiência

- **Apoio, não automação da responsabilidade:** nenhuma recomendação é uma
  decisão final por si só.
- **Explicabilidade por padrão:** a justificativa acompanha o resultado; não é
  um recurso opcional ou posterior.
- **Controle do usuário:** entradas, pesos e escolhas confirmadas devem poder
  ser revisados pelo usuário.
- **Separação entre fato e inferência:** requisitos informados, regras
  disparadas e sugestões calculadas devem ser identificáveis separadamente.
- **Transparência sobre lacunas:** ausência de conhecimento ou de dados não deve
  ser apresentada como certeza.
- **Evolução segura:** conteúdo extensível deve ser validado e versionado.

## 7. Escopo do MVP

O MVP deve validar o fluxo principal de análise arquitetural, com foco em
entradas explícitas e recomendações explicáveis.

### Incluído

- Criar, abrir, editar e salvar um projeto local em arquivo JSON.
- Registrar requisitos funcionais e não funcionais manualmente.
- Registrar restrições e contexto básico do projeto, como tamanho da equipe,
  criticidade e necessidades de escala.
- Consultar uma base inicial de conhecimento em arquivos JSON.
- Avaliar um conjunto inicial de regras determinísticas e versionadas.
- Recomendar um conjunto limitado de arquiteturas e tecnologias conhecidas pela
  base.
- Apresentar critérios considerados, justificativas, benefícios, riscos,
  dependências, conflitos, alternativas e trade-offs disponíveis.
- Permitir que o arquiteto aceite, rejeite ou ajuste recomendações e registre
  notas.
- Validar os arquivos JSON e informar erros de conteúdo de forma clara.
- Exportar os dados do projeto em formato portável.

### Fora do MVP

- Interpretar texto livre com NLP.
- Questionário amplo e adaptativo.
- Geração de diagramas C4 Context, Container e Component a partir de estrutura
  explícita e decisões confirmadas, com preview reativo, proveniência e estado
  de incompletude; topologia não é inventada a partir de recomendações.
- Geração completa de ADRs.
- Busca de projetos similares e estatísticas históricas.
- Colaboração simultânea, controle de acesso corporativo e implantação
  multiusuário.
- Integrações com provedores de nuvem, repositórios ou ferramentas externas.
- Recomendação autônoma, execução de mudanças ou provisionamento de
  infraestrutura.

### Critério de conclusão do MVP

Um usuário consegue criar um projeto, informar requisitos, executar uma análise
determinística, entender por que cada alternativa foi sugerida ou descartada,
registrar sua decisão e exportar o projeto sem depender de banco de dados ou de
serviços de IA.

## 8. Escopo completo pretendido

O produto poderá evoluir para incluir:

- Entrada por questionário arquitetural organizado por categorias e pesos.
- Interpretação determinística de texto livre por dicionários, taxonomias,
  expressões compostas e sinônimos explícitos, com evidências, tratamento de
  negação/ambiguidade e confirmação explícita das hipóteses pelo usuário. A
  interpretação não confirma requisitos nem promete precisão sem avaliação;
  sua especificação está em
  [requirement-text-interpretation.md](./requirement-text-interpretation.md).
- Motor de regras com pontuação, confiança, ranking, tratamento de conflitos e
  explicação da cadeia de decisão.
- Grafo navegável de arquiteturas, tecnologias, patterns e suas relações:
  dependências, recomendações, conflitos e alternativas.
- Comparação de arquiteturas com critérios e trade-offs lado a lado.
- Geração e edição de diagramas a partir de requisitos, decisões e relações do
  grafo.
- Geração de ADRs editáveis, com contexto, problema, alternativas, decisão,
  justificativa, benefícios, riscos e consequências. O ADR é um rascunho
  rastreável de uma decisão humana; ver
  [adr-generation.md](./adr-generation.md).
- Histórico local opt-in de snapshots JSON, comparação por dimensões
  estruturadas, busca determinística e reutilização explícita de decisões
  anteriores; similaridade não é recomendação. Ver
  [architectural-knowledge-history.md](./architectural-knowledge-history.md).
- Ferramentas de curadoria, validação, versionamento e compatibilidade da base
  de conhecimento.
- Recursos de equipe e governança, após validação de necessidade e definição de
  requisitos de segurança e operação.

## 9. Casos de uso principais

1. **Criar projeto arquitetural:** informar nome, objetivo, contexto e
   restrições do sistema.
2. **Capturar requisitos:** registrar requisitos e atributos de qualidade,
   indicando prioridade e origem quando aplicável.
3. **Explorar a base de conhecimento:** consultar definições, critérios de uso,
   vantagens, desvantagens, riscos e relações dos itens disponíveis.
4. **Executar análise:** avaliar os requisitos contra regras e relações
   conhecidas.
5. **Analisar recomendação:** inspecionar justificativas, regras disparadas,
   fatores favoráveis e desfavoráveis, dependências, conflitos e trade-offs.
6. **Comparar alternativas:** comparar opções elegíveis e entender as razões
   para diferenças de pontuação ou adequação.
7. **Registrar decisão humana:** aceitar, rejeitar ou modificar uma sugestão e
   documentar a justificativa do arquiteto.
8. **Salvar e exportar projeto:** preservar entradas, resultados e decisões em
   arquivos portáveis.
9. **Atualizar conhecimento:** adicionar ou revisar itens JSON, validar suas
   referências e disponibilizá-los sem recompilar a aplicação.

## 10. Casos de uso futuros

- Responder a questionário guiado para elicitar requisitos ainda desconhecidos.
- Inserir texto livre e revisar as tags identificadas pelo mecanismo
  determinístico.
- Visualizar e editar diagramas C4 Context, Container e Component. A geração
  automática exige modelo estrutural explícito e mantém elementos propostos
  distintos dos confirmados; ver
  [diagram-generation.md](./diagram-generation.md).
- Gerar, revisar e exportar ADRs.
- Encontrar snapshots históricos semelhantes e comparar decisões com
  similaridade determinística explicável.
- Importar e exportar conhecimento em formatos documentados.
- Compartilhar projetos, aplicar permissões e manter trilha de auditoria em
   ambientes de equipe, mediante decisão de produto específica.
- Configurar conjuntos de regras adequados a domínios ou padrões organizacionais.

## 11. Roadmap evolutivo

### Fase 1 — Visão e MVP

- Confirmar personas, problema, limites do produto e fluxo principal.
- Definir o modelo mínimo de projeto e os formatos JSON.
- Entregar cadastro de requisitos, conhecimento inicial, regras básicas e
  explicação de recomendações.
- Salvar e exportar projetos como arquivos.

### Fase 2 — Domínio e base de conhecimento

- Consolidar bounded contexts, agregados e conceitos do domínio.
- Definir schemas, identificadores, versionamento e validação dos itens de
  conhecimento.
- Expandir os tipos de conteúdo arquitetural e seus relacionamentos.

### Fase 3 — Motor de decisão

- Implementar avaliação determinística, pontuação, ranking e tratamento de
  conflitos.
- Expor a origem e a contribuição das regras para cada resultado.
- Tornar explícitas incertezas e dados insuficientes.

### Fase 4 — Elicitação de requisitos

- Adicionar questionário guiado por categorias.
- Introduzir interpretação lexical determinística de texto livre como fluxo
  assistido e revisável, sem IA generativa ou serviços de IA em runtime.

### Fase 5 — Grafo e comparação

- Modelar relações entre arquiteturas, tecnologias e patterns.
- Detectar dependências, conflitos e alternativas.
- Permitir comparação estruturada de opções e consequências.

### Fase 6 — Diagramas e ADRs

- Gerar diagramas a partir das entradas e decisões confirmadas.
- Gerar documentos ADR editáveis e manter sua relação com o projeto.

### Fase 7 — Histórico e colaboração

- Reutilizar projetos anteriores com consentimento e rastreabilidade.
- Avaliar busca por similaridade, colaboração, governança e implantação
  compartilhada.
- Adotar novos mecanismos de persistência somente se os requisitos futuros
  justificarem a mudança de restrição inicial.

As fases são evolutivas e devem ser priorizadas por validação com usuários,
risco e dependências; a sequência não implica compromisso de prazo.

## 12. Restrições e decisões de produto

- **Sem IA em runtime:** o produto não utiliza IA generativa, OpenAI ou Azure
  OpenAI. A inteligência do sistema é determinística e baseada em regras,
  grafos, taxonomias e conhecimento estruturado.
- **Sem banco de dados inicialmente:** conhecimento e projetos são armazenados
  em arquivos locais, principalmente JSON. A arquitetura deve manter os
  mecanismos de armazenamento substituíveis, sem antecipar uma migração.
- **Conhecimento extensível:** adicionar ou alterar conteúdo válido não pode
  exigir recompilação; a aplicação deve descobrir e validar os arquivos
  conforme um contrato documentado.
- **Base explicável:** não se deve recomendar uma tecnologia sem expor os
  motivos disponíveis, as evidências consideradas e as limitações relevantes.
- **Responsabilidade humana:** o sistema não toma decisões nem altera
  arquiteturas automaticamente.
- **Tecnologia inicial:** .NET 10 LTS e Blazor Server self-hosted local. A
  mudança explícita de .NET 9 foi aprovada antes do início da implementação,
  por seu fim de suporte em 10/11/2026. Experiência desktop poderá ser avaliada
  como evolução; não é requisito do MVP.
- **Arquitetura da solução:** Clean Architecture, com regras de domínio e
  aplicação fora da UI; validação de entrada com FluentValidation conforme a
  arquitetura técnica for detalhada.
- **Persistência de conhecimento:** arquivos JSON validados por schemas e
  identificadores estáveis; formato e compatibilidade devem ser documentados.
- **Versão de .NET:** a implementação foi aprovada em .NET 10 LTS em lugar do
  requisito inicial .NET 9, cujo fim de suporte ocorre em 10/11/2026. Manter
  essa decisão explícita nos projetos e revisar suporte antes de releases
  produtivos. Ver
  [dotnet-solution-architecture.md](./dotnet-solution-architecture.md).

## 13. Trade-offs e alternativas

| Decisão | Benefícios | Custos e riscos | Alternativa considerada |
|---|---|---|---|
| Regras determinísticas, sem IA em runtime | Resultados reproduzíveis, auditáveis e alinhados às restrições do produto | A interpretação de linguagem natural e casos ambíguos exige dicionários e confirmação humana | IA generativa poderia ampliar a flexibilidade, mas foi excluída pelas restrições |
| JSON e arquivos locais no início | Simplicidade, portabilidade, inspeção direta e nenhuma dependência de banco | Concorrência, busca e governança podem ficar limitadas à medida que o volume cresce | Banco de dados desde o início foi considerado, mas adicionaria operação e complexidade antes de haver necessidade validada |
| Blazor Server para a primeira experiência web | Reutilização da stack .NET e entrega integrada de interface e aplicação | Requer conexão ativa com o servidor e decisões posteriores sobre implantação e escala | Blazor WebAssembly ou aplicação desktop podem ser avaliados conforme os requisitos de distribuição |
| MVP baseado em entrada explícita | Reduz ambiguidade e permite validar primeiro a qualidade das regras e explicações | Exige mais trabalho manual do usuário inicialmente | NLP determinístico e questionários extensos ficam para fases posteriores |
| Apoio à decisão, sem decisão automática | Mantém responsabilidade e contexto com o arquiteto | O benefício depende da revisão e participação do usuário | Automatizar a escolha final não é compatível com o propósito do produto |

## 14. Limites e riscos do produto

- Recomendações dependem da qualidade, atualidade e cobertura da base de
  conhecimento.
- Pontuações não devem ser apresentadas como garantia de sucesso ou como
  verdade objetiva.
- Regras conflitantes, referências quebradas e conteúdo desatualizado podem
  reduzir a confiança; validação e rastreabilidade são requisitos centrais.
- A interpretação lexical de texto pode deixar de identificar contexto, negação
  ou ambiguidade; qualquer inferência desse tipo deve ser revisável pelo
  usuário.
- Arquivos locais simplificam o começo, mas não resolvem por si só colaboração
  simultânea, controle de acesso ou recuperação centralizada.
- Diagramas e ADRs gerados devem permanecer editáveis e refletir as decisões
  confirmadas, não apenas recomendações ainda não aceitas.

## 15. Critérios norteadores para decisões futuras

Uma funcionalidade ou decisão de produto deve:

1. preservar explicabilidade e controle humano;
2. respeitar a ausência de IA em runtime e de banco de dados no escopo inicial;
3. manter o conhecimento extensível por contratos de arquivos documentados;
4. evitar duplicar regras de negócio na interface;
5. produzir informação rastreável a requisitos, regras ou decisões do usuário;
6. justificar seus benefícios e trade-offs antes de ampliar o escopo.
