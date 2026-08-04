# TaskManagerAPI

TaskManagerAPI é uma API REST para gerenciamento de tarefas, com autenticação JWT, ASP.NET Core, Entity Framework Core e SQLite — desenvolvida como projeto de estudo com foco em backend real.

A ideia foi simular um fluxo que acontece no dia a dia: cadastro e login, geração de token, rotas protegidas, regras de validação e evolução do banco com migrations.

## Funcionalidades

- Cadastro e login de usuários com token JWT
- Rotas de tarefas protegidas com autorização
- CRUD completo de tarefas
- Validações com DataAnnotations
- Filtros por status e busca por título
- Documentação interativa via Swagger

## Tecnologias

- C#
- .NET 10 (ASP.NET Core Web API)
- Entity Framework Core
- SQLite
- JWT Bearer Authentication
- Swashbuckle (Swagger)

## Estrutura do Projeto

- `Program.cs` - Configuração da aplicação, autenticação JWT e Swagger
- `Controllers/` - Endpoints da API (`AuthController`, `TasksController`)
- `Models/` - Entidades e DTOs
- `Data/` - `AppDbContext`
- `Migrations/` - Histórico de migrations do EF Core
- `Security/` - Hash de senha simples
- `appsettings.json` - Configurações da aplicação (conexão com banco, JWT)

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/en-us/download)

## Configuração

As configurações principais estão em `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=taskmanager.db"
},
"Jwt": {
  "Key": "super-secret-key-change-this-in-production",
  "Issuer": "TaskManagerAPI",
  "Audience": "TaskManagerAPIUsers",
  "ExpiresInMinutes": 60
}
```

> Em produção, altere a chave JWT para um valor forte e seguro.

## Como Executar

1. Restaurar dependências:

```bash
dotnet restore
```

2. Aplicar migrations no banco (opcional — a API também aplica automaticamente ao iniciar):

```bash
dotnet ef database update
```

3. Executar a API:

```bash
dotnet run
```

Por padrão, a API inicia no endereço exibido no terminal (ex.: `http://localhost:5078`).

## Swagger

Com a API rodando em ambiente de desenvolvimento, acesse:

- `http://localhost:5078/swagger`

Para testar rotas protegidas:

1. Faça login em `POST /api/login`
2. Copie o token retornado
3. Clique em **Authorize** no Swagger
4. Informe: `Bearer SEU_TOKEN`

## Autenticação

### Registrar usuário

- `POST /api/register`

Exemplo de body:

```json
{
  "username": "guilherme",
  "password": "123456"
}
```

Respostas possíveis: `200 OK` em caso de sucesso, `409 Conflict` se o usuário já existir.

### Login

- `POST /api/login`

Exemplo de resposta:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

Respostas possíveis: `200 OK` em caso de sucesso, `401 Unauthorized` se as credenciais forem inválidas.

## Endpoints de Tarefas (protegidos)

Todos exigem header:

`Authorization: Bearer <token>`

### Listar todas

- `GET /api/tasks`

### Buscar por ID

- `GET /api/tasks/{id}`

Retorna `404 Not Found` se a tarefa não existir.

### Criar tarefa

- `POST /api/tasks`

Exemplo de body:

```json
{
  "title": "Estudar JWT",
  "description": "Implementar autenticação no projeto",
  "priority": 2,
  "isCompleted": false,
  "dueDate": "2026-04-15T20:00:00Z"
}
```

Retorna `201 Created`. `dueDate` não pode estar no passado.

### Atualizar tarefa

- `PUT /api/tasks/{id}`

Retorna `204 No Content`. `dueDate` não pode estar no passado, exceto se a tarefa já estiver marcada como concluída.

### Deletar tarefa

- `DELETE /api/tasks/{id}`

Retorna `204 No Content`.

### Tarefas concluídas

- `GET /api/tasks/completed`

### Tarefas pendentes

- `GET /api/tasks/pending`

### Buscar por título

- `GET /api/tasks/search?title=texto`

## Regras da Entidade Task

- `Title` obrigatório, com no máximo 100 caracteres
- `Priority` enum:
  - `0 = Low`
  - `1 = Medium`
  - `2 = High`
- `CreatedAt` definido automaticamente no servidor
- `DueDate` opcional, mas não pode estar no passado ao criar ou atualizar uma tarefa pendente

## Migrations Criadas

- `InitialCreate`
- `AddUserAuth`
- `AddTaskDetailsAndValidation`
- `AddUniqueUsernameIndex`

## Melhorias Futuras (sugestões reais)

- Associar tarefas ao usuário autenticado
- Refresh token
- Testes automatizados (xUnit)
- Docker para ambiente padronizado
- CI/CD com GitHub Actions

---

Feito por [Guilherme Santos da Silva](https://github.com/guilhermedev66).

