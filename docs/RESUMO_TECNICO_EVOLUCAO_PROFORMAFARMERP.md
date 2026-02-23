# Resumo Tecnico de Evolucao - ProformaFarmERP

## Contexto
Este documento consolida a evolucao tecnica recente do ProformaFarmERP, com foco em arquitetura e modelagem da Estrutura Organizacional, sem registrar historico de correcao.

## 1) Evolucao de Arquitetura
- Consolidacao da direcao de plataforma ERP farmaceutica omnichannel, com separacao de dominios.
- Estruturacao explicita de boundaries entre blocos core:
  - Auth/Identidade
  - Organizacao
  - Estoque/Produto/Lote
  - Comercial
  - Fiscal
  - Financeiro
  - Logistica
  - Qualidade/SNGPC
- Preservacao do principio de desacoplamento entre contextos funcionais.

## 2) Evolucao de Modelagem de Dominio (Estrutura Organizacional)
Foram incorporadas entidades de organizacao na camada de dominio:
- `Organizacao`
- `UnidadeOrganizacional` (hierarquia)
- `CentroCusto`
- `UnidadeCentroCusto` (vinculo N:N)
- `Cargo`
- `LotacaoUsuario`

Principais capacidades modeladas:
- Multiempresa por organizacao.
- Hierarquia de unidades (matriz/filial/departamento/setor).
- Vinculacao de centro de custo por unidade.
- Lotacao de usuario por unidade e cargo com vigencia temporal.
- Regra de lotacao principal ativa por usuario.

## 3) Evolucao de Persistencia (EF Core)
`ProformaFarmDbContext` foi estendido para incluir o modulo organizacional:
- Novos `DbSet<>` para as entidades de estrutura.
- Mapeamento de tabelas, chaves e relacoes.
- Restricoes de unicidade por organizacao (codigos de unidade, centro de custo e cargo).
- Relacoes com delete restritivo para preservar integridade.
- Indice filtrado para garantir apenas uma lotacao principal ativa por usuario.

## 4) Evolucao de Banco de Dados (SQL Server)
### Script estrutural
- Criado e validado o script idempotente:
  - `docs/sql/001_estrutura_organizacional.sql`
- Escopo do script:
  - Criacao de tabelas da estrutura organizacional.
  - Constraints de integridade (vigencia, referencia, unicidade).
  - Indices operacionais e de unicidade.
- Aplicacao realizada no banco de desenvolvimento e validada com sucesso.

### Estrategia de indices
- Documento tecnico criado:
  - `docs/INDICES_ESTRUTURA_ORGANIZACIONAL.md`
- Conteudo:
  - Indices de integridade.
  - Indices de leitura operacional.
  - Diretrizes de manutencao e evolucao para escala.

### Seed funcional
- Criado e aplicado seed idempotente:
  - `docs/sql/002_seed_estrutura_organizacional.sql`
- Dados de homologacao incluidos:
  - Organizacao
  - Matriz e filial
  - Centro de custo
  - Cargo
  - Lotacao principal ativa

### Otimizacao para OrgContext
- Criado e aplicado indice adicional de consulta de contexto:
  - `docs/sql/003_idx_lotacaousuario_orgcontext.sql`
- Indice criado:
  - `IX_LotacaoUsuario_Usuario_Ativa_Principal`

## 5) Evolucao de API
Foi implementado um controlador para leitura do contexto organizacional:
- `GET /api/organizacao/estrutura`
  - Retorna organizacao, unidades e lotacoes ativas.
- `GET /api/organizacao/estrutura/arvore`
  - Retorna estrutura hierarquica pronta para consumo de frontend (raizes/filhos), incluindo lotacoes por unidade.
- `GET /api/organizacao/contexto`
  - Retorna contexto atual resolvido (`IdUsuario`, `IdOrganizacao`, `IdUnidade`).

Caracteristicas:
- Endpoints protegidos por autenticacao (`[Authorize]`).
- Consulta orientada a leitura via Dapper.
- Contrato padronizado com `ApiResponse`.

## 6) Evolucao de Contexto Organizacional (OrgContext)
Foi introduzido e fortalecido um contexto organizacional de execucao para o usuario autenticado:
- Interface de aplicacao:
  - `IOrgContext`
- Implementacao em infraestrutura:
  - `OrgContext`

Capacidades introduzidas:
- Resolucao de usuario atual a partir das claims do token.
- Resolucao de organizacao atual com base em lotacao ativa do usuario.
- Resolucao de unidade atual (`UnidadeId`) com base em lotacao ativa.
- Suporte a header opcional `X-Organizacao-Id` para selecao de contexto quando aplicavel.
- Cache por request usando `HttpContext.Items` para reduzir repeticao de queries.

Integracao na API:
- `OrganizacaoController` utiliza `OrgContext` para resolver escopo automaticamente quando `idOrganizacao` nao e informado.
- Mantido o contrato de consulta explicita por `idOrganizacao` para preservar comportamento funcional esperado.

## 7) Evolucao de Enforcement de Contexto
Foi incorporado enforcement global (escopo opt-in por rota de organizacao):
- Middleware:
  - `OrgContextEnforcementMiddleware`
- Regra aplicada em `/api/organizacao`:
  - Header `X-Organizacao-Id` invalido -> `403 ORG_HEADER_INVALID`
  - Header informado sem acesso -> `403 ORG_FORBIDDEN`

Esse enforcement reduz ambiguidades de escopo e padroniza seguranca organizacional no runtime.

## 8) Evolucao de Qualidade (Testes)
Foi ampliada a cobertura de integracao na suite `ProformaFarm.Application.Tests` para os endpoints organizacionais e regras de contexto:
- Validacao de seguranca (`401` sem token).
- Validacao de sucesso (`200` com token e organizacao valida).
- Validacao funcional (`404` para organizacao inexistente).
- Validacao da coerencia hierarquica no endpoint de arvore.
- Validacao de header de organizacao invalido (`403 ORG_HEADER_INVALID`).
- Validacao de header sem acesso organizacional (`403 ORG_FORBIDDEN`).
- Validacao do endpoint de contexto organizacional (`/api/organizacao/contexto`).

Abordagem de teste adotada:
- Login real na API para obtencao de token.
- Setup deterministico/idempotente de dados de teste via SQL.
- Independencia de ordenacao fixa (asserts por identificadores/codigos).

Status de validacao:
- Suite `ProformaFarm.Application.Tests` executada com sucesso apos os incrementos (17 testes aprovados).

## 9) Evolucao de Dominio e Persistencia (Estoque Basico)
Foi iniciada a fundacao do modulo de estoque basico no dominio e na persistencia:
- Entidades de dominio incorporadas:
  - `Produto`
  - `Lote`
  - `Estoque`
  - `MovimentacaoEstoque`
  - `ReservaEstoque`
- `ProformaFarmDbContext` expandido com `DbSet<>` e mapeamentos para o bloco de estoque.
- Relacionamentos estruturados para operar em escopo organizacional e por unidade.
- Indices operacionais adicionados para consultas frequentes de saldo, lote, movimentacao e reservas.

## 10) Evolucao de Banco (Estoque Basico)
Foi criado e aplicado script evolutivo/idempotente para o modulo:
- `docs/sql/004_estoque_basico.sql`

Escopo entregue:
- Criacao/adaptacao de `Produto` e `Estoque` considerando compatibilidade com estrutura legada existente.
- Criacao de tabelas novas:
  - `Lote`
  - `MovimentacaoEstoque`
  - `ReservaEstoque`
- Criacao dos indices:
  - `IX_Produto_Org_Codigo`
  - `IX_Lote_Org_Produto_Numero`
  - `IX_Estoque_Org_Unidade_Produto_Lote`
  - `IX_MovimentacaoEstoque_Org_Unidade_Data`
  - `IX_ReservaEstoque_Org_Status_Expira`

Status:
- Script aplicado com sucesso no banco de desenvolvimento.
- Tabelas e indices validados no catalogo SQL Server.

## 11) Resultado de Evolucao
Com os incrementos implementados, o ProformaFarmERP avancou de uma base de autenticacao para um bloco funcional de Estrutura Organizacional com contexto e enforcement em runtime, cobrindo:
- Modelo de dominio
- Persistencia
- Banco aplicado
- API de consulta
- Contexto organizacional em runtime
- Enforcement de escopo organizacional
- Seed de homologacao
- Testes de integracao
- Fundacao do modulo de estoque basico (dominio, persistencia e banco)

Este bloco estabelece fundacao para os proximos modulos de escopo organizacional (estoque, comercial, fiscal, financeiro e logistica), com trilha de crescimento coerente com a arquitetura definida.

## 12) Incremento de API (Estoque)
Foi adicionado um bloco inicial de API para consulta operacional de estoque:
- `GET /api/estoque/saldos`
  - Consulta saldos por organizacao, com filtros opcionais por unidade, produto e codigo de produto.
- `GET /api/estoque/reservas/ativas`
  - Consulta reservas vigentes com filtros opcionais por unidade, produto e status.

Caracteristicas:
- Endpoints protegidos por autenticacao.
- Resolucao de organizacao por `OrgContext` quando `idOrganizacao` nao e informado.
- Contrato de resposta padronizado com `ApiResponse`.

## 13) Evolucao de Enforcement de Contexto
O `OrgContextEnforcementMiddleware` passou a cobrir tambem as rotas de estoque:
- Escopo protegido:
  - `/api/organizacao`
  - `/api/estoque`
- Regras mantidas:
  - `X-Organizacao-Id` invalido -> `403 ORG_HEADER_INVALID`
  - `X-Organizacao-Id` sem acesso -> `403 ORG_FORBIDDEN`

## 14) Incremento de API (Historico de Movimentacoes)
Foi adicionado endpoint de leitura para auditoria operacional do estoque:
- `GET /api/estoque/movimentacoes`
  - Consulta historico de entradas/saidas/ajustes por organizacao.
  - Filtros opcionais:
    - `idUnidadeOrganizacional`
    - `idProduto`
    - `idLote`
    - `tipoMovimento`
    - `dataDe` / `dataAte`
  - Paginacao com `pagina` e `tamanhoPagina`.

Caracteristicas:
- Endpoint protegido por autenticacao.
- Resolucao de organizacao por `OrgContext` quando `idOrganizacao` nao e informado.
- Validacoes de entrada para pagina/tamanho e consistencia de periodo.
- Contrato padronizado com `ApiResponse`.

## 15) Evolucao de Qualidade (Historico de Movimentacoes)
Foi adicionada cobertura de integracao para o novo endpoint em:
- `ProformaFarm.Application.Tests/Integration/Estoque/EstoqueMovimentacoesHistoricoEndpointTests.cs`

Cenarios cobertos:
- `401` sem token.
- `200` com token e filtro por `tipoMovimento`.
- Paginacao funcional (pagina 1 e 2 com `tamanhoPagina=1`).
- Validacao funcional (`400` para periodo invalido).

Status:
- Testes do incremento executados com sucesso (4 aprovados).

## 16) Incremento de API (Exportacao CSV de Movimentacoes)
Foi adicionado endpoint de exportacao operacional:
- `GET /api/estoque/movimentacoes/exportar-csv`
  - Exporta historico de movimentacoes em CSV com UTF-8 BOM.
  - Filtros opcionais:
    - `idUnidadeOrganizacional`
    - `idProduto`
    - `idLote`
    - `tipoMovimento`
    - `dataDe` / `dataAte`
  - Parametro `limite` para controle de volume exportado.

Caracteristicas:
- Endpoint protegido por autenticacao.
- Resolucao de organizacao por `OrgContext` quando `idOrganizacao` nao e informado.
- Validacoes de entrada para `limite` e consistencia de periodo.
- Reuso de estrategia de escaping CSV para preservar integridade de campos textuais.

## 17) Evolucao de Qualidade (Exportacao CSV de Movimentacoes)
Foi adicionada cobertura de integracao para o novo endpoint em:
- `ProformaFarm.Application.Tests/Integration/Estoque/EstoqueMovimentacoesExportCsvEndpointTests.cs`

Cenarios cobertos:
- `401` sem token.
- `200` com token, cabecalho CSV e dados de movimentacao.
- `400` para periodo invalido.

Status:
- Testes do incremento executados com sucesso (3 aprovados).

## 18) Incremento de API (Exportacao CSV Operacional Ampliada)
Foi ampliada a cobertura de exportacao CSV para consultas operacionais adicionais:
- `GET /api/estoque/saldos/exportar-csv`
  - Exporta saldos de estoque com filtros por organizacao/unidade/produto/codigo.
- `GET /api/estoque/reservas/ativas/exportar-csv`
  - Exporta reservas vigentes com filtros por organizacao/unidade/produto/status.

Padrao aplicado:
- UTF-8 BOM para compatibilidade com planilhas.
- Parametro `limite` com validacao de faixa.
- Resolucao de organizacao por `OrgContext` quando `idOrganizacao` nao e informado.
- Escaping consistente de campos textuais no CSV.

## 19) Evolucao de Qualidade (Exportacao CSV Operacional Ampliada)
Foram adicionados testes de integracao para os novos endpoints:
- `ProformaFarm.Application.Tests/Integration/Estoque/EstoqueSaldosExportCsvEndpointTests.cs`
- `ProformaFarm.Application.Tests/Integration/Estoque/EstoqueReservasAtivasExportCsvEndpointTests.cs`

Cenarios cobertos:
- `401` sem token.
- `200` com token, cabecalho CSV e dados esperados.

Status:
- Suite filtrada de exportacao CSV executada com sucesso (9 aprovados).

## 20) Padronizacao Tecnica (CsvExportService)
Foi implementado um servico utilitario unico para exportacao CSV, com objetivo de reduzir duplicacao e facilitar reuso em novos dominios (Comercial/Fiscal):

- Interface:
  - `ProformaFarm.Application/Interfaces/Export/ICsvExportService.cs`
- Implementacao:
  - `ProformaFarm.Application/Services/Export/CsvExportService.cs`
- Registro em DI:
  - `Program.cs` com `AddScoped<ICsvExportService, CsvExportService>()`

Caracteristicas tecnicas:
- construcao tabular generica por colunas declarativas (`Header + ValueSelector`);
- formatacao invariante para tipos numericos;
- serializacao padrao ISO-8601 para datas;
- escaping CSV centralizado (virgula, aspas, quebra de linha).

## 21) Refatoracao do EstoqueController (Exportacoes)
As exportacoes CSV do `EstoqueController` foram refatoradas para consumir o `ICsvExportService`, removendo builders locais duplicados.

Escopo aplicado:
- `saldos/exportar-csv`
- `reservas/ativas/exportar-csv`
- `reservas/exportar-csv`
- `movimentacoes/exportar-csv`

Resultado:
- menor acoplamento no controller;
- padrao unico de formatacao CSV;
- base pronta para replicacao em modulos futuros.

## 22) Governanca de Exportacoes (CSV + PDF)
Foi formalizada a diretriz de exportacoes para os proximos dominios em documento dedicado:

- `docs/ESTRATEGIA_EXPORTACOES_CSV_PDF.md`

Pontos consolidados:
- padrao oficial CSV unico para novos dominios via `ICsvExportService`;
- convencoes de endpoint, codificacao, validacoes e seguranca;
- estrategia de estudo e adocao incremental para exportacao PDF;
- recomendacao inicial de POC tecnica para relatorio de estoque em PDF.

## 23) Atualizacao Executiva (README)
O `docs/README.md` foi atualizado com:

- secao de acordo de padronizacao de exportacoes;
- referencia oficial para a estrategia CSV/PDF.

## 24) Contrato Unificado de Exportacao (API + Backend + Frontend)
Foi aplicado contrato padrao de resposta para exportacoes CSV no `EstoqueController` com metadados em headers:

- `X-Export-Format`
- `X-Export-Resource`
- `X-Export-GeneratedAtUtc`
- `X-Export-FileName`
- `Access-Control-Expose-Headers` com exposicao dos metadados de exportacao

Objetivo:
- habilitar consumo consistente no painel backend e frontend sem logica ad-hoc por endpoint.

## 25) Qualidade (Contrato de Headers de Exportacao)
Os testes de exportacao CSV foram ampliados para validar o contrato de headers:

- `EstoqueSaldosExportCsvEndpointTests`
- `EstoqueReservasAtivasExportCsvEndpointTests`
- `EstoqueReservasExportCsvEndpointTests`
- `EstoqueMovimentacoesExportCsvEndpointTests`

Status:
- suite filtrada de exportacoes CSV executada com sucesso (9 aprovados).

## 26) POC PDF (Estoque)
Foi implementado o primeiro incremento de exportacao PDF no dominio de estoque:

- Interface: `IPdfExportService`
- Implementacao: `PdfExportService`
- Registro em DI no `Program.cs`
- Endpoint piloto:
  - `GET /api/estoque/saldos/exportar-pdf`

Caracteristicas:
- filtros equivalentes ao endpoint de saldos;
- protecao por autenticacao + OrgContext;
- retorno em `application/pdf`;
- metadados de exportacao padronizados via headers `X-Export-*`.

## 27) Qualidade (PDF)
Foi adicionada cobertura de integracao para o endpoint piloto:

- `EstoqueSaldosExportPdfEndpointTests`
  - `401` sem token
  - `200` com tipo de arquivo PDF e headers de metadados

Status:
- suite filtrada de exportacoes (CSV + PDF piloto) executada com sucesso (11 aprovados).

## 28) Expansao PDF (Reservas e Movimentacoes)
A POC de PDF foi expandida para os demais fluxos operacionais de estoque:

- `GET /api/estoque/reservas/exportar-pdf`
- `GET /api/estoque/movimentacoes/exportar-pdf`

Padrao mantido:
- filtros operacionais equivalentes aos endpoints CSV;
- autenticacao + OrgContext;
- contrato de metadados `X-Export-*` e CORS expose headers.

## 29) Qualidade (PDF Expandido)
Foram adicionados testes de integracao:

- `EstoqueReservasExportPdfEndpointTests`
- `EstoqueMovimentacoesExportPdfEndpointTests`

Cenarios cobertos:
- `401` sem token;
- `200` com `application/pdf`;
- validacao do contrato de headers padronizados.

Status:
- suite filtrada de PDF executada com sucesso (6 aprovados).

## 30) Evolucao do Motor PDF (QuestPDF)
O `PdfExportService` foi evoluido para usar motor dedicado de renderizacao PDF:

- pacote adicionado: `QuestPDF` em `ProformaFarm.Application`
- licenca de runtime configurada para uso comunitario (`LicenseType.Community`)
- layout evoluido para:
  - cabecalho com titulo e data/hora UTC de geracao;
  - tabela com cabecalho visual e linhas de dados;
  - suporte a quebra multipagina;
  - rodape com paginacao (`Pagina X / Y`).

Impacto tecnico:
- removeu a geracao manual de bytes PDF;
- aumentou legibilidade e consistencia visual dos relatorios;
- estabeleceu base reutilizavel para PDFs de futuros dominios (Comercial/Fiscal).

## 31) Posicionamento de Plataforma (API + Painel + Frontend)
Foi formalizado o posicionamento do ProformaFarmERP como plataforma completa, e nao apenas API:

- API como camada de dominio/integracao;
- painel backend como retaguarda administrativa;
- frontend para operacao omnichannel.

Estado atual:
- backend e contratos de API em fase avancada de consolidacao;
- painel web inicial publicado em `wwwroot/painel` para validacao funcional;
- evolucao das interfaces seguira a mesma padronizacao de contratos, seguranca e observabilidade dos endpoints.

## 32) Incremento de Painel Administrativo Inicial (Organizacao + Estoque)
O painel web em `wwwroot/painel/index.html` foi evoluido para uma base administrativa funcional, com foco em validacao end-to-end do contrato de API:

- autenticacao real via `POST /api/auth/login` com persistencia local de token;
- consulta de organizacao:
  - `GET /api/organizacao/contexto`
  - `GET /api/organizacao/estrutura`
  - `GET /api/organizacao/estrutura/arvore`
- consulta operacional de estoque:
  - `GET /api/estoque/saldos`
  - `GET /api/estoque/reservas/ativas`
  - `GET /api/estoque/movimentacoes`
- exportacoes CSV/PDF com cliente HTTP unificado e leitura dos headers `X-Export-*`.

Diretriz aplicada:
- o mesmo contrato de integracao foi usado para retaguarda (painel) e frontend, reduzindo divergencias de comportamento entre canais.

## 33) Modularizacao do Painel Web e Tabela Operacional
O painel backend evoluiu de script unico para estrutura modular em `wwwroot/painel/js`:

- `api-client.js`: cliente HTTP unificado (JSON + exportacao) com Bearer token;
- `painel-organizacao.js`: fluxo de consultas de contexto, estrutura e arvore;
- `painel-estoque.js`: consultas de estoque e exportacoes CSV/PDF;
- `ui.js`: utilitarios de status, serializacao e renderizacao tabular;
- `main.js`: composicao dos modulos e ciclo de autenticacao.

Incremento funcional adicional:
- consultas de estoque passaram a exibir tabela no painel para:
  - `saldos`
  - `reservas/ativas`
  - `movimentacoes`

Resultado:
- reducao de acoplamento no frontend administrativo;
- base preparada para crescimento por dominios sem concentrar logica em um unico arquivo.

## 34) Painel Transacional de Estoque e Reservas
Foi incorporado ao painel backend um bloco operacional transacional conectado aos endpoints reais:

- movimentacoes:
  - `POST /api/estoque/movimentacoes/entrada`
  - `POST /api/estoque/movimentacoes/saida`
  - `POST /api/estoque/movimentacoes/ajuste`
- reservas:
  - `POST /api/estoque/reservas`
  - `POST /api/estoque/reservas/{id}/confirmar`
  - `POST /api/estoque/reservas/{id}/cancelar`
  - `POST /api/estoque/reservas/expirar`

Implementacao no frontend administrativo:
- novo modulo `wwwroot/painel/js/painel-transacoes.js`;
- reaproveitamento do cliente unificado `api-client.js`;
- refresh automatico da consulta de estoque apos operacao concluida.

Impacto:
- reduz tempo de validacao operacional de regras transacionais;
- acelera homologacao funcional sem depender exclusivamente de Swagger.

## 35) Testes de Integracao do Painel (Smoke)
Foi adicionada cobertura de integracao para a camada web administrativa em:

- `ProformaFarm.Application.Tests/Integration/Painel/PainelBackendSmokeTests.cs`

Cenarios cobertos:
- disponibilidade dos assets do painel (`/`, `/painel/`, `/painel/js/main.js`);
- fluxo autenticado basico usado pelo painel (contexto organizacional, consulta de saldo e exportacao CSV);
- validacao de endpoints transacionais consumidos pelo painel sem mutacao de estado (retornos `400` para payload invalido).

Status:
- suite `ProformaFarm.Application.Tests` executada com sucesso apos incremento (67 testes aprovados).

## 36) Testes de Contrato Painel/API (ApiResponse + Headers de Exportação)
Foi adicionada suíte de contrato para blindar a integração entre painel e API:

- `ProformaFarm.Application.Tests/Integration/Painel/PainelContratoIntegracaoTests.cs`

Cobertura implementada:
- validação do envelope `ApiResponse` nos endpoints de leitura usados pelo painel:
  - `GET /api/organizacao/contexto`
  - `GET /api/estoque/saldos`
  - `GET /api/estoque/reservas/ativas`
  - `GET /api/estoque/movimentacoes`
- validação de contrato de exportação para CSV e PDF por recurso:
  - presença dos headers `X-Export-Format`, `X-Export-Resource`, `X-Export-GeneratedAtUtc`, `X-Export-FileName`
  - consistência de valores (formato/recurso)
  - verificação de `Access-Control-Expose-Headers` para consumo de frontend/painel.

Status:
- suíte `ProformaFarm.Application.Tests` executada com sucesso após incremento (77 testes aprovados).

## 37) E2E Real de Navegador (Playwright) para o Painel
Foi implementado teste E2E real em navegador para validar o fluxo ponta a ponta do painel:

- pacote adicionado em testes: `Microsoft.Playwright`;
- infraestrutura de host local para execução E2E: `ProformaFarm.Application.Tests/Common/PainelE2eAppHost.cs`;
- teste E2E: `ProformaFarm.Application.Tests/Integration/Painel/PainelBackendE2ePlaywrightTests.cs`.

Cenário validado:
- abertura do painel em navegador real (Chromium/Edge headless);
- autenticação via formulário do painel;
- consulta de estoque com renderização de tabela;
- exportação CSV com captura de download e validação de arquivo.

Status:
- teste E2E dedicado executado com sucesso;
- suíte `ProformaFarm.Application.Tests` executada com sucesso após o incremento (78 testes aprovados).

## 38) E2E Transacional no Painel (Entrada/Saída + Confirmação Visual)
Foi adicionado o segundo cenário E2E de navegador para validação operacional transacional com retorno visual imediato no painel:

- arquivo: `ProformaFarm.Application.Tests/Integration/Painel/PainelBackendE2ePlaywrightTests.cs`
- cenário coberto:
  - autenticação no painel;
  - execução de movimentação de entrada via formulário;
  - confirmação da atualização imediata na tabela de movimentações;
  - execução de movimentação de saída via formulário;
  - nova confirmação visual imediata na tabela.

Status:
- suíte E2E de painel (2 cenários Playwright) executada com sucesso.

## 39) Governança de CI/CD para Build, Integração e E2E
Foi implementado workflow de CI em GitHub Actions para execução em estágios:

- arquivo: `.github/workflows/ci.yml`
- estágios:
  - `build`
  - `integration` (sem os testes E2E de navegador)
  - `e2e` (somente testes Playwright do painel)

Capacidades adicionadas:
- instalação de navegador para Playwright no job E2E;
- upload de artefatos de teste (`trx`) em integração e E2E;
- upload de artefatos E2E de falha/suporte (`trace`, `screenshot`, `video`) gerados em `TestResults/e2e`.

Status:
- filtros de execução validados localmente:
  - integração: 77 testes aprovados;
  - E2E: 2 testes aprovados.

## 40) Pré-requisito World-Class: Domain Events + Outbox Pattern
Foi concluído o incremento de infraestrutura transversal para eventos de domínio com garantia transacional e processamento assíncrono:

- script idempotente de banco:
  - `docs/sql/005_core_outbox.sql`
- tabelas:
  - `Core.OutboxEvent`
  - `Core.OutboxProcessedEvent` (idempotência)
  - `Core.OutboxHelloProbe` (prova de vida)
- persistência do evento no Outbox dentro do `SaveChanges` com `OutboxSaveChangesInterceptor`.
- processamento assíncrono por `OutboxProcessorHostedService` + `OutboxProcessor` com lock, retry e backoff.

Regras consolidadas no incremento:
- `OrganizacaoId` derivado de `OrgContext` e propagado ao evento;
- atomicidade entre operação transacional e persistência do evento no Outbox;
- idempotência por `EventId` + `HandlerName`;
- observabilidade mínima com `EventId` e `CorrelationId` em logs.

## 41) Hello Event (Prova de Vida do Pipeline)
Antes de expandir para novos domínios/eventos, foi implementado e validado o cenário de prova de vida ponta a ponta:

- comando transacional gera evento;
- evento aparece no Outbox;
- worker processa;
- registro é marcado como processado.

Endpoints adicionados para suporte operacional:
- `POST /api/outbox/hello-event`
- `POST /api/outbox/processar-agora`

## 42) Qualidade (Outbox)
Foram adicionados testes de integração dedicados em:

- `ProformaFarm.Application.Tests/Integration/Outbox/OutboxPipelineEndpointTests.cs`

Cenários cobertos:
- persistência do evento no Outbox;
- processamento bem-sucedido;
- retry/backoff com falha simulada;
- idempotência por `EventId`.

Status:
- suíte filtrada de Outbox executada com sucesso (4 aprovados).

## 43) Evento de Negócio Real no Outbox (Estoque Baixo)
Foi implementado o primeiro evento de negócio real sobre o pipeline Outbox:

- evento: `EstoqueBaixoDomainEvent`;
- geração no domínio de estoque para operações que reduzem saldo (saída, ajuste e confirmação de reserva);
- condição operacional aplicada: saldo líquido (`QuantidadeDisponivel - QuantidadeReservada`) em faixa de estoque baixo;
- gravação no Outbox dentro da mesma transação Dapper da movimentação.

## 44) Handler Idempotente para Estoque Baixo
Foi incorporado handler dedicado:

- `EstoqueBaixoDomainEventHandler`;
- persistência de evidência operacional em `Core.EstoqueBaixoNotificacao`;
- idempotência garantida por `EventId` com índice único (`UX_Core_EstoqueBaixoNotificacao_EventId`).

Ajustes de banco:
- atualização de `docs/sql/005_core_outbox.sql` com estrutura da tabela de notificação e índices operacionais.

## 45) Qualidade (Outbox + Evento de Negócio)
Foi adicionada suíte de integração para o cenário real:

- `ProformaFarm.Application.Tests/Integration/Outbox/OutboxEstoqueBaixoPipelineTests.cs`

Cenário coberto:
- operação de saída reduz saldo para faixa de estoque baixo;
- evento é persistido no Outbox;
- worker processa o evento;
- notificação é registrada com idempotência por `EventId`.

Status:
- suíte filtrada de Outbox executada com sucesso (5 aprovados).
