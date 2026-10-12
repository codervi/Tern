import { get, post, put } from "../api.js";
import { h, field, toast, TYPE_LABELS } from "../dom.js";

// One form for both "new" and "edit". On edit the job is loaded first.
export async function render(view, ctx) {
  const editId = ctx.params[0] ? Number(ctx.params[0]) : null;
  const [skills, job] = await Promise.all([
    get("/skills"),
    editId ? get(`/employer/jobs/${editId}`) : Promise.resolve(null),
  ]);

  const title = h("input", { type: "text", required: true, minlength: 5, maxlength: 150, value: job?.title ?? "" });
  const place = h("input", { type: "text", required: true, maxlength: 100, value: job?.location ?? "" });
  const remote = h("input", { type: "checkbox", checked: job?.isRemote ?? false });
  const type = h("select", {}, Object.entries(TYPE_LABELS).map(([v, t]) => h("option", { value: v }, t)));
  type.value = job?.employmentType ?? "FullTime";
  const min = h("input", { type: "number", min: 0, max: 10000000, step: 1000, value: job?.salaryMin ?? "" });
  const max = h("input", { type: "number", min: 0, max: 10000000, step: 1000, value: job?.salaryMax ?? "" });
  const currency = h("input", { type: "text", required: true, minlength: 3, maxlength: 3, value: job?.currency ?? "EUR" });
  const days = h("input", { type: "number", min: 1, max: 120, value: editId ? "" : 30 });
  const description = h("textarea", { rows: 10, required: true, minlength: 30, maxlength: 4000 }, job?.description ?? "");

  const picked = new Set(job?.skillIds ?? []);
  const skillBoxes = skills.map((s) => {
    const box = h("input", { type: "checkbox", value: s.id, checked: picked.has(s.id) });
    return h("label", { class: "check" }, box, " ", s.name);
  });

  const error = h("p", { class: "form-error", role: "alert" });

  const form = h("form", {
    class: "form-wide",
    onsubmit: async (e) => {
      e.preventDefault();
      error.textContent = "";

      const skillIds = skillBoxes.map((l) => l.firstChild).filter((b) => b.checked).map((b) => Number(b.value));
      if (skillIds.length > 10) { error.textContent = "Pick at most 10 skills."; return; }
      const lo = min.value === "" ? null : Number(min.value);
      const hi = max.value === "" ? null : Number(max.value);
      if (lo !== null && hi !== null && lo > hi) { error.textContent = "The minimum salary can't be above the maximum."; return; }

      const body = {
        title: title.value.trim(),
        description: description.value.trim(),
        location: place.value.trim(),
        isRemote: remote.checked,
        employmentType: type.value,
        salaryMin: lo,
        salaryMax: hi,
        currency: currency.value.trim().toUpperCase(),
        expiresInDays: days.value === "" ? null : Number(days.value),
        skillIds,
      };

      const button = form.querySelector("button[type=submit]");
      button.disabled = true;
      try {
        if (editId) await put(`/employer/jobs/${editId}`, body);
        else await post("/employer/jobs", body);
        toast(editId ? "Job updated." : "Job posted.");
        location.hash = "#/employer";
      } catch (err) {
        error.textContent = err.message;
        button.disabled = false;
      }
    },
  },
    field("Job title", title),
    h("div", { class: "grid-2" },
      field("Location", place),
      field("Type", type)),
    h("label", { class: "check" }, remote, " Remote friendly"),
    h("div", { class: "grid-3" },
      field("Salary from (per year)", min),
      field("Salary to (per year)", max),
      field("Currency", currency, "Three letters, like EUR")),
    field("Days until it closes", days,
      editId ? "Leave empty to keep the current closing date." : "Between 1 and 120."),
    field("Description", description),
    h("fieldset", {}, h("legend", {}, "Skills (up to 10)"), h("div", { class: "check-grid" }, skillBoxes)),
    error,
    h("div", { class: "actions" },
      h("button", { class: "btn", type: "submit" }, editId ? "Save changes" : "Post job"),
      h("a", { class: "btn btn-ghost", href: "#/employer" }, "Cancel")));

  view.replaceChildren(h("h1", {}, editId ? "Edit job" : "Post a job"), form);
}
