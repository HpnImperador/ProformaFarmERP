# ProformaFarm ERP

Documento de apresentacao institucional e tecnica do projeto ProformaFarm ERP.
Este arquivo e a referencia de alto nivel para stakeholders, times tecnicos e futuras apresentacoes oficiais.

## 1) O que e o ProformaFarm ERP

O ProformaFarm ERP e uma plataforma ERP farmaceutica omnichannel, projetada para operar com:

- compliance regulatoria (incluindo trilha para SNGPC);
- governanca financeira e fiscal;
- estrutura multiempresa e multiunidade;
- arquitetura evolutiva para escala operacional.

Nao e apenas um sistema operacional de farmacia. O objetivo e consolidar uma plataforma de gestao integrada, modular e preparada para crescimento.

## 2) Visao de produto

### Proposta de valor

- Unificar operacao comercial, estoque, fiscal e financeiro em uma base coerente.
- Garantir rastreabilidade ponta a ponta (organizacao, unidade, produto, lote, reserva, movimentacao).
- Reduzir risco operacional com regras de escopo organizacional no runtime.
- Sustentar crescimento por incrementos sem refatoracao estrutural ampla.

### Publico-alvo

- redes de farmacias;
- operacoes com multiplas filiais/unidades;
- times que precisam de controle organizacional + governanca de dados + trilha de auditoria.

## 3) Arquitetura adotada

### Diretriz principal

- Modular Monolith Evolutivo.

### Escopo de produto (oficial)

- O ProformaFarmERP **nao** e um produto API-only.
- O escopo oficial contempla tres camadas integradas:
  - API de dominio e integracao;
  - painel backend (retaguarda administrativa);
  - frontend de operacao omnichannel.

### Principios

- separacao clara de dominios;
- contratos de API padronizados;
- persistencia orientada a integridade e performance;
- evolucao incremental com testes de integracao reais.

### Dominios mapeados

- Auth / Identidade
- Organizacao
- Estoque
- Comercial
- Fiscal
- Financeiro
- Logistica
- Qualidade / SNGPC

## 4) Estado atual do projeto

Status de referencia desta versao do documento:

- **Estado geral:** Base arquitetural consolidada e em expansao funcional.
- **Maturidade atual:** Bloco Organizacional consolidado + bloco inicial de Estoque implementado e validado.
- **Qualidade:** Suite de testes de integracao ativa, com cobertura de fluxos criticos e seguranca.

### 4.1 Entregas ja consolidadas

- modelagem de Estrutura Organizacional no dominio e banco;
- scripts SQL idempotentes de estrutura, seed e indices;
- `OrgContext` para resolucao de escopo organizacional em runtime;
- enforcement de contexto com bloqueios `403` para escopo invalido/sem acesso;
- endpoints organizacionais (`estrutura`, `arvore`, `contexto`);
- modulo inicial de estoque:
  - saldos;
  - reservas (ativas, historico, detalhe, operacoes, expiracao);
  - movimentacoes (entrada, saida, ajuste, historico);
  - exportacoes CSV operacionais.

### 4.2 Exportacoes CSV implementadas

- `GET /api/estoque/saldos/exportar-csv`
- `GET /api/estoque/reservas/ativas/exportar-csv`
- `GET /api/estoque/reservas/exportar-csv`
- `GET /api/estoque/movimentacoes/exportar-csv`

### 4.2.1 Exportacao PDF piloto implementada

- `GET /api/estoque/saldos/exportar-pdf`
- `GET /api/estoque/reservas/exportar-pdf`
- `GET /api/estoque/movimentacoes/exportar-pdf`
- Motor de renderizacao: `QuestPDF` (layout paginado com tabela).

Padrao aplicado:

- UTF-8 BOM para compatibilidade com planilhas;
- filtros operacionais por escopo;
- validacoes de entrada (limite e periodo);
- protecao por autenticacao e OrgContext.

### 4.3 Situacao operacional

- banco de desenvolvimento com scripts aplicados e validados;
- seeds de homologacao disponiveis;
- endpoints principais funcionando com login real;
- testes de integracao executando com sucesso para incrementos recentes.
- painel web inicial com login, consultas de Organizacao/Estoque e exportacoes CSV/PDF para validacao funcional em navegador.
- painel evoluido com cliente modular para consultas operacionais e visualizacao tabular de estoque.
- painel com operacoes transacionais de estoque e reservas para validacao funcional end-to-end.
- contratos de integração painel/API cobertos por testes para `ApiResponse` e headers `X-Export-*`.
- pipeline CI/CD com estágios de `build`, testes de integração e E2E Playwright do painel, com artefatos de execução.

### 4.4 Incremento Atual: Outbox + Domain Events

Foi concluída a primeira etapa de eventos de domínio com padrão Outbox, como pré-requisito para evolução de integrações enterprise:

- persistência transacional de eventos no mesmo `SaveChanges` da operação de negócio;
- processamento assíncrono com `BackgroundService`, lock, retry e backoff;
- idempotência por `EventId` + `HandlerName`;
- prova de vida validada (`Hello Event`) com processamento ponta a ponta;
- `EstoqueBaixoDomainEvent` implementado como primeiro evento de negócio real do pipeline Outbox.

Validação técnica concluída:
- testes de integração de Outbox executados com sucesso (5/5).

Referências:
- `docs/sql/005_core_outbox.sql`
- `docs/architecture/outbox-implementation-notes.md`

## 5) Indicadores de Progresso (KPIs)

Data de referencia desta fotografia: **22 de fevereiro de 2026**.

| KPI | Objetivo | Status atual |
|---|---|---|
| Cobertura de dominios core implantados | Consolidar base Organizacao + Estoque para evolucao dos modulos transacionais | **Em andamento (base pronta e operacional)** |
| Scripts SQL idempotentes aplicados | Garantir reproducao de ambiente sem retrabalho manual | **Concluido para blocos atuais (001-004)** |
| Endpoints de estoque com autenticacao e OrgContext | Garantir seguranca e escopo organizacional em runtime | **Concluido no bloco atual** |
| Endpoints com exportacao CSV operacional | Habilitar extracao rapida para operacao e analise | **Concluido para saldos, reservas ativas, reservas historico e movimentacoes** |
| Testes de integracao dos incrementos recentes | Reduzir regressao em evolucoes incrementais | **Concluido (suites recentes verdes)** |
| Padronizacao tecnica de exportacao | Reduzir duplicacao e facilitar reuso em novos dominios | **Concluido neste incremento (CsvExportService)** |
| Prontidao para proximo modulo | Avancar para incremento de dominio com base estavel | **Pronto para proxima frente funcional** |

### 5.1 Leitura executiva dos KPIs

- O projeto saiu da fase de base arquitetural para uma fase de crescimento funcional com governanca.
- O risco de regressao nos fluxos entregues esta controlado por testes de integracao e padrao de API.
- A padronizacao de exportacao CSV reduz custo de evolucao para Comercial/Fiscal.

### 5.2 Acordo de padronizacao de exportacoes

- CSV: todos os novos dominios devem adotar `ICsvExportService` como padrao unico.
- PDF: evolucao sera incremental, iniciando por POC tecnica e template padrao.
- Referencia tecnica oficial: `docs/ESTRATEGIA_EXPORTACOES_CSV_PDF.md`
- Contrato padrao de exportacao deve cobrir API + painel backend + frontend (headers de metadados + nome de arquivo padrao).
- Contrato padrao ja aplicado no piloto PDF para manter comportamento consistente entre formatos.

## 6) Roadmap executivo (macro)

### Fase atual (em andamento)

- consolidacao do bloco de Estoque e operacao omnichannel inicial.

### Proximas fases

1. hardening e padronizacao tecnica:
   - consolidar utilitarios compartilhados (ex.: exportacao CSV);
   - ampliar cobertura de testes para cenarios de borda.
2. evolucao operacional:
   - qualidade e bloqueios por lote;
   - integracoes e workflow.
3. governanca:
   - BI, trilha de auditoria avancada, controladoria.

## 7) Como este documento sera usado

Este `README.md` deve ser a camada de comunicacao executiva do projeto:

- onboarding rapido de novos participantes;
- base para apresentacao institucional;
- referencia de status para gestao.

## 8) Politica de atualizacao

Sempre que houver incremento relevante:

1. atualizar este arquivo com visao executiva e estado atual;
2. manter detalhes tecnicos e trilha de implementacao em `docs/RESUMO_TECNICO_EVOLUCAO_PROFORMAFARMERP.md`;
3. garantir consistencia entre ambos os documentos.

## 9) Referencias complementares

- `docs/RESUMO_TECNICO_EVOLUCAO_PROFORMAFARMERP.md`
- `docs/AVALIACAO_ARQUITETURAL_CONSOLIDADA_PROFORMAFARMERP.md`
- `docs/PROFORMA_MASTER_ARCH.md`
- `docs/DOMAINS_MAP.md`
- `docs/ESTRATEGIA_EXPORTACOES_CSV_PDF.md`
- `docs/INDICES_ESTRUTURA_ORGANIZACIONAL.md`
- `docs/sql/001_estrutura_organizacional.sql`
- `docs/sql/002_seed_estrutura_organizacional.sql`
- `docs/sql/003_idx_lotacaousuario_orgcontext.sql`
- `docs/sql/004_estoque_basico.sql`
- `docs/sql/005_core_outbox.sql`
- `docs/architecture/outbox-implementation-notes.md`
- `docs/architecture/outbox-merge-checklist.md`


