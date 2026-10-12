import { get } from "../api.js";
import { h, field, emptyState, pager, tags, fmtSalary, timeAgo, TYPE_LABELS } from "../dom.js";

let skillsCache = null;
async function loadSkills() {
  if (!skillsCache) skillsCache = await get("/skills");
  return skillsCache;
}

function go(hash) {
  // Same hash again would not fire "hashchange", so searching twice for the same thing still refreshes.
  if (location.hash === hash) window.dispatchEvent(new Event("hashchange"));
  else location.hash = hash;
}

export async function render(view, { query }) {
  const skills = await loadSkills();

  const q = h("input", { type: "search", maxlength: 100, placeholder: "Job title, company or keyword", value: query.get("q") ?? "" });
  const place = h("input", { type: "text", maxlength: 100, placeholder: "City or country", value: query.get("location") ?? "" });
  const type = h("select", { value: query.get("type") ?? "" },
    h("option", { value: "" }, "Any type"),
    Object.entries(TYPE_LABELS).map(([value, label]) => h("option", { value }, label)));
  const remote = h("input", { type: "checkbox", checked: query.get("remote") === "true" });
  const minSalary = h("input", { type: "number", min: 0, step: 5000, placeholder: "For example 50000", value: query.get("minSalary") ?? "" });
  const sort = h("select", { value: query.get("sort") ?? "newest" },
    h("option", { value: "newest" }, "Newest first"),
    h("option", { value: "salary" }, "Highest salary first"));

  const picked = new Set(query.getAll("skill").map(Number));
  const skillBoxes = skills.map((s) => ({ s, box: h("input", { type: "checkbox", value: s.id, checked: picked.has(s.id) }) }));

  const form = h("form", {
    class: "filters",
    onsubmit: (e) => {
      e.preventDefault();
      const p = new URLSearchParams();
      if (q.value.trim()) p.set("q", q.value.trim());
      if (place.value.trim()) p.set("location", place.value.trim());
      if (type.value) p.set("type", type.value);
      if (remote.checked) p.set("remote", "true");
      if (minSalary.value) p.set("minSalary", minSalary.value);
      if (sort.value !== "newest") p.set("sort", sort.value);
      for (const { s, box } of skillBoxes) if (box.checked) p.append("skill", s.id);
      const text = p.toString();
      go(text ? "#/?" + text : "#/");
    },
  },
    field("Search", q),
    field("Location", place),
    field("Type", type),
    h("label", { class: "check" }, remote, " Remote jobs only"),
    field("Minimum yearly salary", minSalary, "Jobs whose top salary reaches this amount"),
    h("fieldset", { class: "field" },
      h("legend", {}, "Skills"),
      h("p", { class: "hint" }, "Jobs must ask for all the skills you tick."),
      h("div", { class: "skill-list" }, skillBoxes.map(({ s, box }) => h("label", { class: "check" }, box, " ", s.name)))),
    field("Sort by", sort),
    h("div", { class: "form-actions" },
      h("button", { class: "btn", type: "submit" }, "Search"),
      h("a", { class: "btn btn-ghost", href: "#/" }, "Clear")));

  const results = h("section", { class: "results", "aria-live": "polite" });

  view.replaceChildren(
    h("div", { class: "intro" },
      h("h1", {}, "Find a developer job"),
      h("p", { class: "lead" }, "A small job board for people who write code, built as a portfolio project.")),
    h("div", { class: "search-layout" }, h("aside", {}, form), results));

  // ----- ask the API -----
  const params = new URLSearchParams();
  for (const key of ["q", "location", "type", "remote", "minSalary", "sort"]) {
    if (query.get(key)) params.set(key, query.get(key));
  }
  for (const id of picked) params.append("skill", id);
  params.set("page", query.get("page") ?? "1");
  params.set("pageSize", "10");

  const res = await get("/jobs?" + params);

  const hrefFor = (page) => {
    const p = new URLSearchParams(query);
    p.set("page", page);
    return "#/?" + p;
  };

  results.replaceChildren(
    h("p", { class: "count" }, `${res.totalCount} ${res.totalCount === 1 ? "job" : "jobs"} found`),
    res.items.length
      ? h("ul", { class: "jobs" }, res.items.map(jobRow))
      : emptyState("No jobs match your search", "Try fewer filters or a different keyword."),
    pager(res.page, res.totalPages, hrefFor));
}

function jobRow(j) {
  return h("li", { class: "job" },
    h("div", { class: "job-main" },
      h("h2", {}, h("a", { href: `#/jobs/${j.id}` }, j.title)),
      h("p", { class: "job-company" }, `${j.companyName}, ${j.location}`),
      tags(j)),
    h("div", { class: "job-side" },
      h("p", { class: "salary" }, fmtSalary(j.salaryMin, j.salaryMax, j.currency)),
      h("p", { class: "muted" }, "Posted " + timeAgo(j.postedAtUtc))));
}
