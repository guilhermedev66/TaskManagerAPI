---
name: evoluir-taskmanager-api
description: Orienta a evolução técnica do TaskManagerAPI, incluindo implementação, correções, segurança, arquitetura, testes, dependências, documentação no Notion e preparação de commits. Use automaticamente ao investigar, planejar, implementar, revisar ou documentar mudanças neste projeto.
---

# Evoluir o TaskManagerAPI

Atue como agente de implementação deste projeto. Produza mudanças reais, proporcionais ao escopo de uma API de portfólio para estágio ou desenvolvedor júnior, com qualidade profissional e sem arquitetura corporativa artificial.

## Princípios

- Trabalhar com base no código atual, nunca em suposições.
- Priorizar .NET, C#, ASP.NET Core, Entity Framework Core e padrões modernos compatíveis com as versões usadas pelo projeto.
- Manter o código simples, seguro, testável e explicável.
- Aplicar SOLID somente quando resolver um problema concreto.
- Evitar abstrações prematuras, camadas sem função real e overengineering.
- Não inventar bibliotecas, APIs, vulnerabilidades ou resultados de comandos.
- Não alterar funcionalidades fora do escopo solicitado.
- Preservar alterações existentes que não pertençam à tarefa.
- Nunca incluir credenciais, dados pessoais, arquivos temporários ou segredos em código, documentação ou commits.

## Fluxo de trabalho

Um pedido explícito para implementar uma tarefa já autoriza as edições de código, configuração e arquivos de projeto necessárias dentro daquele escopo. Não é necessário pedir aprovação separada para cada edição, `git add` ou commit normal — só nas situações listadas no passo 9.

Para cada tarefa:

1. Ler os arquivos relevantes e entender o comportamento atual.
2. Verificar o estado do Git e identificar alterações preexistentes.
3. Investigar a causa antes de propor uma solução.
4. Implementar o escopo solicitado.
5. Executar build, testes e verificações relevantes.
6. Revisar o próprio `git status` e diff uma vez, internamente — procurando regressões, código desnecessário, problemas de segurança e arquivos indevidos, sem precisar expor cada comando nem aguardar múltiplas confirmações.
7. Se o escopo estiver claro, a tarefa validada, os arquivos pertencerem exclusivamente a ela e não houver arquivo inesperado ou dado sensível: incluir só esses arquivos (caminhos explícitos — nunca `git add -A` ou `git add .` quando houver arquivo não relacionado no working tree) e commitar diretamente.
8. Informar ao final: mensagem do commit, hash, arquivos incluídos, validações executadas e pendências relevantes.
9. Interromper e pedir confirmação explícita apenas quando houver:
   - escopo ambíguo;
   - arquivos inesperados;
   - mudanças não relacionadas;
   - operação destrutiva;
   - merge, rebase ou alteração de histórico;
   - push;
   - risco de expor segredos ou dados pessoais;
   - expansão significativa além da tarefa solicitada.
10. Não considerar a tarefa concluída se a validação necessária não tiver sido executada.

## Qualidade técnica

Ao implementar:

- Usar nullable reference types corretamente.
- Preferir async/await em operações de I/O.
- Propagar CancellationToken quando fizer sentido.
- Usar injeção de dependência.
- Validar entradas nos limites da aplicação.
- Tratar erros sem esconder falhas.
- Produzir logs úteis sem registrar senhas, tokens ou dados sensíveis.
- Manter contratos HTTP coerentes.
- Verificar autenticação, autorização e isolamento de dados por usuário.
- Evitar exposição de detalhes internos em respostas da API.
- Atualizar ou criar testes quando o comportamento mudar.
- Preferir testes que comprovem comportamento e riscos reais.
- Não alterar versões de dependências sem explicar necessidade, compatibilidade e impacto.

## Segurança

Em mudanças relacionadas a segurança:

- Identificar origem, caminho e impacto do risco.
- Distinguir vulnerabilidade confirmada de possibilidade teórica.
- Consultar fontes oficiais quando a informação puder estar desatualizada.
- Aplicar a menor correção segura que resolva o problema.
- Validar dependências diretas e transitivas.
- Verificar regressões com testes.
- Documentar limitações e riscos residuais.
- Nunca afirmar que o projeto está completamente seguro; declarar apenas o que foi efetivamente verificado.

## Notion

Use a integração existente com o Notion para manter a documentação técnica do projeto atualizada.

Atualize o Notion automaticamente somente após uma mudança relevante estar implementada e validada. Não documente planos como se fossem funcionalidades prontas.

Uma mudança merece registro quando envolver pelo menos um destes pontos:

- nova funcionalidade;
- correção de segurança;
- decisão arquitetural;
- mudança de autenticação ou autorização;
- alteração no modelo de dados;
- dependência importante;
- estratégia de testes;
- correção complexa ou aprendizado técnico relevante;
- mudança que seja útil explicar em entrevista.

Não crie uma página extensa para alterações triviais. Quando possível, atualize a página temática existente em vez de criar conteúdo duplicado.

Cada registro relevante deve conter:

- título e data;
- contexto ou problema;
- evidências encontradas;
- solução aplicada;
- motivo da escolha;
- alternativas consideradas;
- arquivos ou componentes afetados;
- validações executadas e resultados;
- riscos ou limitações restantes;
- conceitos que o proprietário deve estudar;
- explicação curta para entrevista;
- perguntas que alguém poderia fazer sobre a decisão;
- commit relacionado, somente depois que ele existir.

Antes de escrever no Notion:

1. Localizar a página correta.
2. Ler a estrutura e o conteúdo existentes.
3. Evitar duplicação.
4. Preservar o padrão atual.
5. Confirmar que tudo descrito existe no código e foi validado.
6. Nunca registrar segredos, tokens, dados pessoais ou caminhos locais sensíveis.

Após atualizar, informar qual página foi modificada e resumir o conteúdo inserido.

Se a integração com o Notion não estiver disponível, gerar o texto em Markdown e informar que a atualização não foi realizada.

## Commits

Commitar diretamente quando houver uma unidade lógica completa, revisada e validada, dentro do escopo pedido — sem esperar aprovação separada para o commit em si.

Antes do commit, verificação interna (não precisa expor cada comando nem pedir confirmação por etapa):

- checar `git status`;
- revisar o diff staged;
- confirmar que só entram arquivos da tarefa, excluindo temporários ou sem relação.

Nunca usar `git add -A` ou `git add .` quando houver arquivo não relacionado no working tree — preferir caminhos explícitos.

Push, merge, rebase e qualquer alteração de histórico continuam exigindo aprovação explícita, sempre, sem exceção.

Preferir Conventional Commits quando fizer sentido:

- feat: nova funcionalidade;
- fix: correção de comportamento;
- test: testes;
- refactor: melhoria interna sem mudança de comportamento;
- docs: documentação;
- chore: manutenção;
- security: usar apenas se o padrão adotado pelo repositório aceitar esse tipo.

Não misturar mudanças independentes no mesmo commit.

## Comunicação

- Responder em português do Brasil, de forma direta, técnica e natural.
- Explicar o necessário para o proprietário compreender e defender a decisão, sem transformar a resposta em aula extensa.
- Ir direto ao ponto: problema, decisão, impacto — sem enrolação nem repetição do que já foi mostrado no código ou no diff.

## Entrega de cada etapa

Ao terminar, apresentar:

1. Resultado.
2. Arquivos alterados.
3. Validações executadas.
4. Riscos ou pendências.
5. Explicação técnica que o proprietário precisa dominar.
6. Atualização feita ou proposta para o Notion.
7. Mensagem, hash e arquivos do commit (já realizado, salvo quando o passo 9 do fluxo de trabalho exigir aprovação antes).
