import { numOrEmpty, setStatus, toPretty, txt } from "./ui.js";

export function bindOrganizacao(context) {
  const { api, state, elements } = context;

  elements.btnOrgConsultar.addEventListener("click", async () => {
    if (!state.token) {
      setStatus(elements.orgStatus, "Realize login antes de consultar organizacao.", false);
      return;
    }

    try {
      const action = txt("orgAction");
      const idOrganizacao = numOrEmpty("orgId");
      const query = new URLSearchParams();
      let path = "/api/organizacao/contexto";

      if (action === "estrutura") path = "/api/organizacao/estrutura";
      if (action === "arvore") path = "/api/organizacao/estrutura/arvore";
      if (idOrganizacao && action !== "contexto") query.set("idOrganizacao", idOrganizacao);

      const payload = await api.requestJson(path, { query });
      setStatus(elements.orgStatus, `Consulta concluida.\n${toPretty(payload)}`, true);
    } catch (error) {
      setStatus(elements.orgStatus, `Falha na consulta de organizacao.\n${error}`, false);
    }
  });
}
