# Estrategia de Indices Operacionais - Estrutura Organizacional

## Objetivo
Garantir baixa latencia para consultas operacionais de lotacao, arvore organizacional e alocacao de centro de custo, sem comprometer escrita.

## Premissas de uso
- Multiempresa por `IdOrganizacao`.
- Consultas frequentes por unidades ativas e por hierarquia pai/filho.
- Consultas de lotacao por usuario ativo (contexto de autenticacao/autorizacao).
- Vinculo unidade-centro de custo para rateio e relatorios.

## Indices de integridade (unicidade)
- `UX_Organizacao_Cnpj`
  - Evita duplicidade de CNPJ.
- `UX_UnidadeOrganizacional_Org_Codigo`
  - Codigo unico por organizacao.
- `UX_CentroCusto_Org_Codigo`
  - Codigo unico de centro de custo por organizacao.
- `UX_Cargo_Org_Codigo`
  - Codigo unico de cargo por organizacao.
- `UX_LotacaoUsuario_PrincipalAtiva` (filtrado)
  - Garante apenas 1 lotacao principal ativa por usuario.
- `UX_UnidadeCentroCusto_PrincipalPorUnidade` (filtrado)
  - Garante no maximo 1 centro de custo principal por unidade.

## Indices de leitura operacional
- `IX_UnidadeOrganizacional_Org_Pai_Ativa`
  - Caso de uso: carregar filhos de uma unidade por organizacao (menu/arvore).
- `IX_UnidadeOrganizacional_Org_Tipo_Ativa`
  - Caso de uso: listar unidades por tipo (ex.: filiais ativas).
- `IX_CentroCusto_Org_Ativo`
  - Caso de uso: combos/listagens de centros de custo ativos.
- `IX_UnidadeCentroCusto_CentroCusto`
  - Caso de uso: localizar unidades de um centro de custo.
- `IX_LotacaoUsuario_Usuario_Ativa`
  - Caso de uso: resolver lotacao corrente do usuario logado.
- `IX_LotacaoUsuario_Unidade_Ativa`
  - Caso de uso: listar equipe ativa de uma unidade.

## Regras de manutencao
- Atualizar estatisticas com amostragem adequada em tabelas de alta escrita (`LotacaoUsuario`).
- Rebuild/reorganize de indices conforme fragmentacao:
  - Reorganize: 10% a 30%
  - Rebuild: acima de 30%
- Monitorar crescimento de indices filtrados e plano de execucao para garantir seletividade.

## Evolucao recomendada (fase de escala)
- Particionamento por `IdOrganizacao` em tabelas com alto volume historico (`LotacaoUsuario`).
- Inclusao de `rowversion` em tabelas de escrita concorrente para controle otimista.
- Materializacao de hierarquia (closure table ou path) se consulta recursiva ficar critica.
