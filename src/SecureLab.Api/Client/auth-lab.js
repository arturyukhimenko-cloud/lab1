
const result = document.querySelector("#result");
const state = document.querySelector("#state");
const profile = document.querySelector("#profile");

async function refreshProfile() {
  profile.textContent = "";
  try {
    const r = await fetch("/api/me", { credentials: "same-origin" });
    if (r.status === 401) { state.textContent = "Не ввійшли"; return 401; }
    if (!r.ok || !r.headers.get("content-type")?.includes("application/json")) {
      state.textContent = "Стан невідомий"; return r.status;
    }
    const user = await r.json();
    state.textContent = "Сеанс активний";
    profile.textContent = `${user.id} | ${user.userName} | ${user.displayName}`;
    return 200;
  } catch { state.textContent = "Стан невідомий"; return "помилка"; }
}

const paths = { register: "/api/auth/register", login: "/api/auth/login", create: "/api/incidents" };
document.querySelectorAll("form").forEach(form => form.onsubmit = async e => {
  e.preventDefault();
  try {
    const body = Object.fromEntries(new FormData(form));
    if (form.id === "create") body.occurredAtUtc = new Date(body.occurredAtUtc).toISOString();
    const r = await fetch(paths[form.id], {
      method: "POST", credentials: "same-origin",
      headers: { "Content-Type": "application/json" }, body: JSON.stringify(body)
    });
    result.textContent = `${form.id}: ${r.status}`;
    if (form.id === "login") await refreshProfile();
    if (r.ok && form.id !== "login") form.reset();
    if (r.status === 201 && form.id === "register") result.textContent += " — тепер увійдіть";
  } catch {
    result.textContent = `${form.id}: помилка`;
    if (form.id === "login") await refreshProfile();
  } finally {
    if (form.elements.password) form.elements.password.value = "";
  }
});

document.querySelector("#me").onclick = async () => result.textContent = `/api/me: ${await refreshProfile()}`;
document.querySelector("#logout").onclick = async () => {
  try {
    const r = await fetch("/api/auth/logout", { method: "POST", credentials: "same-origin" });
    result.textContent = `Logout: ${r.status}; /api/me: ${await refreshProfile()}`;
  } catch { profile.textContent = ""; state.textContent = "Стан невідомий"; result.textContent = "Помилка виходу"; }
};
refreshProfile();
