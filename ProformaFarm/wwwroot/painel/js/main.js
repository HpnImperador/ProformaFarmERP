import { ApiClient } from "./api-client.js";
import { bindEstoque } from "./painel-estoque.js";
import { bindOrganizacao } from "./painel-organizacao.js";
import { bindTransacoes } from "./painel-transacoes.js";
import { setStatus, txt, toPretty } from "./ui.js";

const STORAGE_KEY = "proformafarm.painel.token";

const elements = {
  btnLogin: document.getElementById("btnLogin"),
  btnLimpar: document.getElementById("btnLimpar"),
  authStatus: document.getElementById("authStatus"),
  btnOrgConsultar: document.getElementById("btnOrgConsultar"),
  orgStatus: document.getElementById("orgStatus"),
  btnEstoqueConsultar: document.getElementById("btnEstoqueConsultar"),
  readStatus: document.getElementById("readStatus"),
  readTableWrap: document.getElementById("readTableWrap"),
  btnExport: document.getElementById("btnExport"),
  exportStatus: document.getElementById("exportStatus"),
  btnMovExecutar: document.getElementById("btnMovExecutar"),
  btnReservaCriar: document.getElementById("btnReservaCriar"),
  btnReservaAcao: document.getElementById("btnReservaAcao"),
  btnReservaExpirar: document.getElementById("btnReservaExpirar"),
  txStatus: document.getElementById("txStatus")
};

const state = {
  token: localStorage.getItem(STORAGE_KEY) || ""
};

const api = new ApiClient(
  () => txt("baseUrl"),
  () => state.token
);

bindOrganizacao({ api, state, elements });
bindEstoque({ api, state, elements });
bindTransacoes({
  api,
  state,
  elements,
  refreshConsultaEstoque: () => elements.btnEstoqueConsultar.click()
});
bindAuth();
syncAuthStatus();

function bindAuth() {
  elements.btnLogin.addEventListener("click", async () => {
    try {
      const login = txt("login");
      const senha = txt("senha");
      if (!txt("baseUrl") || !login || !senha) {
        setStatus(elements.authStatus, "Informe baseUrl, login e senha.", false);
        return;
      }

      const payload = await api.requestJson("/api/auth/login", {
        method: "POST",
        body: { login, senha }
      });

      if (!payload?.success || !payload?.data?.accessToken) {
        setStatus(elements.authStatus, `Falha no login.\n${toPretty(payload)}`, false);
        return;
      }

      state.token = payload.data.accessToken;
      localStorage.setItem(STORAGE_KEY, state.token);
      setStatus(
        elements.authStatus,
        `Token obtido com sucesso.\nExpira em: ${payload.data.expiresAtUtc || "-"}`,
        true
      );
    } catch (error) {
      setStatus(elements.authStatus, `Erro no login.\n${error}`, false);
    }
  });

  elements.btnLimpar.addEventListener("click", () => {
    state.token = "";
    localStorage.removeItem(STORAGE_KEY);
    syncAuthStatus();
  });
}

function syncAuthStatus() {
  if (state.token) {
    setStatus(elements.authStatus, "Token carregado e pronto para uso.", true);
    return;
  }

  setStatus(elements.authStatus, "Sem token.", false);
}
