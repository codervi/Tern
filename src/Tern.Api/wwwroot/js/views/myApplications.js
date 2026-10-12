import { get, del } from "../api.js";
import { h, badge, emptyState, toast, fmtDate } from "../dom.js";

export async function render(view) {
  const items = await get("/applications");

  async function withdraw(a) {
    if (!window.confirm(`Withdraw your application for ${a.jobTitle}?`)) return;
    try {
      await del(`/applications/${a.id}`);
      toast("Application withdrawn.");
      await render(view);
    } catch (err) { toast(err.message, "error"); }
  }

  view.replaceChildren(
    h("h1", {}, "My applications"),
    items.length
      ? h("ul", { class: "rows" }, items.map((a) =>
          h("li", {},
            h("div", {},
              h("a", { href: `#/jobs/${a.jobId}` }, a.jobTitle),
              h("p", { class: "muted" }, `${a.companyName}. Applied ${fmtDate(a.appliedAtUtc)}.`)),
            h("div", { class: "actions" },
              badge(a.status),
              a.canWithdraw ? h("button", { class: "btn btn-small btn-danger", onclick: () => withdraw(a) }, "Withdraw") : null))))
      : emptyState("No applications yet", "Find a job you like and send your first application."),
    h("p", {}, h("a", { href: "#/" }, "Browse jobs")));
}
