// Shell of the page: navigation bar, hash router and role checks.
import { getSession, clearSession, setUnauthorizedHandler } from "./api.js";
import { h, toast } from "./dom.js";

// Each route: a pattern, the module to load, and who may see it.
// role: null = anyone, "Candidate" / "Employer" = signed in with that role.
const routes = [
  { path: /^\/$/,                              load: () => import("./views/search.js"),         role: null },
  { path: /^\/jobs\/(\d+)$/,                   load: () => import("./views/jobDetail.js"),      role: null },
  { path: /^\/login$/,                         load: () => import("./views/login.js"),          role: null },
  { path: /^\/register$/,                      load: () => import("./views/register.js"),       role: null },
  { path: /^\/applications$/,                  load: () => import("./views/myApplications.js"), role: "Candidate" },
  { path: /^\/employer$/,                      load: () => import("./views/employerJobs.js"),   role: "Employer" },
  { path: /^\/employer\/jobs\/new$/,           load: () => import("./views/jobForm.js"),         role: "Employer" },
  { path: /^\/employer\/jobs\/(\d+)\/edit$/,   load: () => import("./views/jobForm.js"),         role: "Employer" },
  { path: /^\/employer\/jobs\/(\d+)\/applicants$/, load: () => import("./views/applicants.js"), role: "Employer" },
];

const app = document.getElementById("app");
const nav = h("header", { class: "topbar" });
const main = h("main", { id: "view", tabindex: "-1" });
app.append(nav, main);

function drawNav() {
  const session = getSession();
  const links = [h("a", { href: "#/" }, "Jobs")];
  if (session?.role === "Candidate") links.push(h("a", { href: "#/applications" }, "My applications"));
  if (session?.role === "Employer") links.push(h("a", { href: "#/employer" }, "Your jobs"));

  const right = session
    ? [h("span", { class: "who" }, session.displayName),
       h("button", { class: "btn btn-small btn-ghost", onclick: () => { clearSession(); location.hash = "#/"; } }, "Sign out")]
    : [h("a", { href: "#/login" }, "Sign in"), h("a", { class: "btn btn-small", href: "#/register" }, "Sign up")];

  nav.replaceChildren(
    h("div", { class: "topbar-inner" },
      h("a", { class: "brand", href: "#/" }, "Tern"),
      h("nav", { "aria-label": "Main" }, links),
      h("div", { class: "topbar-right" }, right)));
}

// Splits "#/jobs/4?x=1" into a path and its query string.
function parseHash() {
  const raw = location.hash.slice(1) || "/";
  const i = raw.indexOf("?");
  return {
    path: i === -1 ? raw : raw.slice(0, i),
    query: new URLSearchParams(i === -1 ? "" : raw.slice(i + 1)),
    raw,
  };
}

let navigation = 0;

async function route() {
  const mine = ++navigation; // if the user clicks again while this one loads, the old one is dropped
  const { path, query, raw } = parseHash();
  const session = getSession();

  drawNav();

  for (const r of routes) {
    const match = r.path.exec(path);
    if (!match) continue;

    if (r.role && !session) {
      location.hash = "#/login?next=" + encodeURIComponent("#" + raw);
      return;
    }
    if (r.role && session.role !== r.role) {
      toast("That page is for " + r.role.toLowerCase() + " accounts.", "error");
      location.hash = "#/";
      return;
    }

    // every screen draws into its own element, so a slow response can't write over a newer screen
    const view = h("div", { class: "view" });
    try {
      const mod = await r.load();
      if (mine !== navigation) return;
      main.replaceChildren(view);
      await mod.render(view, { params: match.slice(1), query });
    } catch (err) {
      if (mine !== navigation) return;
      view.replaceChildren(
        h("h1", {}, err.status === 404 ? "Not found" : "Something went wrong"),
        h("p", {}, err.status === 404 ? "That page doesn't exist, or it isn't yours." : err.message),
        h("p", {}, h("a", { href: "#/" }, "Back to the jobs")));
      main.replaceChildren(view);
    }
    if (mine === navigation) {
      document.title = (view.querySelector("h1")?.textContent ?? "Jobs") + " | Tern";
      main.focus({ preventScroll: true });
      window.scrollTo(0, 0);
    }
    return;
  }

  main.replaceChildren(h("div", { class: "view" },
    h("h1", {}, "Page not found"),
    h("p", {}, h("a", { href: "#/" }, "Back to the jobs"))));
}

setUnauthorizedHandler(() => { location.hash = "#/login?expired=1"; });
window.addEventListener("hashchange", route);
window.addEventListener("session-change", drawNav);
route();
