# PROFORMA FARM ERP

# Diretrizes Arquiteturais Obrigatórias

Documento normativo interno que estabelece regras técnicas obrigatórias
para evolução do ProformaFarmERP. Este documento deve ser tratado como
contrato arquitetural para qualquer desenvolvimento futuro.

------------------------------------------------------------------------

## 0. Posicionamento Oficial do Produto

O ProformaFarmERP NÃO é um produto API-only.

O escopo oficial contempla três camadas integradas e igualmente
estratégicas:

1.  API de domínio e integração
    -   Responsável por regras de negócio, persistência, segurança e
        integrações externas.
    -   Base para integrações fiscais, gateways, hubs e automações.
2.  Painel Backend (retaguarda administrativa)
    -   Interface administrativa para gestão operacional, financeira e
        regulatória.
    -   Consome a API de domínio.
    -   Deve respeitar integralmente OrgContext e regras multiempresa.
3.  Frontend de Operação Omnichannel
    -   Interface de operação (PDV, pedidos, logística, etc.).
    -   Pode operar em múltiplos canais (web, dispositivos dedicados,
        integrações externas).
    -   Também consome a API e respeita isolamento organizacional.

Diretriz estrutural:

A arquitetura deve sempre considerar essas três camadas como partes
oficiais do produto. Qualquer decisão técnica deve avaliar impacto nas
três camadas.

------------------------------------------------------------------------

## 1. Princípios Fundamentais

1.  Arquitetura: Modular Monolith Evolutivo.
2.  Separação explícita de domínios (Bounded Contexts).
3.  OrgContext obrigatório para qualquer operação transacional.
4.  Single Database com segregação lógica por OrganizacaoId e UnidadeId.
5.  Escrita com EF Core. Leitura otimizada com Dapper.
6.  Comunicação interdomínios futura via Domain Events.
7.  Scripts SQL idempotentes obrigatórios para alterações estruturais.
8.  Todas as decisões devem considerar impacto em API, Backend e
    Frontend.

------------------------------------------------------------------------

## 2. OrgContext -- Regra Estrutural Obrigatória

### 2.1 Escopo mínimo obrigatório

Toda operação deve possuir:

-   UsuarioId
-   OrganizacaoId
-   UnidadeId

Nenhum módulo pode aceitar OrganizacaoId diretamente por parâmetro sem
validação via OrgContext.

### 2.2 Segurança

-   Se header X-Organizacao-Id for informado e o usuário não possuir
    acesso, retornar 403.
-   Exportações devem validar HasAccessToOrganizacaoAsync.
-   Nunca confiar exclusivamente em claims.

### 2.3 Performance

-   Implementar cache por request usando HttpContext.Items.
-   Índice obrigatório:

CREATE INDEX IX_LotacaoUsuario_Usuario_Ativa_Principal ON
dbo.LotacaoUsuario (IdUsuario, Ativa, Principal) INCLUDE
(IdUnidadeOrganizacional);

------------------------------------------------------------------------

## 3. Estratégia Oficial EF Core vs Dapper

### EF Core

Usar para: - Escrita - Controle transacional - Agregados de domínio

### Dapper

Usar para: - Queries de leitura complexas - Exportações - Relatórios

Regra: Nunca misturar EF e Dapper na mesma transação sem controle
explícito.

------------------------------------------------------------------------

## 4. Auditoria da Estratégia de Exportações (CSV/PDF)

### 4.1 Regras obrigatórias

1.  Exportação nunca recebe OrganizacaoId direto.
2.  Sempre usar OrgContext.
3.  Registrar auditoria de exportação.
4.  Aplicar paginação ou streaming para grandes volumes.
5.  Respeitar LGPD.
6.  Garantir que exportações não violem isolamento multiempresa.

### 4.2 Estrutura recomendada

-   Application/Exports
-   Infra/Exports
-   Registro em tabela ExportacaoLog

------------------------------------------------------------------------

## 5. Blueprint Técnico -- Módulo Estoque

### 5.1 Entidades mínimas

-   Produto
-   Lote
-   Estoque
-   MovimentacaoEstoque
-   ReservaEstoque

Todas contendo OrganizacaoId NOT NULL e UnidadeId NOT NULL.

### 5.2 Regras obrigatórias

-   Não permitir movimentação sem OrgContext válido.
-   Controlar concorrência (RowVersion ou lock explícito).
-   Toda movimentação gera histórico.
-   Reserva deve possuir expiração (TTL).
-   API deve expor endpoints adequados para Backend e Frontend
    omnichannel.

### 5.3 Índices mínimos

-   Estoque (ProdutoId, UnidadeId)
-   Lote (ProdutoId, DataValidade)
-   MovimentacaoEstoque (UnidadeId, DataMovimentacao)

------------------------------------------------------------------------

## 6. Roadmap Técnico -- Próximos 90 Dias

### Fase 1 -- Consolidação (Semanas 1--3)

-   Hardening final do OrgContext
-   Middleware global de contexto
-   Índices de performance
-   Política formal de transações
-   Revisão de impacto nas três camadas (API, Backend, Frontend)

### Fase 2 -- Estoque Básico (Semanas 4--8)

-   Implementação do módulo Estoque
-   Testes de integração completos
-   Auditoria de movimentações
-   Exposição consistente para Backend e Operação

### Fase 3 -- Observabilidade (Semanas 9--12)

-   Logs estruturados por domínio
-   Auditoria de exportações
-   Introdução de Domain Events + Outbox
-   Avaliação de impacto omnichannel

------------------------------------------------------------------------

# Prompt Oficial para Uso no Codex

Você está atuando como Arquiteto de Software Sênior responsável pela
evolução do ProformaFarmERP.

Contexto fundamental:

O ProformaFarmERP NÃO é um produto API-only. Ele possui três camadas
oficiais: - API de domínio e integração - Painel backend (retaguarda
administrativa) - Frontend de operação omnichannel

Qualquer decisão deve considerar impacto nas três camadas.

Regras obrigatórias:

1.  Respeitar estritamente as Diretrizes Arquiteturais Obrigatórias.
2.  Nunca implementar código que ignore OrgContext.
3.  Garantir OrganizacaoId e UnidadeId em todas entidades transacionais.
4.  Separar leitura (Dapper) e escrita (EF Core).
5.  Implementar índices necessários em qualquer nova entidade.
6.  Sempre validar segurança multiempresa.
7.  Criar testes de integração para qualquer novo módulo.
8.  Avaliar impacto funcional para Backend e Frontend omnichannel.

Antes de sugerir código:

-   Validar impacto arquitetural.
-   Verificar integridade de dados.
-   Avaliar concorrência e performance.
-   Sugerir melhorias estruturais quando necessário.

Objetivo final:

Construir uma plataforma ERP farmacêutica omnichannel, escalável, segura
e governável, mantendo coerência arquitetural e integridade multiempresa
em todas as camadas do produto.
