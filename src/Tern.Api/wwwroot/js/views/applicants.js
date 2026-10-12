import { get, patch } from "../api.js";
import { h, badge, emptyState, toast, fmtDate, safeLink, statusLabel, STATUS_LABELS } from "../dom.js";

export async function render(view, ctx) {
  const jobId = Number(ctx.params[0]);
  const filter = ctx.query.get("status") ?? "";
  const qs = filter ? `?status=${encodeURIComponent(filter)}` : "";

  const [job, people] = await Promise.all([
    get(`/employer/jobs/${jobId}`),
    get(`/employer/jobs/${jobId}/applications${qs}`),
  ]);

  async function move(person, status) {
    try {
      await patch(`/employer/applications/${person.id}/status`, { status });
      toast(`${person.applicantName} is now: ${statusLabel(status)}.`);
      await render(view, ctx);
    } catch (err) {
      toast(err.message, "error");
      await render(view, ctx); // the list may be out of date
    }
  }

  function card(p) {
    return h("li", { class: "card" },
      h("div", { class: "card-head" },
        h("div", {},
          h("h2", {}, p.applicantName),
          h("p", { class: "muted" }, p.applicantEmail, ` . Applied ${fmtDate(p.appliedAtUtc)}`)),
        badge(p.status)),
      h("p", { class: "letter" }, p.coverLetter),
      p.cvUrl ? h("p", {}, "CV: ", safeLink(p.cvUrl)) : null,
      p.allowedNext.length
        ? h("div", { class: "actions" }, p.allowedNext.map((s) =>
            h("button", { class: "btn btn-small " + (s === "Rejected" ? "btn-danger" : ""), onclick: () => move(p, s) },
              s === "Rejected" ? "Reject" : "Move to " + statusLabel(s))))
        : h("p", { class: "muted" }, "This application is closed."));
  }

  const select = h("select", {
    "aria-label": "Filter by status",
    onchange: () => {
      location.hash = `#/employer/jobs/${jobId}/applicants` + (select.value ? `?status=${select.value}` : "");
    },
  },
    h("option", { value: "" }, "All statuses"),
    Object.entries(STATUS_LABELS).map(([v, t]) => h("option", { value: v }, t)));
  select.value = filter;

  view.replaceChildren(
    h("p", {}, h("a", { href: "#/employer" }, "Back to your jobs")),
    h("div", { class: "page-head" }, h("h1", {}, `Applicants for ${job.title}`), select),
    people.length
      ? h("ul", { class: "cards" }, people.map(card))
      : emptyState(filter ? "No one at this stage" : "No applications yet"));
}
