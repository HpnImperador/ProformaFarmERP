# INTEGRACOES N8N – DIRETRIZES ARQUITETURAIS WORLD-CLASS
## ProformaFarm ERP

---
## 1. Premissas Estratégicas

A integração com n8n NÃO transforma o ProformaFarmERP em um sistema orientado a automações externas.
O ERP continua sendo o núcleo transacional e autoridade de domínio.

O n8n deve atuar como:
- Orquestrador de integrações
- Executor de workflows externos
- Hub de notificações e integrações de borda

Nunca como:
- Motor transacional primário
- Substituto de regras de domínio
- Executor de lógica fiscal/financeira crítica

---
## 2. Riscos Identificados

1. Quebra de isolamento multiempresa (OrgContext bypass)
2. Duplicidade de execução (webhooks repetidos)
3. Corrupção de estoque ou financeiro por chamadas concorrentes
4. Falta de rastreabilidade e auditoria
5. Acoplamento forte entre ERP e workflows externos

---
## 3. Modelo Arquitetural Obrigatório

### 3.1 Outbound (ERP → n8n)

Fluxo:
Dominio → DomainEvent → Outbox → EventRelay → Webhook n8n

Regras:
- Nunca chamar n8n dentro da transação principal.
- Sempre usar Outbox Pattern.
- Assinar payload com HMAC.
- Registrar cada tentativa em IntegrationDeliveryLog.

---
### 3.2 Inbound (n8n → ERP)

Fluxo:
n8n → Integration API → Autenticação por IntegrationClient → Derivação automática de OrganizacaoId → Comando interno

Regras:
- Proibir header livre de OrganizacaoId.
- API Key vinculada a OrganizacaoId.
- Exigir Idempotency-Key.
- Registrar execução em IdempotencyLog.
- Aplicar validações completas de domínio.

---
## 4. Camada de Integração (Novo Boundary)

Criar domínio: Integracao

Responsabilidades:
- Registro de clientes de integração
- Logs de entrega
- Controle de idempotência
- Rate limiting
- Assinatura de payload

---
## 5. Segurança e Governança

- API Keys armazenadas com hash.
- Rotacionamento de credenciais.
- Logs com CorrelationId.
- Auditoria de comandos disparados por integração.
- Rate limit por organização.

---
## 6. Escopo Inicial Seguro

Fase 1 (baixo risco):
- Evento VendaFinalizada → webhook n8n
- Evento EstoqueBaixo → webhook n8n

Fase 2 (controle reforçado):
- Webhook inbound para registrar tarefa ou notificação
- Nenhuma operação fiscal automática nesta fase

---
## 7. Critério de Segurança para Evolução

Somente evoluir para integrações fiscais ou financeiras quando:
- Outbox estiver estável
- Idempotência validada em testes de concorrência
- Auditoria integrada
- Logs estruturados com métricas

---
## 8. Conclusão

A integração com n8n deve ser tratada como plataforma de integração desacoplada, com governança rígida.
O ERP permanece autoridade transacional.
