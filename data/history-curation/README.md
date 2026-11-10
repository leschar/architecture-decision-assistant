# Curadoria de similaridade histórica

Coloque nesta pasta cópias JSON de projetos/snapshots históricos
representativos, um arquivo por projeto. Os arquivos devem seguir
[`project.schema.json`](../../schemas/project.schema.json).

Use dados reais e variados, mas remova ou anonimize nomes, justificativas,
requisitos e outros textos sensíveis quando não forem necessários à comparação.
Preserve os valores estruturados que devam ser avaliados. Não inclua arquivos
de índice, registros de arquivo ou políticas de similaridade.

Os arquivos JSON desta pasta são ignorados pelo Git para evitar publicar dados
de projetos acidentalmente. Eles permanecem locais; a aplicação não os carrega
como histórico nem cria uma política automaticamente. A curadoria ocorrerá
explicitamente depois que os exemplos forem fornecidos.
