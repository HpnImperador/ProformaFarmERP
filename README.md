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

## 5) Roadmap executivo (macro)

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

## 6) Como este documento sera usado

Este `README.md` deve ser a camada de comunicacao executiva do projeto:

- onboarding rapido de novos participantes;
- base para apresentacao institucional;
- referencia de status para gestao.

## 7) Politica de atualizacao

Sempre que houver incremento relevante:

1. atualizar este arquivo com visao executiva e estado atual;
2. manter detalhes tecnicos e trilha de implementacao em `docs/RESUMO_TECNICO_EVOLUCAO_PROFORMAFARMERP.md`;
3. garantir consistencia entre ambos os documentos.

## 8) Referencias complementares

- `docs/RESUMO_TECNICO_EVOLUCAO_PROFORMAFARMERP.md`
- `docs/AVALIACAO_ARQUITETURAL_CONSOLIDADA_PROFORMAFARMERP.md`
- `docs/PROFORMA_MASTER_ARCH.md`
- `docs/DOMAINS_MAP.md`
- `docs/INDICES_ESTRUTURA_ORGANIZACIONAL.md`
- `docs/sql/001_estrutura_organizacional.sql`
- `docs/sql/002_seed_estrutura_organizacional.sql`
- `docs/sql/003_idx_lotacaousuario_orgcontext.sql`
- `docs/sql/004_estoque_basico.sql`
