// Fetch wrapper plus the signed-in session.
//
// The JWT is kept in sessionStorage, so it disappears when the tab is closed and it is
// sent in the Authorization header. That is the usual JWT setup for a single page app.
// The trade-off: JavaScript can read it, so any XSS bug would expose it. That is why the
// pages are built with textContent / createElement and never with innerHTML.

const KEY = "tern.session";

export function getSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem(KEY));
    if (session && new Date(session.expiresAtUtc.endsWith("Z") ? session.expiresAtUtc : session.expiresAtUtc + "Z") > new Date()) {
      return session;
    }
  } catch { /* nothing stored, or storage is blocked */ }
  return null;
}

export function setSession(auth) {
  try { sessionStorage.setItem(KEY, JSON.stringify(auth)); } catch { /* storage blocked */ }
  window.dispatchEvent(new Event("session-change"));
}

export function clearSession() {
  try { sessionStorage.removeItem(KEY); } catch { /* storage blocked */ }
  window.dispatchEvent(new Event("session-change"));
}

export class ApiError extends Error {
  constructor(status, problem) {
    super(messageFrom(status, problem));
    this.status = status;
  }
}

function messageFrom(status, problem) {
  if (problem?.errors) {
    const first = Object.values(problem.errors).flat()[0];
    if (first) return first;
  }
  if (problem?.detail) return problem.detail;
  if (problem?.title) return problem.title;
  if (status === 429) return "Too many attempts. Wait a minute and try again.";
  return "Something went wrong. Please try again.";
}

let onUnauthorized = () => {};
export function setUnauthorizedHandler(fn) { onUnauthorized = fn; }

export async function api(method, url, body) {
  const headers = { Accept: "application/json" };
  const session = getSession();
  if (session) headers.Authorization = "Bearer " + session.token;

  const options = { method, headers };
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch("/api" + url, options);
  } catch {
    throw new ApiError(0, { detail: "Can't reach the server. Check your connection." });
  }

  if (response.status === 204) return null;

  const text = await response.text();
  let data = null;
  if (text) {
    try { data = JSON.parse(text); } catch { /* not JSON */ }
  }

  if (!response.ok) {
    // A 401 on a call that carried a token means the token expired or was rejected.
    if (response.status === 401 && session && !url.startsWith("/auth/")) {
      clearSession();
      onUnauthorized();
    }
    throw new ApiError(response.status, data);
  }
  return data;
}

export const get = (url) => api("GET", url);
export const post = (url, body) => api("POST", url, body);
export const put = (url, body) => api("PUT", url, body);
export const patch = (url, body) => api("PATCH", url, body);
export const del = (url) => api("DELETE", url);
