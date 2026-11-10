# Estilo arquitetural

Clean Architecture

## Domain

Contém:

- Entidades
- Value Objects
- Domain Events

Não referencia Application.

## Application

Contém:

- Use Cases
- Handlers
- DTOs
- Validators

Não referencia Infrastructure.

## Infrastructure

Contém:

- FileSystem
- JsonRepositories
- DiagramEngine
- Logging

## Web

Contém:

- Blazor
- Components
- Pages

Sem regras de negócio.

## KnowledgeBase

Contém somente arquivos JSON.

Nenhuma lógica.