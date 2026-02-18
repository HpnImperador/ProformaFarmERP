# ProformaFarmERP — Governança do Backend

## 1. Padrões Arquiteturais

### 1.1 Linguagem
- Schema SQL: PT-BR (tabelas e colunas)
- Classes, métodos e migrations: Inglês técnico
- DTOs: Inglês técnico
- Mensagens ao usuário: PT-BR

---

## 2. Estrutura de Resposta da API

Todas as respostas seguem o padrão:

```json
{
  "success": true | false,
  "code": "OK | ERROR_CODE",
  "message": "Mensagem descritiva",
  "correlationId": "trace-id",
  "data": { ... }
}
