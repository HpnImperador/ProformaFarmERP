# Estrategia de Exportacoes (CSV e PDF)

## Objetivo

Definir um padrao unico para exportacoes no ProformaFarm ERP, garantindo consistencia entre dominios e evolucao controlada para formato PDF.

## 1) Padrao oficial de CSV (vigente)

A partir deste ponto, todo novo dominio deve reutilizar o padrao implementado com `ICsvExportService`/`CsvExportService`.

### Regras obrigatorias

1. Endpoints de exportacao devem seguir convencao:
   - `GET /api/{dominio}/{recurso}/exportar-csv`
2. Codificacao:
   - UTF-8 com BOM.
3. Seguranca:
   - endpoint autenticado;
   - escopo resolvido por `OrgContext` quando aplicavel.
4. Controles operacionais:
   - parametro `limite` com validacao;
   - filtros de escopo e periodo quando fizer sentido.
5. Formato:
   - cabecalhos deterministas;
   - datas em ISO-8601;
   - numericos com cultura invariante;
   - escaping CSV centralizado via servico.

### Contrato padrao de resposta (Backend + Frontend)

Todo endpoint de exportacao deve retornar, alem do arquivo, os metadados abaixo:

- `Content-Type`: `text/csv; charset=utf-8`
- `Content-Disposition`: attachment com nome padrao do arquivo
- `X-Export-Format`: `csv`
- `X-Export-Resource`: recurso exportado (ex.: `saldos`, `reservas`, `movimentacoes`)
- `X-Export-GeneratedAtUtc`: timestamp UTC de geracao (ISO-8601)
- `X-Export-FileName`: nome final do arquivo
- `Access-Control-Expose-Headers`: incluir os headers de exportacao para leitura no frontend

Padrao de nome de arquivo:

- `{recurso}_{yyyyMMdd_HHmmss}.csv`

Uso no frontend/painel:

1. Ler `X-Export-FileName` para rotulo no UI e download assistido.
2. Exibir `X-Export-GeneratedAtUtc` como data/hora da geracao.
3. Persistir filtros aplicados no estado da tela para auditoria operacional.
4. Tratar respostas de validacao (`400`) e escopo (`403`) com mensagens orientadas ao usuario.

### Implementacao de referencia

- Interface: `ProformaFarm.Application/Interfaces/Export/ICsvExportService.cs`
- Implementacao: `ProformaFarm.Application/Services/Export/CsvExportService.cs`
- Registro DI: `ProformaFarm/Program.cs`
- Dominio de referencia: `EstoqueController` (todos os endpoints de exportacao CSV).

## 2) Estudo tecnico para exportacoes PDF

## 2.1 Requisitos funcionais esperados

- exportacao de relatorios operacionais em layout de apresentacao;
- cabecalho com identidade visual da empresa;
- filtros aplicados e data/hora de geracao;
- paginação e quebra de tabela adequada;
- opcional de assinatura digital em fase futura.

## 2.2 Abordagens avaliadas

1. Renderizacao por biblioteca .NET nativa de PDF (ex.: QuestPDF)
   - pontos fortes: controle programatico, boa produtividade, sem dependencia de browser.
   - riscos: curva de desenho de layout para relatorios mais complexos.
2. Conversao HTML -> PDF (engine externa/headless)
   - pontos fortes: reaproveita templates HTML/CSS.
   - riscos: dependencia operacional maior (engine/binario), variacao de render entre ambientes.
3. Biblioteca enterprise comercial
   - pontos fortes: recursos avancados e suporte oficial.
   - riscos: custo e lock-in de fornecedor.

## 2.3 Recomendacao atual

Iniciar com **POC de renderizacao .NET (QuestPDF)** para o primeiro relatorio PDF operacional.

Motivos:

- menor complexidade operacional;
- bom fit com stack atual;
- facilita padronizacao de componente de layout para multiplos dominios.

## 3) Plano de adocao (incremental)

1. Criar contrato de aplicacao para exportacao PDF (`IPdfExportService`).
2. Implementar POC com 1 relatorio de estoque (ex.: saldos).
3. Validar:
   - tamanho do arquivo;
   - tempo medio de geracao;
   - legibilidade em desktop e mobile.
4. Padronizar template (cabecalho, rodape, tabela, metadados).
5. Reutilizar em Comercial/Fiscal.

## 3.1 Status da POC PDF (estoque)

POC inicial implementada:

- Endpoint piloto:
  - `GET /api/estoque/saldos/exportar-pdf`
  - `GET /api/estoque/reservas/exportar-pdf`
  - `GET /api/estoque/movimentacoes/exportar-pdf`
- Servico:
  - `IPdfExportService` + `PdfExportService`
  - engine dedicado: `QuestPDF`
- Contrato de metadados de exportacao aplicado tambem no PDF:
  - `X-Export-Format`
  - `X-Export-Resource`
  - `X-Export-GeneratedAtUtc`
  - `X-Export-FileName`

Resultado atual:
- PDF com layout paginado e tabela (nao mais geracao manual de bytes PDF).

## 4) Criterios de pronto para PDF

- endpoint protegido e com escopo;
- layout validado com area de negocio;
- testes de integracao para status e tipo de arquivo;
- guideline documentado para novos relatorios.

## 5) Governanca de evolucao

- atualizar este documento a cada incremento de exportacao;
- refletir status executivo no `docs/README.md`;
- detalhar implementacao tecnica no `docs/RESUMO_TECNICO_EVOLUCAO_PROFORMAFARMERP.md`.
