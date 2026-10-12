import { get, post, del } from "../api.js";
import { h, emptyState, toast, fmtDate } from "../dom.js";

function statusOf(job) {
  if (!job.isOpen) return ["Closed", "closed"];
  if (job.isExpired) return ["Expired", "expired"];
  return ["Open", "open"];
}

export async function render(view) {
  const jobs = await get("/employer/jobs");

  async function toggle(job) {
    try {
      await post(`/employer/jobs/${job.id}/${job.isOpen ? "close" : "reopen"}`);
      toast(job.isOpen ? "Job closed." : "Job reopened.");
      await render(view);
    } catch (err) { toast(err.message, "error"); }
  }

  async function remove(job) {
    if (!window.confirm(`Delete "${job.title}"? This can't be undone.`)) return;
    try {
      await del(`/employer/jobs/${job.id}`);
      toast("Job deleted.");
      await render(view);
    } catch (err) { toast(err.message, "error"); }
  }

  function row(job) {
    const [label, css] = statusOf(job);
    return h("tr", {},
      h("td", {}, h("strong", {}, job.title), h("div", { class: "muted" }, job.location)),
      h("td", {}, h("span", { class: "badge badge-" + css }, label)),
      h("td", {}, fmtDate(job.expiresAtUtc)),
      h("td", {}, h("a", { href: `#/employer/jobs/${job.id}/applicants` },
        job.total ? `${job.total} (${job.submitted} new)` : "None")),
      h("td", { class: "actions" },
        h("a", { class: "btn btn-small btn-ghost", href: `#/employer/jobs/${job.id}/edit` }, "Edit"),
        h("button", { class: "btn btn-small btn-ghost", onclick: () => toggle(job) }, job.isOpen ? "Close" : "Reopen"),
        h("button", {
          class: "btn btn-small btn-danger",
          disabled: job.total > 0,
          title: job.total > 0 ? "Jobs with applications can only be closed" : null,
          onclick: () => remove(job),
        }, "Delete")));
  }

  view.replaceChildren(
    h("div", { class: "page-head" },
      h("h1", {}, "Your jobs"),
      h("a", { class: "btn", href: "#/employer/jobs/new" }, "Post a job")),
    jobs.length
      ? h("div", { class: "table-wrap" },
          h("table", {},
            h("thead", {}, h("tr", {}, ["Job", "Status", "Closes", "Applications", ""].map((t) => h("th", { scope: "col" }, t)))),
            h("tbody", {}, jobs.map(row))))
      : emptyState("Nothing posted yet", "Your first listing takes a couple of minutes."));
}
