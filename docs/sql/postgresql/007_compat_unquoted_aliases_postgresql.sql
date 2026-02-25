-- Compatibilidade de naming para SQL nao-citado (dbo/core/integration)
-- Objetivo: permitir consultas legadas com dbo.X / Core.X / Integration.X
-- usando nomes de colunas sem aspas (em minusculo), sobre tabelas com colunas citadas.

BEGIN;

CREATE SCHEMA IF NOT EXISTS dbo;
CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS integration;

DROP VIEW IF EXISTS dbo.usuario;
DROP VIEW IF EXISTS dbo.perfil;
DROP VIEW IF EXISTS dbo.usuarioperfil;
DROP VIEW IF EXISTS dbo.refreshtoken;
DROP VIEW IF EXISTS dbo.organizacao;
DROP VIEW IF EXISTS dbo.unidadeorganizacional;
DROP VIEW IF EXISTS dbo.centrocusto;
DROP VIEW IF EXISTS dbo.unidadecentrocusto;
DROP VIEW IF EXISTS dbo.cargo;
DROP VIEW IF EXISTS dbo.lotacaousuario;
DROP VIEW IF EXISTS dbo.produto;
DROP VIEW IF EXISTS dbo.lote;
DROP VIEW IF EXISTS dbo.estoque;
DROP VIEW IF EXISTS dbo.movimentacaoestoque;
DROP VIEW IF EXISTS dbo.reservaestoque;
DROP VIEW IF EXISTS core.outboxevent;
DROP VIEW IF EXISTS core.outboxprocessedevent;
DROP VIEW IF EXISTS core.outboxhelloprobe;
DROP VIEW IF EXISTS core.estoquebaixonotificacao;
DROP VIEW IF EXISTS core.estoquerepostonotificacao;
DROP VIEW IF EXISTS integration.integrationclient;
DROP VIEW IF EXISTS integration.integrationdeliverylog;

-- dbo (auth)
CREATE OR REPLACE VIEW dbo.usuario AS
SELECT
    "IdUsuario" AS idusuario,
    "Nome" AS nome,
    "Login" AS login,
    "SenhaHash" AS senhahash,
    "SenhaSalt" AS senhasalt,
    "Ativo" AS ativo,
    "DataCriacao" AS datacriacao
FROM public."Usuario";

CREATE OR REPLACE VIEW dbo.perfil AS
SELECT
    "IdPerfil" AS idperfil,
    "Nome" AS nome
FROM public."Perfil";

CREATE OR REPLACE VIEW dbo.usuarioperfil AS
SELECT
    "IdUsuario" AS idusuario,
    "IdPerfil" AS idperfil
FROM public."UsuarioPerfil";

CREATE OR REPLACE VIEW dbo.refreshtoken AS
SELECT
    "IdRefreshToken" AS idrefreshtoken,
    "IdUsuario" AS idusuario,
    "TokenHash" AS tokenhash,
    "CriadoEmUtc" AS criadoemutc,
    "ExpiraEmUtc" AS expiraemutc,
    "RevogadoEmUtc" AS revogadoemutc,
    "CriadoPorIp" AS criadoporip,
    "RevogadoPorIp" AS revogadoporip,
    "SubstituidoPorHash" AS substituidoporhash
FROM public."RefreshToken";

-- dbo (organizacao)
CREATE OR REPLACE VIEW dbo.organizacao AS
SELECT
    "IdOrganizacao" AS idorganizacao,
    "RazaoSocial" AS razaosocial,
    "NomeFantasia" AS nomefantasia,
    "Cnpj" AS cnpj,
    "Ativa" AS ativa,
    "DataCriacao" AS datacriacao
FROM public."Organizacao";

CREATE OR REPLACE VIEW dbo.unidadeorganizacional AS
SELECT
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdOrganizacao" AS idorganizacao,
    "IdUnidadePai" AS idunidadepai,
    "Tipo" AS tipo,
    "Codigo" AS codigo,
    "Nome" AS nome,
    "Ativa" AS ativa,
    "DataInicio" AS datainicio,
    "DataFim" AS datafim
FROM public."UnidadeOrganizacional";

CREATE OR REPLACE VIEW dbo.centrocusto AS
SELECT
    "IdCentroCusto" AS idcentrocusto,
    "IdOrganizacao" AS idorganizacao,
    "Codigo" AS codigo,
    "Descricao" AS descricao,
    "Ativo" AS ativo
FROM public."CentroCusto";

CREATE OR REPLACE VIEW dbo.unidadecentrocusto AS
SELECT
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdCentroCusto" AS idcentrocusto,
    "Principal" AS principal
FROM public."UnidadeCentroCusto";

CREATE OR REPLACE VIEW dbo.cargo AS
SELECT
    "IdCargo" AS idcargo,
    "IdOrganizacao" AS idorganizacao,
    "Codigo" AS codigo,
    "Nome" AS nome,
    "Ativo" AS ativo
FROM public."Cargo";

CREATE OR REPLACE VIEW dbo.lotacaousuario AS
SELECT
    "IdLotacaoUsuario" AS idlotacaousuario,
    "IdUsuario" AS idusuario,
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdCargo" AS idcargo,
    "DataInicio" AS datainicio,
    "DataFim" AS datafim,
    "Principal" AS principal,
    "Ativa" AS ativa
FROM public."LotacaoUsuario";

-- dbo (estoque)
CREATE OR REPLACE VIEW dbo.produto AS
SELECT
    "IdProduto" AS idproduto,
    "IdOrganizacao" AS idorganizacao,
    "Codigo" AS codigo,
    "Nome" AS nome,
    "Ativo" AS ativo
FROM public."Produto";

CREATE OR REPLACE VIEW dbo.lote AS
SELECT
    "IdLote" AS idlote,
    "IdOrganizacao" AS idorganizacao,
    "IdProduto" AS idproduto,
    "NumeroLote" AS numerolote,
    "DataFabricacao" AS datafabricacao,
    "DataValidade" AS datavalidade,
    "Bloqueado" AS bloqueado
FROM public."Lote";

CREATE OR REPLACE VIEW dbo.estoque AS
SELECT
    "IdEstoque" AS idestoque,
    "IdOrganizacao" AS idorganizacao,
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdProduto" AS idproduto,
    "IdLote" AS idlote,
    "QuantidadeDisponivel" AS quantidadedisponivel,
    "QuantidadeReservada" AS quantidadereservada
FROM public."Estoque";

CREATE OR REPLACE VIEW dbo.movimentacaoestoque AS
SELECT
    "IdMovimentacaoEstoque" AS idmovimentacaoestoque,
    "IdOrganizacao" AS idorganizacao,
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdProduto" AS idproduto,
    "IdLote" AS idlote,
    "TipoMovimento" AS tipomovimento,
    "Quantidade" AS quantidade,
    "DocumentoReferencia" AS documentoreferencia,
    "DataMovimento" AS datamovimento
FROM public."MovimentacaoEstoque";

CREATE OR REPLACE VIEW dbo.reservaestoque AS
SELECT
    "IdReservaEstoque" AS idreservaestoque,
    "IdOrganizacao" AS idorganizacao,
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdProduto" AS idproduto,
    "IdLote" AS idlote,
    "Quantidade" AS quantidade,
    "ExpiraEmUtc" AS expiraemutc,
    "Status" AS status,
    "DocumentoReferencia" AS documentoreferencia
FROM public."ReservaEstoque";

-- core (outbox)
CREATE OR REPLACE VIEW core.outboxevent AS
SELECT
    "Id" AS id,
    "OrganizacaoId" AS organizacaoid,
    "EventType" AS eventtype,
    "Payload" AS payload,
    "OccurredOnUtc" AS occurredonutc,
    "CorrelationId" AS correlationid,
    "Status" AS status,
    "RetryCount" AS retrycount,
    "NextAttemptUtc" AS nextattemptutc,
    "ProcessedOnUtc" AS processedonutc,
    "LockedUntilUtc" AS lockeduntilutc,
    "LastError" AS lasterror
FROM "Core"."OutboxEvent";

CREATE OR REPLACE VIEW core.outboxprocessedevent AS
SELECT
    "Id" AS id,
    "EventId" AS eventid,
    "HandlerName" AS handlername,
    "ProcessedOnUtc" AS processedonutc
FROM "Core"."OutboxProcessedEvent";

CREATE OR REPLACE VIEW core.outboxhelloprobe AS
SELECT
    "IdOutboxHelloProbe" AS idoutboxhelloprobe,
    "OrganizacaoId" AS organizacaoid,
    "NomeEvento" AS nomeevento,
    "SimularFalhaUmaVez" AS simularfalhaumavez,
    "ProcessedCount" AS processedcount,
    "CriadoEmUtc" AS criadoemutc,
    "UltimoProcessamentoUtc" AS ultimoprocessamentoutc
FROM "Core"."OutboxHelloProbe";

CREATE OR REPLACE VIEW core.estoquebaixonotificacao AS
SELECT
    "Id" AS id,
    "EventId" AS eventid,
    "OrganizacaoId" AS organizacaoid,
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdProduto" AS idproduto,
    "IdLote" AS idlote,
    "QuantidadeDisponivel" AS quantidadedisponivel,
    "QuantidadeReservada" AS quantidadereservada,
    "QuantidadeLiquida" AS quantidadeliquida,
    "LimiteEstoqueBaixo" AS limiteestoquebaixo,
    "OrigemMovimento" AS origemmovimento,
    "DocumentoReferencia" AS documentoreferencia,
    "CorrelationId" AS correlationid,
    "DetectadoEmUtc" AS detectadoemutc,
    "RegistradoEmUtc" AS registradoemutc
FROM "Core"."EstoqueBaixoNotificacao";

CREATE OR REPLACE VIEW core.estoquerepostonotificacao AS
SELECT
    "Id" AS id,
    "EventId" AS eventid,
    "OrganizacaoId" AS organizacaoid,
    "IdUnidadeOrganizacional" AS idunidadeorganizacional,
    "IdProduto" AS idproduto,
    "IdLote" AS idlote,
    "QuantidadeLiquidaAntes" AS quantidadeliquidaantes,
    "QuantidadeLiquidaDepois" AS quantidadeliquidadepois,
    "LimiteEstoqueBaixo" AS limiteestoquebaixo,
    "OrigemMovimento" AS origemmovimento,
    "DocumentoReferencia" AS documentoreferencia,
    "CorrelationId" AS correlationid,
    "DetectadoEmUtc" AS detectadoemutc,
    "RegistradoEmUtc" AS registradoemutc
FROM "Core"."EstoqueRepostoNotificacao";

-- integration (relay)
CREATE OR REPLACE VIEW integration.integrationclient AS
SELECT
    "IdIntegrationClient" AS idintegrationclient,
    "OrganizacaoId" AS organizacaoid,
    "Nome" AS nome,
    "WebhookUrl" AS webhookurl,
    "SecretKey" AS secretkey,
    "Ativo" AS ativo,
    "CriadoEmUtc" AS criadoemutc,
    "AtualizadoEmUtc" AS atualizadoemutc
FROM "Integration"."IntegrationClient";

CREATE OR REPLACE VIEW integration.integrationdeliverylog AS
SELECT
    "IdIntegrationDeliveryLog" AS idintegrationdeliverylog,
    "OutboxEventId" AS outboxeventid,
    "OrganizacaoId" AS organizacaoid,
    "IdIntegrationClient" AS idintegrationclient,
    "AttemptCount" AS attemptcount,
    "Status" AS status,
    "ResponseStatusCode" AS responsestatuscode,
    "ResponseBody" AS responsebody,
    "LastError" AS lasterror,
    "NextAttemptUtc" AS nextattemptutc,
    "LastAttemptUtc" AS lastattemptutc,
    "LockedUntilUtc" AS lockeduntilutc,
    "CorrelationId" AS correlationid,
    "CriadoEmUtc" AS criadoemutc
FROM "Integration"."IntegrationDeliveryLog";

COMMIT;
