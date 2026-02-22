export class ApiClient {
  constructor(getBaseUrl, getToken) {
    this.getBaseUrl = getBaseUrl;
    this.getToken = getToken;
  }

  async requestJson(path, options = {}) {
    const method = options.method || "GET";
    const query = options.query || null;
    const body = options.body || null;
    const response = await fetch(this.#buildUrl(path, query), {
      method,
      headers: this.#buildHeaders(!!body),
      body: body ? JSON.stringify(body) : undefined
    });

    const text = await response.text();
    const payload = tryParseJson(text);
    if (!response.ok) {
      const detail = payload ? JSON.stringify(payload, null, 2) : text;
      throw new Error(`Status ${response.status}\n${detail}`);
    }

    return payload;
  }

  async requestExport(path, query) {
    const response = await fetch(this.#buildUrl(path, query), {
      headers: this.#buildHeaders(false)
    });

    if (!response.ok) {
      const text = await response.text();
      throw new Error(`Status ${response.status}\n${text}`);
    }

    return {
      blob: await response.blob(),
      fileName: response.headers.get("X-Export-FileName"),
      format: response.headers.get("X-Export-Format"),
      resource: response.headers.get("X-Export-Resource"),
      generatedAtUtc: response.headers.get("X-Export-GeneratedAtUtc")
    };
  }

  #buildUrl(path, query) {
    const baseUrl = normalizeBaseUrl(this.getBaseUrl());
    if (!baseUrl) throw new Error("Base URL obrigatoria.");
    const qs = query && query.toString() ? `?${query.toString()}` : "";
    return `${baseUrl}${path}${qs}`;
  }

  #buildHeaders(includeJsonContentType) {
    const headers = {};
    const token = this.getToken();
    if (token) headers.Authorization = `Bearer ${token}`;
    if (includeJsonContentType) headers["Content-Type"] = "application/json";
    return headers;
  }
}

function normalizeBaseUrl(value) {
  const trimmed = (value || "").trim();
  if (!trimmed) return "";
  return trimmed.endsWith("/") ? trimmed.slice(0, -1) : trimmed;
}

function tryParseJson(text) {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}
