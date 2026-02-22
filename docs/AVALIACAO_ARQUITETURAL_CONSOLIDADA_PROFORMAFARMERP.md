# PROFORMA FARM ERP

# Avaliação Arquitetural Consolidada e Diretrizes Estratégicas

------------------------------------------------------------------------

## 1. Estado Atual do Projeto

O ProformaFarmERP evoluiu de uma base de autenticação simples para uma
plataforma ERP farmacêutica modular com os seguintes componentes
consolidados:

-   Estrutura Organizacional modelada e aplicada no banco
-   OrgContext funcional em runtime
-   Scripts SQL idempotentes
-   Índices estruturais definidos
-   Seed determinístico
-   Endpoints organizacionais implementados com Dapper
-   Suíte de testes de integração validada

O projeto encontra-se em nível arquitetural intermediário-avançado, com
fundação sólida para expansão segura.

------------------------------------------------------------------------

## 2. Avaliação Arquitetural Geral

### 2.1 Separação de Domínios

Os seguintes boundaries estão explicitamente definidos:

-   Auth / Identidade
-   Organização
-   Estoque
-   Comercial
-   Fiscal
-   Financeiro
-   Logística
-   Qualidade / SNGPC

Impactos positivos:

-   Redução de acoplamento estrutural
-   Evolução modular controlada
-   Base adequada para modular monolith evolutivo
-   Facilita futura extração de microserviços

------------------------------------------------------------------------

### 2.2 Estrutura Organizacional

Características consolidadas:

-   Hierarquia real via UnidadeOrganizacional
-   Lotação com vigência temporal
-   Restrição de lotação principal ativa
-   Delete restritivo para preservar integridade
-   Unicidade por organização

A modelagem está alinhada com cenários enterprise multiempresa.

------------------------------------------------------------------------

### 2.3 OrgContext

O OrgContext:

-   Resolve usuário autenticado via claims
-   Valida acesso organizacional contra banco de dados
-   Suporta seleção opcional via header
-   Não depende exclusivamente de claims para escopo

Isso estabelece controle real de multiempresa em runtime.

------------------------------------------------------------------------

## 3. Riscos Arquiteturais Identificados

### 3.1 Ausência de Cache por Request no OrgContext

Problema:

Cada chamada a GetCurrentOrganizacaoIdAsync executa consulta no banco.

Impacto potencial:

-   Multiplicação de queries por request
-   Aumento de latência
-   Redução de escalabilidade sob carga

Diretriz:

Implementar cache por request utilizando HttpContext.Items.

------------------------------------------------------------------------

### 3.2 Tratamento do Header X-Organizacao-Id

Situação atual:

Se o header informado não corresponder a uma lotação ativa, o método
retorna null.

Risco:

-   Pode mascarar tentativa de acesso indevido
-   Pode gerar comportamento inconsistente nos controllers

Diretriz:

Se o header for informado e o usuário não possuir acesso à organização,
retornar explicitamente 403 Forbidden.

------------------------------------------------------------------------

### 3.3 Escopo Incompleto (Ausência de UnidadeOperacional)

Atualmente o OrgContext resolve:

-   UsuarioId
-   OrganizacaoId

Módulos transacionais exigirão:

-   UnidadeOrganizacionalId
-   CentroCustoId (quando aplicável)

Diretriz:

Expandir IOrgContext para incluir UnidadeId e, opcionalmente,
CentroCustoId.

------------------------------------------------------------------------

### 3.4 Falta de Enforcement Global de Contexto

Risco:

Novos módulos podem ignorar o escopo organizacional.

Diretriz:

Criar middleware global que:

-   Resolva o OrgContext por request
-   Injete o contexto via DI scoped
-   Bloqueie a execução se o contexto for inválido

------------------------------------------------------------------------

## 4. Avaliação de Banco de Dados

### 4.1 Pontos Positivos

-   Scripts idempotentes
-   Índices de integridade
-   Delete restritivo
-   Seed determinístico

### 4.2 Índice Recomendado

CREATE INDEX IX_LotacaoUsuario_Usuario_Ativa_Principal\
ON dbo.LotacaoUsuario (IdUsuario, Ativa, Principal)\
INCLUDE (IdUnidadeOrganizacional);

Objetivo:

Evitar table scan conforme crescimento da base.

------------------------------------------------------------------------

## 5. Nível de Maturidade Atual

Arquitetura atual caracteriza-se como:

-   Modular Monolith consistente
-   Multiempresa validado
-   Escopo organizacional funcional
-   Persistência sólida
-   Testes de integração reais

O projeto encontra-se no ponto crítico onde o fortalecimento do
OrgContext definirá a escalabilidade futura.

------------------------------------------------------------------------

## 6. Diretrizes Estratégicas

### Fase Imediata -- Hardening

1.  Implementar cache por request no OrgContext\
2.  Expandir OrgContext para incluir UnidadeId\
3.  Implementar middleware global de contexto\
4.  Criar teste negativo para header inválido (403)\
5.  Criar índice otimizado para LotacaoUsuario

### Próximo Módulo Recomendado: Estoque Básico

Entidades mínimas:

-   Produto\
-   Lote\
-   Estoque\
-   MovimentacaoEstoque\
-   ReservaEstoque

Todas contendo OrganizacaoId NOT NULL e UnidadeId NOT NULL.

------------------------------------------------------------------------

## 7. Diretriz Arquitetural Definitiva

-   Arquitetura: Modular Monolith Evolutivo\
-   Isolamento: Por domínio\
-   Escopo: OrgContext obrigatório\
-   Banco: Single Database com segregação lógica\
-   Integração futura: Event-driven

------------------------------------------------------------------------

## 8. Conclusão

O projeto encontra-se estruturalmente correto, conceitualmente alinhado
e pronto para expansão controlada.

O próximo passo obrigatório é consolidar o OrgContext como pilar
estrutural definitivo antes da evolução para módulos transacionais.
