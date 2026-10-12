// Helpers for building the page. Everything is made with createElement and text nodes,
// nothing from the server is ever put into innerHTML.

export function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  let value;
  for (const [key, val] of Object.entries(attrs ?? {})) {
    if (val === null || val === undefined || val === false) continue;
    if (key === "class") el.className = val;
    else if (key === "value") value = val;
    else if (key.startsWith("on") && typeof val === "function") el.addEventListener(key.slice(2), val);
    else el.setAttribute(key, val === true ? "" : String(val));
  }
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  if (value !== undefined) el.value = value; // last, so a <select> already has its options
  return el;
}

let nextId = 0;
export function field(label, input, hint) {
  const id = "field-" + ++nextId;
  input.id = id;
  return h("div", { class: "field" },
    h("label", { for: id }, label),
    input,
    hint ? h("p", { class: "hint" }, hint) : null);
}

export function toast(message, kind = "ok") {
  const box = document.getElementById("toasts");
  const el = h("div", { class: "toast toast-" + kind }, message);
  box.append(el);
  setTimeout(() => el.remove(), 4000);
}

export function emptyState(title, text) {
  return h("div", { class: "empty" }, h("p", { class: "empty-title" }, title), text ? h("p", {}, text) : null);
}

export function pager(page, totalPages, hrefFor) {
  if (totalPages <= 1) return h("div");
  return h("nav", { class: "pager", "aria-label": "Pages" },
    page > 1 ? h("a", { class: "btn btn-ghost btn-small", href: hrefFor(page - 1) }, "Previous") : null,
    h("span", {}, `Page ${page} of ${totalPages}`),
    page < totalPages ? h("a", { class: "btn btn-ghost btn-small", href: hrefFor(page + 1) }, "Next") : null);
}

// Only http and https links are ever turned into <a> elements.
export function safeLink(url, text) {
  try {
    const u = new URL(url);
    if (u.protocol === "https:" || u.protocol === "http:") {
      return h("a", { href: u.href, target: "_blank", rel: "noopener noreferrer" }, text ?? u.href);
    }
  } catch { /* not a URL */ }
  return h("span", {}, text ?? url);
}

// ---- labels and formats ----
export const TYPE_LABELS = { FullTime: "Full time", PartTime: "Part time", Contract: "Contract", Internship: "Internship" };
export const STATUS_LABELS = { Submitted: "Submitted", InReview: "In review", Interview: "Interview", Offer: "Offer", Hired: "Hired", Rejected: "Rejected" };

export const typeLabel = (t) => TYPE_LABELS[t] ?? t;
export const statusLabel = (s) => STATUS_LABELS[s] ?? s;

export function badge(status) {
  return h("span", { class: "badge badge-" + String(status).toLowerCase() }, statusLabel(status));
}

export function parseUtc(s) {
  return new Date(/(Z|[+-]\d\d:\d\d)$/.test(s) ? s : s + "Z");
}

const rtf = new Intl.RelativeTimeFormat("en", { numeric: "auto" });
export function timeAgo(utc) {
  const days = Math.floor((Date.now() - parseUtc(utc).getTime()) / 86400000);
  if (days < 7) return rtf.format(-days, "day");
  if (days < 60) return rtf.format(-Math.floor(days / 7), "week");
  return rtf.format(-Math.floor(days / 30), "month");
}

const dateFmt = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric" });
export const fmtDate = (utc) => dateFmt.format(parseUtc(utc));

export function fmtSalary(min, max, currency) {
  let money;
  try {
    const nf = new Intl.NumberFormat("en-GB", { style: "currency", currency, maximumFractionDigits: 0 });
    money = (n) => nf.format(n);
  } catch {
    money = (n) => `${n} ${currency}`;
  }
  if (min && max) return `${money(min)} to ${money(max)} a year`;
  if (max) return `Up to ${money(max)} a year`;
  if (min) return `From ${money(min)} a year`;
  return "Salary not listed";
}

export function tags(job) {
  return h("ul", { class: "tags", "aria-label": "Details" },
    h("li", { class: "tag tag-type" }, typeLabel(job.employmentType)),
    job.isRemote ? h("li", { class: "tag tag-remote" }, "Remote") : null,
    (job.skills ?? []).map((s) => h("li", { class: "tag" }, s)));
}
