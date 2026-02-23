-- Proforma Farm ERP - Seed minimo Estrutura Organizacional (PostgreSQL)
-- Script: 002_seed_estrutura_organizacional_postgresql.sql

DO $$
DECLARE
    v_id_organizacao integer;
    v_id_matriz integer;
    v_id_filial integer;
    v_id_centro_custo integer;
    v_id_cargo integer;
    v_id_usuario integer;
BEGIN
    SELECT "IdOrganizacao" INTO v_id_organizacao
    FROM "Organizacao"
    WHERE "Cnpj" = '12345678000199';

    IF v_id_organizacao IS NULL THEN
        INSERT INTO "Organizacao" ("RazaoSocial", "NomeFantasia", "Cnpj", "Ativa", "DataCriacao")
        VALUES ('Proforma Farm LTDA', 'Proforma Farm', '12345678000199', true, now())
        RETURNING "IdOrganizacao" INTO v_id_organizacao;
    END IF;

    SELECT "IdUnidadeOrganizacional" INTO v_id_matriz
    FROM "UnidadeOrganizacional"
    WHERE "IdOrganizacao" = v_id_organizacao
      AND "Codigo" = 'MATRIZ';

    IF v_id_matriz IS NULL THEN
        INSERT INTO "UnidadeOrganizacional" (
            "IdOrganizacao", "IdUnidadePai", "Tipo", "Codigo", "Nome", "Ativa", "DataInicio", "DataFim"
        ) VALUES (
            v_id_organizacao, NULL, 'Matriz', 'MATRIZ', 'Matriz Central', true, now(), NULL
        ) RETURNING "IdUnidadeOrganizacional" INTO v_id_matriz;
    END IF;

    SELECT "IdUnidadeOrganizacional" INTO v_id_filial
    FROM "UnidadeOrganizacional"
    WHERE "IdOrganizacao" = v_id_organizacao
      AND "Codigo" = 'FILIAL-001';

    IF v_id_filial IS NULL THEN
        INSERT INTO "UnidadeOrganizacional" (
            "IdOrganizacao", "IdUnidadePai", "Tipo", "Codigo", "Nome", "Ativa", "DataInicio", "DataFim"
        ) VALUES (
            v_id_organizacao, v_id_matriz, 'Filial', 'FILIAL-001', 'Filial 001', true, now(), NULL
        ) RETURNING "IdUnidadeOrganizacional" INTO v_id_filial;
    END IF;

    SELECT "IdCentroCusto" INTO v_id_centro_custo
    FROM "CentroCusto"
    WHERE "IdOrganizacao" = v_id_organizacao
      AND "Codigo" = 'ADM-GERAL';

    IF v_id_centro_custo IS NULL THEN
        INSERT INTO "CentroCusto" ("IdOrganizacao", "Codigo", "Descricao", "Ativo")
        VALUES (v_id_organizacao, 'ADM-GERAL', 'Administracao Geral', true)
        RETURNING "IdCentroCusto" INTO v_id_centro_custo;
    END IF;

    INSERT INTO "UnidadeCentroCusto" ("IdUnidadeOrganizacional", "IdCentroCusto", "Principal")
    VALUES (v_id_matriz, v_id_centro_custo, true)
    ON CONFLICT ("IdUnidadeOrganizacional", "IdCentroCusto") DO NOTHING;

    SELECT "IdCargo" INTO v_id_cargo
    FROM "Cargo"
    WHERE "IdOrganizacao" = v_id_organizacao
      AND "Codigo" = 'GERENTE';

    IF v_id_cargo IS NULL THEN
        INSERT INTO "Cargo" ("IdOrganizacao", "Codigo", "Nome", "Ativo")
        VALUES (v_id_organizacao, 'GERENTE', 'Gerente', true)
        RETURNING "IdCargo" INTO v_id_cargo;
    END IF;

    SELECT "IdUsuario" INTO v_id_usuario
    FROM "Usuario"
    WHERE "Ativo" = true
    ORDER BY "IdUsuario"
    LIMIT 1;

    IF v_id_usuario IS NOT NULL AND NOT EXISTS (
        SELECT 1
        FROM "LotacaoUsuario"
        WHERE "IdUsuario" = v_id_usuario
          AND "Principal" = true
          AND "Ativa" = true
    ) THEN
        INSERT INTO "LotacaoUsuario" (
            "IdUsuario", "IdUnidadeOrganizacional", "IdCargo", "DataInicio", "DataFim", "Principal", "Ativa"
        ) VALUES (
            v_id_usuario, v_id_matriz, v_id_cargo, now(), NULL, true, true
        );
    END IF;
END
$$;
