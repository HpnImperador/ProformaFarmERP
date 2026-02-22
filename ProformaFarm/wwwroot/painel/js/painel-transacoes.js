import { numOrEmpty, setStatus, toPretty, txt } from "./ui.js";

export function bindTransacoes(context) {
  const { api, state, elements, refreshConsultaEstoque } = context;

  elements.btnMovExecutar.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.txStatus, "Realize login antes de executar movimentacao.", false);
      return;
    }

    try {
      const movAction = txt("movAction");
      const endpoint = resolveMovimentacaoEndpoint(movAction);
      const body = buildMovimentacaoBody(movAction);
      const payload = await api.requestJson(endpoint, { method: "POST", body });
      setStatus(elements.txStatus, `Movimentacao concluida.\n${toPretty(payload)}`, true);
      refreshConsultaEstoque();
    } catch (error) {
      setStatus(elements.txStatus, `Falha na movimentacao.\n${error}`, false);
    }
  });

  elements.btnReservaCriar.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.txStatus, "Realize login antes de criar reserva.", false);
      return;
    }

    try {
      const body = buildCriarReservaBody();
      const payload = await api.requestJson("/api/estoque/reservas", { method: "POST", body });
      setStatus(elements.txStatus, `Reserva criada com sucesso.\n${toPretty(payload)}`, true);
      refreshConsultaEstoque();
    } catch (error) {
      setStatus(elements.txStatus, `Falha ao criar reserva.\n${error}`, false);
    }
  });

  elements.btnReservaAcao.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.txStatus, "Realize login antes de executar acao da reserva.", false);
      return;
    }

    try {
      const idReserva = numOrEmpty("resId");
      if (!idReserva) throw new Error("IdReservaEstoque obrigatorio.");
      const action = txt("resAction");
      const endpoint = `/api/estoque/reservas/${idReserva}/${action}`;
      const payload = await api.requestJson(endpoint, { method: "POST" });
      setStatus(elements.txStatus, `Acao da reserva concluida.\n${toPretty(payload)}`, true);
      refreshConsultaEstoque();
    } catch (error) {
      setStatus(elements.txStatus, `Falha na acao da reserva.\n${error}`, false);
    }
  });

  elements.btnReservaExpirar.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.txStatus, "Realize login antes de expirar reservas.", false);
      return;
    }

    try {
      const body = buildExpirarReservasBody();
      const payload = await api.requestJson("/api/estoque/reservas/expirar", { method: "POST", body });
      setStatus(elements.txStatus, `Expiracao processada.\n${toPretty(payload)}`, true);
      refreshConsultaEstoque();
    } catch (error) {
      setStatus(elements.txStatus, `Falha ao expirar reservas.\n${error}`, false);
    }
  });
}

function resolveMovimentacaoEndpoint(action) {
  if (action === "entrada") return "/api/estoque/movimentacoes/entrada";
  if (action === "saida") return "/api/estoque/movimentacoes/saida";
  if (action === "ajuste") return "/api/estoque/movimentacoes/ajuste";
  throw new Error(`Acao de movimentacao nao suportada: ${action}`);
}

function buildMovimentacaoBody(action) {
  const idOrganizacao = numOrEmpty("idOrganizacao");
  const idUnidade = numOrEmpty("idUnidade");
  const idProduto = numOrEmpty("idProduto");
  const idLote = numOrEmpty("idLote");
  const documentoReferencia = txt("movDocumento");

  if (!idUnidade || !idProduto) {
    throw new Error("IdUnidade e IdProduto sao obrigatorios para movimentacao.");
  }

  const body = {
    idUnidadeOrganizacional: Number(idUnidade),
    idProduto: Number(idProduto)
  };

  if (idOrganizacao) body.idOrganizacao = Number(idOrganizacao);
  if (idLote) body.idLote = Number(idLote);
  if (documentoReferencia) body.documentoReferencia = documentoReferencia;

  if (action === "ajuste") {
    const quantidadeDisponivel = txt("movQuantidadeDisponivel");
    if (!quantidadeDisponivel) throw new Error("QuantidadeDisponivel obrigatoria para ajuste.");
    body.quantidadeDisponivel = Number(quantidadeDisponivel);
  } else {
    const quantidade = txt("movQuantidade");
    if (!quantidade) throw new Error("Quantidade obrigatoria para entrada/saida.");
    body.quantidade = Number(quantidade);
  }

  return body;
}

function buildCriarReservaBody() {
  const idOrganizacao = numOrEmpty("idOrganizacao");
  const idUnidade = numOrEmpty("idUnidade");
  const idProduto = numOrEmpty("idProduto");
  const idLote = numOrEmpty("idLote");
  const quantidade = txt("resQuantidade");
  const ttlMinutos = txt("resTtl");
  const documentoReferencia = txt("resDocumento");

  if (!idUnidade || !idProduto || !quantidade || !ttlMinutos) {
    throw new Error("Para criar reserva: IdUnidade, IdProduto, Quantidade e TTL sao obrigatorios.");
  }

  const body = {
    idUnidadeOrganizacional: Number(idUnidade),
    idProduto: Number(idProduto),
    quantidade: Number(quantidade),
    ttlMinutos: Number(ttlMinutos)
  };

  if (idOrganizacao) body.idOrganizacao = Number(idOrganizacao);
  if (idLote) body.idLote = Number(idLote);
  if (documentoReferencia) body.documentoReferencia = documentoReferencia;

  return body;
}

function buildExpirarReservasBody() {
  const idOrganizacao = numOrEmpty("idOrganizacao");
  const idUnidade = numOrEmpty("idUnidade");
  const idProduto = numOrEmpty("idProduto");
  const idLote = numOrEmpty("idLote");
  const maxItens = txt("resExpirarMaxItens") || "200";

  const body = { maxItens: Number(maxItens) };
  if (idOrganizacao) body.idOrganizacao = Number(idOrganizacao);
  if (idUnidade) body.idUnidadeOrganizacional = Number(idUnidade);
  if (idProduto) body.idProduto = Number(idProduto);
  if (idLote) body.idLote = Number(idLote);
  return body;
}
