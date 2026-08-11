# TaskManagerAPI

Projeto de estudo com foco em backend real: uma API REST para gerenciamento de tarefas com autenticação JWT, ASP.NET Core, Entity Framework Core e SQLite.

A ideia foi simular um fluxo que acontece no dia a dia: cadastro e login, geração de token, rotas protegidas, regras de validação e evolução do banco com migrations.

## Funcionalidades

- Cadastro e login de usuarios com token JWT
- Rotas de tarefas protegidas com autorizacao
- CRUD completo de tarefas
- Validacoes com DataAnnotations
- Filtros por status e busca por titulo
- Documentacao interativa via Swagger

## Tecnologias

- .NET 10 (ASP.NET Core Web API)
- Entity Framework Core
- SQLite
- JWT Bearer Authentication
- Swashbuckle (Swagger)

## Estrutura do Projeto

- `Controllers/` - Endpoints da API (`AuthController`, `TasksController`)
- `Models/` - Entidades e DTOs
- `Data/` - `AppDbContext`
- `Migrations/` - Historico de migrations do EF Core
- `Security/` - Hash de senha simples

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
  "ExpiresInMinutes": 60
}
```

A chave de assinatura do JWT (`Jwt:Key`) nao fica no `appsettings.json` nem versionada no Git. Configure localmente com [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "Jwt:Key" "uma-chave-aleatoria-forte-com-pelo-menos-32-bytes"
```

Sem esse segredo configurado, a API falha ao iniciar com `Jwt:Key não configurado.`. Em producao, defina `Jwt:Key` via variavel de ambiente (`Jwt__Key`) ou outro cofre de segredo do ambiente de deploy — nunca em arquivo versionado.

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
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

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

## Migrations Criadas

- `InitialCreate`
- `AddUserAuth`
- `AddTaskDetailsAndValidation`

## Melhorias Futuras (sugestoes reais)

- Relacionar tarefas por usuario autenticado
- Refresh token
- Testes automatizados (xUnit)
- Docker para ambiente padronizado
- CI/CD com GitHub Actions

---

Feito por [Guilherme Santos da Silva](https://github.com/guilhermedev66).
