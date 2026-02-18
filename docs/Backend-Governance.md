Backend-Governance.md (VERSÃO OFICIAL)
# ProformaFarmERP — Governança do Backend

Documento oficial de diretrizes arquiteturais, padrões técnicos e políticas de segurança do backend.

---

# 1. Linguagem e Convenções

## 1.1 Banco de Dados
- Tabelas e colunas: PT-BR
- Chaves primárias: IdEntidade (ex: IdUsuario, IdVenda)
- Foreign Keys explícitas
- Datas em UTC
- Prefixo dbo obrigatório

## 1.2 Código
- Classes e métodos: Inglês técnico
- DTOs: Inglês técnico
- Services e Repositories: Inglês técnico
- Mensagens ao usuário: PT-BR

---

# 2. Estrutura de Resposta da API

Todas as respostas seguem o padrão:

```json
{
  "success": true | false,
  "code": "OK | ERROR_CODE",
  "message": "Mensagem descritiva",
  "correlationId": "trace-id",
  "data": { ... }
}
```
Classe base: ApiResponse<T>

Regra:

Nunca retornar exception crua.

Nunca retornar 200 com erro lógico.

CorrelationId sempre presente.

Regra adicional:
- Endpoints que retornarem null devem responder com 401, 404 ou 204, nunca 200 com data null.

# 3. Mapeamento Oficial de Status Code
Status	Quando usar
200	Operação bem-sucedida
400	Regra de negócio inválida (AppException)
401	Não autenticado / Token inválido
403	Autenticado sem permissão
404	Recurso não encontrado
409	Conflito de estado
500	Erro interno inesperado

# 4. Autenticação e Segurança
# 4.1 JWT

Access Token com expiração curta

Claims mínimas necessárias

Nunca armazenar dados sensíveis no token

Claims obrigatórias no Access Token:
- sub (IdUsuario)
- unique_name (Login)
- nome
- uid
- role
- iss
- aud
- exp

# 4.2 Refresh Token

Armazenado como HASH

Rotação obrigatória

Token utilizado é revogado

Reutilização retorna 401

Multi-sessão permitida (novo login NÃO invalida sessões anteriores)

# 4.3 Política de Segurança

Tokens expirados nunca retornam do banco

Revogação protegida contra replay

Índice obrigatório em TokenHash

# 5. Política de Transações

Operações críticas devem ser atômicas:

Refresh token rotation

Movimentações financeiras

Movimentações de estoque

Cancelamentos

Estornos

Abertura/Fechamento de caixa

Se houver múltiplas operações dependentes, devem ocorrer dentro da mesma transação.

# 6. Step-Up Authentication (Módulos Críticos)

Módulos sensíveis exigem reautenticação de operador:

PDV

Cancelamento de venda

Desconto acima do limite

Sangria / Suprimento

Estorno

Reimpressão fiscal

Alteração de preço

Fluxo:

Login padrão abre sessão do ambiente.

Operador autentica para módulo crítico.

Auditoria registra IdUsuarioAmbiente + IdOperador.

# 7. Auditoria

Eventos críticos devem registrar:

IdUsuarioAmbiente

IdOperador

IdLoja

IdTerminal

DataHoraUtc

Evento

Referência (IdVenda, IdCaixa, etc)

# 8. Princípios Arquiteturais

Clean Architecture

Separação clara entre:

Domain

Application

Infrastructure

API

Domain não depende de Infrastructure

Infra não contém regra de negócio

Services não acessam diretamente banco (via Repository)

Dependências apontam para o centro

# 9. Banco de Dados — Regras Obrigatórias

Índices em colunas de busca frequente

FK obrigatórias

Constraints de integridade

Nunca depender apenas de validação no código

Datas sempre em UTC

Evitar campos nullable desnecessários

# 10. Observabilidade

CorrelationId em todas as requisições

Logs estruturados

Erros centralizados via middleware

Nunca expor stacktrace em produção

# 11. Próximos Passos Evolutivos

ExceptionMiddleware definitivo

Política de autorização refinada por módulo

Auditoria estruturada

Testes automatizados para Auth

Validação transacional de operações críticas

# 12. Versionamento da API

- Padrão: /api/v1/
- Mudanças breaking exigem nova versão
- Nunca alterar contrato existente sem versionamento
