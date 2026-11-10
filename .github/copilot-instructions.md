# Projeto

Architecture Decision Assistant

Plataforma de apoio a decisões arquiteturais.

# Objetivo

Auxiliar arquitetos de software na definição de:

- Arquiteturas
- Tecnologias
- Design Patterns
- Princípios
- Trade-offs
- ADRs
- Diagramas

# Restrições

- Não utilizar IA em runtime.
- Não utilizar OpenAI.
- Não utilizar Azure OpenAI.
- Não utilizar banco de dados inicialmente.
- Toda informação deve ficar em arquivos JSON.
- Todo conhecimento deve ser extensível sem recompilação.

# Stack

- .NET 10 LTS
- C#
- Blazor Server
- MediatR
- FluentValidation
- Serilog

# Arquitetura

Seguir Clean Architecture.

Projects:

- ArchitectureAssistant.Web
- ArchitectureAssistant.Application
- ArchitectureAssistant.Domain
- ArchitectureAssistant.Infrastructure
- ArchitectureAssistant.KnowledgeBase

Hospedagem inicial: self-hosted local para um usuário. Persistir somente em
JSON no workspace configurado no host; não habilitar acesso remoto ou
multiusuário implicitamente.

# Princípios

Sempre priorizar:

- SOLID
- DRY
- KISS
- Clean Code
- DDD
- CQRS apenas quando necessário

Evitar:

- Serviços estáticos
- Código duplicado
- Acoplamento forte
- Dependências circulares

# Persistência

Não usar banco.

Utilizar:

- JSON
- YAML
- Arquivos locais

# Diagramas

Todos os diagramas devem ser baseados em grafos.

Nós:

- Arquitetura
- Tecnologia
- Pattern

Arestas:

- DependsOn
- Recommends
- ConflictsWith
- AlternativeTo

# Regras

Nunca implementar lógica de negócio na UI.

Toda regra deve ficar na camada Application ou Domain.

Validar entrada usando FluentValidation.

Utilizar Result Pattern ao invés de Exceptions para regras de negócio.