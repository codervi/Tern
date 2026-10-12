import { post, setSession } from "../api.js";
import { h, field } from "../dom.js";

export function render(view) {
  const name = h("input", { type: "text", required: true, maxlength: 100, autocomplete: "name" });
  const email = h("input", { type: "email", required: true, autocomplete: "username" });
  const password = h("input", { type: "password", required: true, minlength: 10, autocomplete: "new-password" });
  const company = h("input", { type: "text", maxlength: 120 });
  const candidate = h("input", { type: "radio", name: "account", value: "Candidate", checked: true });
  const employer = h("input", { type: "radio", name: "account", value: "Employer" });
  const error = h("p", { class: "form-error", role: "alert" });

  const companyField = field("Company name", company);
  companyField.hidden = true;
  const sync = () => { companyField.hidden = !employer.checked; company.required = employer.checked; };
  candidate.addEventListener("change", sync);
  employer.addEventListener("change", sync);

  const form = h("form", {
    class: "form-narrow",
    onsubmit: async (e) => {
      e.preventDefault();
      const button = form.querySelector("button[type=submit]");
      button.disabled = true;
      error.textContent = "";
      try {
        const auth = await post("/auth/register", {
          email: email.value,
          password: password.value,
          displayName: name.value,
          accountType: employer.checked ? "Employer" : "Candidate",
          companyName: employer.checked ? company.value : null,
        });
        setSession(auth);
        location.hash = auth.role === "Employer" ? "#/employer" : "#/";
      } catch (err) {
        error.textContent = err.message;
        button.disabled = false;
      }
    },
  },
    h("fieldset", { class: "field" },
      h("legend", {}, "I am"),
      h("label", { class: "check" }, candidate, " Looking for a job"),
      h("label", { class: "check" }, employer, " Hiring")),
    field("Your name", name),
    companyField,
    field("Email", email),
    field("Password", password, "At least 10 characters, with upper and lower case letters, a number and a symbol."),
    error,
    h("button", { class: "btn", type: "submit" }, "Create account"));

  view.replaceChildren(
    h("h1", {}, "Create an account"),
    form,
    h("p", {}, "Already registered? ", h("a", { href: "#/login" }, "Sign in")));
}
