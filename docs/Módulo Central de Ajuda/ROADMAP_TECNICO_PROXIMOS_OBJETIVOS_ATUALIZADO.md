# ProformaFarmERP — Roadmap Técnico (Próximos Objetivos)

Este roadmap consolida os próximos objetivos técnicos e inclui a estratégia de entrega “MVP minimalista” para o módulo Central de Ajuda, reduzindo atrito na primeira versão e permitindo evolução incremental.

## Linha mestra (ordem de prioridade)

1. Hardening do OrgContext como pilar estrutural definitivo (global enforcement, segurança e performance).
2. Estoque Básico (núcleo transacional que valida contexto, concorrência e integridade).
3. Central de Ajuda (Help Center) com MVP minimalista + evolução opcional para IA.
4. Observabilidade e eventos (Domain Events + Outbox) para sustentar desacoplamento (Financeiro/Fiscal/SNGPC/Integrações).

## Objetivo 1 — OrgContext (Hardening final)

Entregas obrigatórias:
- Cache por request (HttpContext.Items) para reduzir queries repetidas.
- Middleware global de OrgContext (bloqueio de requests sem contexto).
- Header X-Organizacao-Id: se informado e sem acesso => 403 (sem retorno silencioso).
- Expansão do contexto (quando aplicável): OrganizacaoId + UnidadeId (e CentroCustoId opcional).
- Testes de integração: 401/403 e cenários de múltiplas lotações.

Critério de pronto:
- Nenhum endpoint transacional executa sem OrgContext válido.
- Performance estável (sem multiplicação de queries por request).

## Objetivo 2 — Estoque Básico (núcleo transacional)

Fase inicial (sem WMS):
- Produto
- Lote
- Estoque
- MovimentacaoEstoque
- ReservaEstoque (TTL)

Regras obrigatórias:
- OrganizacaoId e UnidadeId NOT NULL em todas tabelas transacionais.
- Controle de concorrência (RowVersion ou lock explícito).
- Auditoria/histórico de movimentações.
- Separação: EF para escrita, Dapper para consultas e relatórios.

Critério de pronto:
- Fluxos mínimos: entrada, saída, ajuste, consulta e reserva com expiração.
- Testes de integração cobrindo concorrência e isolamento por unidade.

## Objetivo 3 — Central de Ajuda (Help Center) — MVP minimalista

Decisão arquitetural:
- O ProformaFarmERP não é API-only; o Help Center deve atender:
  - API (conteúdo e busca)
  - Painel backend (administração do conteúdo)
  - Frontend omnichannel (ajuda ao operador)

Entrega MVP minimalista (reduz atrito):
- SQL MVP: categorias, artigos, versões, logs mínimos.
- Busca lexical simples (LIKE) com paginação e limites.
- Admin básico: CRUD + publicar/arquivar.
- Público: categorias, busca, artigo por slug, feedback.
- Telemetria mínima: SearchLog, ArticleView, Feedback.

Scripts opcionais (fase seguinte):
- Tags.
- Contexto (módulo/tela/feature flag).
- FAQ.
- FULLTEXT.
- IA (embeddings + RAG + logs).

Critério de pronto (MVP):
- Backend consegue criar/publicar conteúdo.
- Frontend consegue buscar e abrir artigos.
- Logs mínimos gerados e consultáveis.

Evolução IA (fase 2):
- Implementar embeddings e busca híbrida (lexical + semântica).
- Assistente RAG respondendo apenas com base em artigos aprovados (citações obrigatórias).
- Feature flag para ligar/desligar IA.

## Objetivo 4 — Observabilidade e eventos (plataforma)

- Logs estruturados por domínio e CorrelationId.
- Auditoria ampliada (inclui exportações e Help Center).
- Domain Events + Outbox (primeiro para vendas/financeiro; depois fiscal e SNGPC).
- Métricas de produto: buscas sem resultado, artigos mais acessados, feedback.

## Sugestão adicional (alto ROI, baixo risco)

Adicionar “Ajuda Contextual” como componente de UI padrão:
- Botão “?” na retaguarda e operação.
- Drawer lateral com artigos sugeridos por módulo (fase 1).
- Evoluir para módulo+tela (fase 2, quando Contexto estiver habilitado).

## Dependências resumidas

- OrgContext hardening é dependência para módulos transacionais (Estoque/Comercial) e para auditoria (Help Center).
- Estoque básico é dependência para omnichannel real (reserva, picking futuro).
- Help Center MVP pode ser implementado em paralelo ao Estoque, mas deve respeitar padrões e logs.
