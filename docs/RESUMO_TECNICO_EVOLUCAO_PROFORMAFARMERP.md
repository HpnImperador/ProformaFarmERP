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
