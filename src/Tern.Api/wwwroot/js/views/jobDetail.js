import { get, post, getSession } from "../api.js";
import { h, field, tags, fmtSalary, fmtDate, timeAgo, toast, safeLink } from "../dom.js";

export async function render(view, ctx) {
  const id = Number(ctx.params[0]);
  const job = await get(`/jobs/${id}`);
  const session = getSession();

  view.replaceChildren(
    h("a", { class: "back", href: "#/" }, "All jobs"),
    h("article", { class: "detail" },
      h("h1", {}, job.title),
      h("p", { class: "job-company" },
        job.company.website ? safeLink(job.company.website, job.company.name) : job.company.name,
        `, ${job.location}`),
      tags(job),
      h("p", { class: "salary" }, fmtSalary(job.salaryMin, job.salaryMax, job.currency)),
      h("p", { class: "muted" }, `Posted ${timeAgo(job.postedAtUtc)}. ${job.acceptingApplications ? "Open until " + fmtDate(job.expiresAtUtc) + "." : ""}`),
      h("div", { class: "description" }, job.description)),
    applyPanel(job, session, () => render(view, ctx)));
}

function applyPanel(job, session, refresh) {
  const panel = h("section", { class: "panel apply" }, h("h2", {}, "Apply"));

  if (!job.acceptingApplications) {
    panel.append(h("p", {}, "This position is no longer accepting applications."));
    return panel;
  }
  if (!session) {
    panel.append(h("p", {},
      h("a", { href: "#/login?next=" + encodeURIComponent("#/jobs/" + job.id) }, "Sign in"),
      " or ",
      h("a", { href: "#/register" }, "create a candidate account"),
      " to apply."));
    return panel;
  }
  if (session.role !== "Candidate") {
    panel.append(h("p", {}, "Employer accounts can't apply to jobs."));
    return panel;
  }
  if (job.hasApplied) {
    panel.append(h("p", {}, "You have applied to this job. ", h("a", { href: "#/applications" }, "See your applications")));
    return panel;
  }

  const letter = h("textarea", { rows: 7, required: true, minlength: 20, maxlength: 3000 });
  const cv = h("input", { type: "url", maxlength: 300, placeholder: "https://" });
  const error = h("p", { class: "form-error", role: "alert" });

  const form = h("form", {
    onsubmit: async (e) => {
      e.preventDefault();
      const button = form.querySelector("button");
      button.disabled = true;
      error.textContent = "";
      try {
        await post("/applications", { jobId: job.id, coverLetter: letter.value, cvUrl: cv.value || null });
        toast("Application sent.");
        refresh();
      } catch (err) {
        error.textContent = err.message;
        button.disabled = false;
      }
    },
  },
    field("Cover letter", letter, "Tell the employer why you fit. At least 20 characters."),
    field("Link to your CV or portfolio (optional)", cv),
    error,
    h("button", { class: "btn", type: "submit" }, "Send application"));

  panel.append(form);
  return panel;
}
