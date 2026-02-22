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

## 4) Criterios de pronto para PDF

- endpoint protegido e com escopo;
- layout validado com area de negocio;
- testes de integracao para status e tipo de arquivo;
- guideline documentado para novos relatorios.

## 5) Governanca de evolucao

- atualizar este documento a cada incremento de exportacao;
- refletir status executivo no `docs/README.md`;
- detalhar implementacao tecnica no `docs/RESUMO_TECNICO_EVOLUCAO_PROFORMAFARMERP.md`.
