import { post, setSession } from "../api.js";
import { h, field } from "../dom.js";

export function render(view, { query }) {
  const email = h("input", { type: "email", required: true, autocomplete: "username" });
  const password = h("input", { type: "password", required: true, autocomplete: "current-password" });
  const error = h("p", { class: "form-error", role: "alert" },
    query.get("expired") ? "Your session ended. Please sign in again." : "");

  const form = h("form", {
    class: "form-narrow",
    onsubmit: async (e) => {
      e.preventDefault();
      const button = form.querySelector("button");
      button.disabled = true;
      error.textContent = "";
      try {
        const auth = await post("/auth/login", { email: email.value, password: password.value });
        setSession(auth);
        const next = query.get("next");
        // only local hash links are followed, never an address from outside
        location.hash = next && next.startsWith("#/") ? next : auth.role === "Employer" ? "#/employer" : "#/";
      } catch (err) {
        error.textContent = err.message;
        password.value = "";
        button.disabled = false;
      }
    },
  },
    field("Email", email),
    field("Password", password),
    error,
    h("button", { class: "btn", type: "submit" }, "Sign in"));

  view.replaceChildren(
    h("h1", {}, "Sign in"),
    form,
    h("p", {}, "New here? ", h("a", { href: "#/register" }, "Create an account")));
}
