export function txt(id) {
  return document.getElementById(id).value.trim();
}

export function numOrEmpty(id) {
  const value = txt(id);
  if (!value) return "";
  const number = Number(value);
  return Number.isFinite(number) ? String(number) : "";
}

export function toPretty(value) {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

export function setStatus(element, message, ok) {
  element.className = "status " + (ok ? "ok" : "err");
  element.textContent = message;
}

export function clearElement(element) {
  element.innerHTML = "";
}

export function renderTable(element, rows, preferredColumns) {
  if (!Array.isArray(rows) || rows.length === 0) {
    element.innerHTML = "<div class=\"status\">Sem linhas para exibir.</div>";
    return;
  }

  const columns = preferredColumns.filter((c) =>
    rows.some((r) => Object.prototype.hasOwnProperty.call(r, c))
  );
  if (columns.length === 0) {
    element.innerHTML = "<div class=\"status\">Sem colunas mapeadas para exibir.</div>";
    return;
  }

  const header = columns.map((c) => `<th>${escapeHtml(c)}</th>`).join("");
  const body = rows
    .map((row) => {
      const cells = columns
        .map((column) => `<td>${escapeHtml(formatCellValue(row[column]))}</td>`)
        .join("");
      return `<tr>${cells}</tr>`;
    })
    .join("");

  element.innerHTML = `<table><thead><tr>${header}</tr></thead><tbody>${body}</tbody></table>`;
}

function formatCellValue(value) {
  if (value === null || value === undefined) return "";
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return JSON.stringify(value);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}
