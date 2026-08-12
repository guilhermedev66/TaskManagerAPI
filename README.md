# TaskManagerAPI

Projeto de estudo com foco em backend real: uma API REST para gerenciamento de tarefas com autenticação JWT, ASP.NET Core, Entity Framework Core e SQLite.

A ideia foi simular um fluxo que acontece no dia a dia: cadastro e login, geração de token, rotas protegidas, regras de validação e evolução do banco com migrations.

## Funcionalidades

- Cadastro e login de usuarios com access token JWT e refresh token
- Rotacao de refresh tokens, deteccao de reuso e logout com revogacao da sessao
- Rotas de tarefas protegidas com autorizacao
- Isolamento das tarefas por usuario autenticado
- CRUD completo de tarefas
- Validacoes com DataAnnotations
- Filtros por status e busca por titulo
- Testes automatizados de integracao com xUnit
- Documentacao interativa via Swagger

## Tecnologias

- .NET 10 (ASP.NET Core Web API)
- Entity Framework Core
- SQLite
- JWT Bearer Authentication
- Swashbuckle (Swagger)

## Estrutura do Projeto

- `Controllers/` - Endpoints da API (`AuthController`, `TasksController`), responsaveis por HTTP, autenticacao, entrada e saida
- `Services/` - Regras de negocio e acesso a dados (`TaskService`, `AuthService`), isolado de HTTP/ProblemDetails
- `Models/` - Entidades e DTOs
- `Data/` - `AppDbContext`
- `Migrations/` - Historico de migrations do EF Core
- `Security/` - Hash de senha, opcoes e validacao de JWT

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/en-us/download)

## Configuracao

As configuracoes nao sensiveis estao em `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=taskmanager.db"
},
"Jwt": {
  "Issuer": "TaskManagerAPI",
  "Audience": "TaskManagerAPIUsers",
  "ExpiresInMinutes": 60,
  "RefreshTokenExpiresInDays": 7
}
```

A chave de assinatura do JWT (`Jwt:Key`) nao fica no `appsettings.json` nem versionada no Git. Configure localmente com [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), gerando uma chave aleatoria (nao reaproveite um valor fixo de exemplo):

```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
```

`Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiresInMinutes` e `Jwt:RefreshTokenExpiresInDays` sao validados na inicializacao (Options Pattern): a API falha ao subir se a chave tiver menos de 32 bytes UTF-8, se Issuer/Audience estiverem vazios ou se os tempos de expiracao nao forem maiores que zero. Em producao, defina esses valores via variavel de ambiente (`Jwt__Key`, etc.) ou outro cofre de segredo do ambiente de deploy — nunca em arquivo versionado.

Os testes de integracao (`TaskManagerAPI.Tests`) nao dependem desse segredo: rodam em ambiente `Testing`, com uma chave JWT fixa e exclusiva de teste fornecida em memoria pelo `CustomWebApplicationFactory` — nunca leem os `user-secrets` da maquina.

## Como Executar

1. Restaurar dependencias:

```bash
dotnet restore
```

2. Aplicar migrations no banco:

```bash
dotnet ef database update
```

3. Executar a API:

```bash
dotnet run
```

Por padrao, a API inicia no endereco exibido no terminal (ex.: `http://localhost:5078`).

## Executar com Docker

O container executa como usuario nao root, expoe a porta `8080` e persiste o banco SQLite em um volume nomeado.

1. Copie `.env.example` para `.env`.
2. Gere uma chave aleatoria e defina `JWT_KEY` no `.env` (nao versione esse arquivo):

```bash
openssl rand -base64 48
```

3. Construa e inicie a API:

```bash
docker compose up --build
```

A API estara disponivel em `http://localhost:8080`. O Docker verifica a prontidao pelo endpoint `/health/ready`; os dados permanecem no volume `taskmanager-data` entre reinicializacoes.

Para encerrar os containers sem apagar o banco:

```bash
docker compose down
```

## Swagger

Com a API rodando, acesse:

- `http://localhost:5078/swagger`

Para testar rotas protegidas:

1. Faça login em `POST /api/login`
2. Copie o token retornado
3. Clique em **Authorize** no Swagger
4. Informe: `Bearer SEU_TOKEN`

## Autenticacao

### Registrar usuario

- `POST /api/register`

Exemplo de body:

```json
{
  "username": "guilherme",
  "password": "123456"
}
```

### Login

- `POST /api/login`

Exemplo de resposta:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "token-opaco..."
}
```

O access token e usado no header `Authorization`. O refresh token e armazenado no banco somente como hash e deve ser enviado para obter um novo par de tokens.

### Renovar tokens

- `POST /api/refresh`

```json
{
  "refreshToken": "token-opaco..."
}
```

Cada renovacao rotaciona o refresh token. A tentativa de reutilizar um token ja rotacionado revoga toda a familia da sessao.

### Logout

- `POST /api/logout`

```json
{
  "refreshToken": "token-opaco..."
}
```

O logout revoga a familia do refresh token e retorna `204 No Content`. A operacao e idempotente e nao exige um access token valido.

## Endpoints de Tarefas (protegidos)

Todos exigem header:

`Authorization: Bearer <token>`

### Listar todas

- `GET /api/tasks`

### Buscar por ID

- `GET /api/tasks/{id}`

### Criar tarefa

- `POST /api/tasks`

Exemplo de body:

```json
{
  "title": "Estudar JWT",
  "description": "Implementar autenticacao no projeto",
  "priority": 2,
  "isCompleted": false,
  "dueDate": "2026-04-15T20:00:00Z"
}
```

### Atualizar tarefa

- `PUT /api/tasks/{id}`

### Deletar tarefa

- `DELETE /api/tasks/{id}`

### Tarefas concluidas

- `GET /api/tasks/completed`

### Tarefas pendentes

- `GET /api/tasks/pending`

### Buscar por titulo

- `GET /api/tasks/search?title=texto`

## Regras da Entidade Task

- `Title` obrigatorio
- `Title` com no maximo 100 caracteres
- `Priority` enum:
  - `0 = Low`
  - `1 = Medium`
  - `2 = High`
- `CreatedAt` definido automaticamente no servidor
- `DueDate` opcional

## Formato de Erros

Respostas de erro (400, 401, 404, 409) usam o formato nativo [ProblemDetails](https://learn.microsoft.com/aspnet/core/web-api/handle-errors) do ASP.NET Core (`Content-Type: application/problem+json`), em vez de string solta ou corpo vazio.

Erro pontual (ex.: usuario duplicado, credenciais invalidas, DueDate no passado):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Usuário já existe."
}
```

Erro de validacao de campo (DataAnnotations em `Username`, `Title`, etc.) usa `ValidationProblemDetails`, com um dicionario `errors` por campo:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Username": ["The field Username must be a string with a minimum length of 3."]
  }
}
```

Tarefa de outro usuario continua retornando `404 Not Found` (nao `403`), pra nao revelar que o recurso existe — ver [Regras da Entidade Task](#regras-da-entidade-task) e o isolamento por usuario no `TasksController`.

## Migrations Criadas

- `InitialCreate`
- `AddUserAuth`
- `AddTaskDetailsAndValidation`
- `AddUniqueUsernameIndex`
- `AddTaskOwnership`
- `AddRefreshTokens`

## Melhorias Futuras (sugestoes reais)

- Docker para ambiente padronizado
- CI/CD com GitHub Actions

---

Feito por [Guilherme Santos da Silva](https://github.com/guilhermedev66).
