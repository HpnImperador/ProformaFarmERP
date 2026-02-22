# EVOLUCAO ESTRATEGICA WORLD CLASS 2
## Pre-Requisitos Arquiteturais para Evolucao World-Class
## ProformaFarm ERP

---

## 1. Objetivo

Este documento estabelece os pre-requisitos arquiteturais obrigatorios antes da expansao dos modulos:

- Clinica (Servicos de Saude)
- CRM de Adesao ao Tratamento
- Inteligencia Artificial para Estoque

O objetivo e garantir que o ProformaFarm ERP evolua para um patamar world-class sem gerar divida tecnica estrutural invisivel.

---

# 2. Fundamentos Arquiteturais Obrigatorios

Antes da expansao funcional, os seguintes pilares devem estar consolidados:

1. Infraestrutura formal de Domain Events
2. Implementacao de Outbox Pattern
3. Auditoria estruturada por dominio
4. Politica de dados sensiveis (LGPD-ready)
5. Governanca de modelos de IA (versionamento e rastreabilidade)

---

# 3. Blueprint de Domain Events + Outbox Pattern

## 3.1 Objetivo

Permitir comunicacao desacoplada entre dominios (Comercial, Estoque, CRM, Clinica) garantindo:

- Consistencia transacional
- Resiliencia
- Idempotencia
- Auditabilidade

## 3.2 Estrutura de Domain Events (Aplicacao)

Criar contrato base:

- IDomainEvent
- BaseDomainEvent
    - EventId (GUID)
    - OccurredOnUtc (DateTimeOffset)
    - CorrelationId (GUID)
    - OrganizacaoId (obrigatorio)

Eventos devem sempre carregar OrganizacaoId para alinhamento com OrgContext.

## 3.3 Estrutura SQL – Outbox

CREATE TABLE Core.OutboxEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(200) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    OccurredOnUtc DATETIMEOFFSET NOT NULL,
    ProcessedOnUtc DATETIMEOFFSET NULL,
    Status INT NOT NULL DEFAULT 0,
    RetryCount INT NOT NULL DEFAULT 0,
    CorrelationId UNIQUEIDENTIFIER NULL
);

CREATE INDEX IX_Outbox_Status ON Core.OutboxEvents (Status, OccurredOnUtc);

## 3.4 Fluxo de Execucao

1. Dominio executa comando (ex: FinalizarVenda).
2. Evento de dominio e registrado na mesma transacao.
3. Evento e persistido na tabela Outbox.
4. Background Worker processa eventos pendentes.
5. Evento e publicado internamente ou externamente.
6. Status atualizado para Processed.

## 3.5 Regras Obrigatorias

- Nenhum evento pode ser disparado fora de transacao.
- Eventos devem ser idempotentes.
- Falha no handler nao pode quebrar transacao principal.
- Processamento deve permitir retry com backoff.

---

# 4. Auditoria Estruturada por Dominio

## 4.1 Objetivo

Garantir rastreabilidade completa para:

- Dados financeiros
- Dados clinicos
- Sugestoes de IA
- Alteracoes criticas de estoque

## 4.2 Estrutura Sugerida

CREATE TABLE Core.AuditLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    Dominio NVARCHAR(100) NOT NULL,
    Entidade NVARCHAR(100) NOT NULL,
    EntidadeId UNIQUEIDENTIFIER NOT NULL,
    Acao NVARCHAR(50) NOT NULL,
    UsuarioId UNIQUEIDENTIFIER NOT NULL,
    DadosAntes NVARCHAR(MAX) NULL,
    DadosDepois NVARCHAR(MAX) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL
);

CREATE INDEX IX_Audit_Entidade ON Core.AuditLog (Entidade, EntidadeId);

## 4.3 Dados Sensiveis

Para Clinica:

- Registrar log de leitura (quando aplicavel)
- Avaliar criptografia por coluna
- Registrar hash de integridade dos registros clinicos

---

# 5. Governanca de Modelos de IA

Antes de implementar sugestoes preditivas, incluir:

Campos adicionais em Estoque.SugestoesCompraIA:

- ModeloVersao NVARCHAR(50)
- ParametrosModelo NVARCHAR(MAX)
- DatasetReferenciaHash NVARCHAR(128)

Objetivo:

- Permitir auditoria de decisoes automaticas
- Garantir rastreabilidade regulatoria
- Evitar comportamento "caixa preta"

---

# 6. Politica de Dados Sensiveis (LGPD Ready)

Obrigatorio antes de Clinica:

- Mapeamento de dados sensiveis
- Controle de acesso granular
- Log de acesso a dados clinicos
- Politica de retencao de dados

---

# 7. Plano de Endurecimento Arquitetural – 60 Dias

## Fase 1 (Dias 1–15)

- Implementar infraestrutura base de Domain Events.
- Criar tabela Outbox.
- Implementar worker de processamento.
- Criar testes de resiliencia.

## Fase 2 (Dias 16–30)

- Implementar AuditLog generico.
- Integrar auditoria em Estoque e Organizacao.
- Formalizar guideline de persistencia com OrganizacaoId obrigatorio.

## Fase 3 (Dias 31–45)

- Implementar padrao de eventos para EstoqueBaixoEvent.
- Validar idempotencia.
- Implementar versionamento de modelo IA (estrutura apenas).

## Fase 4 (Dias 46–60)

- Hardening:
    - Stress tests de processamento de eventos.
    - Validacao de concorrencia.
    - Revisao de indices.
- Documentacao formal dos padroes.
- Atualizacao de README e diretrizes tecnicas.

---

# 8. Criterio de Liberacao para Expansao Clinica/IA

A expansao so deve iniciar quando:

- Outbox estiver estavel.
- Auditoria integrada em dominios criticos.
- Testes de resiliencia aprovados.
- Politica de dados sensiveis documentada.

---

# 9. Conclusao

O ProformaFarm ERP possui base arquitetural solida (OrgContext, enforcement, padronizacao de exportacoes).

Para atingir nivel world-class real, e necessario consolidar infraestrutura transversal antes de adicionar novos dominios de alta complexidade.

A ordem correta e:

1. Infraestrutura transversal (Eventos + Auditoria).
2. Governanca e seguranca.
3. Expansao funcional (Clinica, CRM, IA).

Somente assim o crescimento ocorrera sem divida arquitetural oculta.
