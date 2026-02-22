import { clearElement, numOrEmpty, renderTable, setStatus, toPretty, txt } from "./ui.js";

const READ_ROUTES = {
  saldos: "/api/estoque/saldos",
  reservasAtivas: "/api/estoque/reservas/ativas",
  movimentacoes: "/api/estoque/movimentacoes"
};

const EXPORT_ROUTES = {
  saldos_csv: "/api/estoque/saldos/exportar-csv",
  saldos_pdf: "/api/estoque/saldos/exportar-pdf",
  reservas_csv: "/api/estoque/reservas/exportar-csv",
  reservas_pdf: "/api/estoque/reservas/exportar-pdf",
  movimentacoes_csv: "/api/estoque/movimentacoes/exportar-csv",
  movimentacoes_pdf: "/api/estoque/movimentacoes/exportar-pdf"
};

const TABLE_COLUMNS = {
  saldos: [
    "idEstoque",
    "codigoUnidade",
    "nomeUnidade",
    "codigoProduto",
    "nomeProduto",
    "numeroLote",
    "quantidadeDisponivel",
    "quantidadeReservada",
    "quantidadeLiquida"
  ],
  reservasAtivas: [
    "idReservaEstoque",
    "codigoUnidade",
    "nomeUnidade",
    "codigoProduto",
    "nomeProduto",
    "numeroLote",
    "quantidade",
    "status",
    "expiraEmUtc",
    "documentoReferencia"
  ],
  movimentacoes: [
    "idMovimentacaoEstoque",
    "dataMovimento",
    "tipoMovimento",
    "codigoUnidade",
    "nomeUnidade",
    "codigoProduto",
    "nomeProduto",
    "numeroLote",
    "quantidade",
    "documentoReferencia"
  ]
};

export function bindEstoque(context) {
  const { api, state, elements } = context;

  elements.btnEstoqueConsultar.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.readStatus, "Realize login antes de consultar estoque.", false);
      return;
    }

    try {
      const resource = txt("readResource");
      const path = READ_ROUTES[resource];
      if (!path) throw new Error(`Recurso de consulta nao mapeado: ${resource}`);

      const query = buildCommonQuery(resource === "movimentacoes");
      const payload = await api.requestJson(path, { query });
      const rows = extractRows(payload);
      renderTable(elements.readTableWrap, rows, TABLE_COLUMNS[resource] || []);

      setStatus(
        elements.readStatus,
        `Consulta concluida. Linhas retornadas: ${rows.length}\n${toPretty(payload)}`,
        true
      );
    } catch (error) {
      clearElement(elements.readTableWrap);
      setStatus(elements.readStatus, `Falha na consulta de estoque.\n${error}`, false);
    }
  });

  elements.btnExport.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.exportStatus, "Realize login antes de exportar.", false);
      return;
    }

    try {
      const resource = txt("exportResource");
      const format = txt("format");
      const routeKey = `${resource}_${format}`;
      const path = EXPORT_ROUTES[routeKey];
      if (!path) throw new Error(`Rota de exportacao nao mapeada: ${routeKey}`);

      const query = buildCommonQuery(false);
      const limite = numOrEmpty("limite");
      if (limite) query.set("limite", limite);

      const exportResult = await api.requestExport(path, query);
      const fileName = exportResult.fileName || `export.${format}`;
      downloadBlob(exportResult.blob, fileName);

      setStatus(
        elements.exportStatus,
        "Exportacao concluida.\n" +
          `Arquivo: ${fileName}\n` +
          `Formato: ${exportResult.format || "-"}\n` +
          `Recurso: ${exportResult.resource || "-"}\n` +
          `Gerado em UTC: ${exportResult.generatedAtUtc || "-"}\n` +
          `Tamanho bytes: ${exportResult.blob.size}`,
        true
      );
    } catch (error) {
      setStatus(elements.exportStatus, `Falha na exportacao.\n${error}`, false);
    }
  });
}

function buildCommonQuery(includePagination) {
  const query = new URLSearchParams();
  const idOrganizacao = numOrEmpty("idOrganizacao");
  const idUnidade = numOrEmpty("idUnidade");
  const idProduto = numOrEmpty("idProduto");
  const idLote = numOrEmpty("idLote");
  const tipoMovimento = txt("tipoMovimento");
  const statusReserva = txt("statusReserva");

  if (idOrganizacao) query.set("idOrganizacao", idOrganizacao);
  if (idUnidade) query.set("idUnidadeOrganizacional", idUnidade);
  if (idProduto) query.set("idProduto", idProduto);
  if (idLote) query.set("idLote", idLote);
  if (tipoMovimento) query.set("tipoMovimento", tipoMovimento);
  if (statusReserva) query.set("status", statusReserva);

  if (includePagination) {
    const pagina = numOrEmpty("pagina");
    const tamanhoPagina = numOrEmpty("tamanhoPagina");
    if (pagina) query.set("pagina", pagina);
    if (tamanhoPagina) query.set("tamanhoPagina", tamanhoPagina);
  }

  return query;
}

function extractRows(payload) {
  if (!payload || typeof payload !== "object") return [];
  const data = payload.data;
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if (Array.isArray(data.itens)) return data.itens;
  return [];
}

function downloadBlob(blob, fileName) {
  const anchor = document.createElement("a");
  anchor.href = URL.createObjectURL(blob);
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
}
