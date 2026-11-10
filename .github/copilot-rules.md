# Regras para geração de código

Sempre:

- Utilizar record quando fizer sentido.
- Utilizar readonly.
- Utilizar async/await.
- CancelationToken obrigatório.
- Injeção de dependência obrigatória.

Nunca:

- Criar classes God Object.
- Criar métodos com mais de 100 linhas.
- Criar services gigantes.
- Colocar regra na camada UI.

Todos os handlers:

- Devem retornar Result<T>

Todas as validações:

- Devem usar FluentValidation

Todos os logs:

- Devem usar Serilog

Todos os contratos:

- Devem possuir interface.