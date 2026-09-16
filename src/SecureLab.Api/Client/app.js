const listElement = document.querySelector("#incident-list");
const listStatusElement = document.querySelector("#list-status");
const detailsElement = document.querySelector("#incident-details");
const filterForm = document.querySelector("#filter-form");
const summaryButton = document.querySelector("#summary-button");
const summaryStatusElement = document.querySelector("#summary-status");
const summaryListElement = document.querySelector("#summary-list");

async function apiFetch(path, options = {}) {
  const response = await fetch(path, {
    headers: { Accept: "application/json", ...options.headers },
    ...options,
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.title ?? `HTTP ${response.status}`);
  }

  return response.json();
}

function createTextElement(tagName, text, className) {
  const element = document.createElement(tagName);
  element.textContent = text;
  if (className) {
    element.className = className;
  }
  return element;
}

function renderIncidentList(incidents) {
  listElement.replaceChildren();

  if (incidents.length === 0) {
    listStatusElement.textContent = "За заданим фільтром інцидентів немає.";
    return;
  }

  listStatusElement.textContent = `Знайдено: ${incidents.length}`;
  for (const incident of incidents) {
    const item = document.createElement("li");
    const button = document.createElement("button");
    button.type = "button";
    button.className = "incident-card";
    button.append(
      createTextElement("strong", incident.title),
      createTextElement("span", `${incident.severity} · ${incident.status}`, "metadata"),
    );
    button.addEventListener("click", () => loadIncidentDetails(incident.id));
    item.append(button);
    listElement.append(item);
  }
}

function renderIncidentDetails(incident) {
  const heading = createTextElement("h3", incident.title);
  const metadata = createTextElement(
    "p",
    `${incident.severity} · ${incident.status} · автор: ${incident.ownerDisplayName}`,
    "metadata",
  );
  const description = createTextElement("p", incident.description);
  const commentsHeading = createTextElement("h4", "Коментарі");
  const comments = document.createElement("ul");

  for (const comment of incident.comments) {
    const item = document.createElement("li");
    item.append(
      createTextElement("strong", `${comment.authorDisplayName}: `),
      document.createTextNode(comment.text),
    );
    comments.append(item);
  }

  if (incident.comments.length === 0) {
    comments.append(createTextElement("li", "Коментарів немає."));
  }

  detailsElement.className = "";
  detailsElement.replaceChildren(heading, metadata, description, commentsHeading, comments);
}

async function loadIncidents() {
  listStatusElement.textContent = "Завантаження…";
  listElement.replaceChildren();

  const status = new FormData(filterForm).get("status");
  const query = status ? `?status=${encodeURIComponent(status)}` : "";

  try {
    renderIncidentList(await apiFetch(`/api/incidents${query}`));
  } catch (error) {
    listStatusElement.textContent = `Помилка: ${error.message}`;
  }
}

async function loadIncidentDetails(id) {
  detailsElement.className = "details-placeholder";
  detailsElement.textContent = "Завантаження…";

  try {
    renderIncidentDetails(await apiFetch(`/api/incidents/${encodeURIComponent(id)}`));
  } catch (error) {
    detailsElement.textContent = `Помилка: ${error.message}`;
  }
}

async function loadSeveritySummary() {
  summaryListElement.replaceChildren();
  summaryStatusElement.textContent = "Завантаження…";

  try {
    // Враховуємо обраний фільтр статусу з форми
    const status = filterForm ? new FormData(filterForm).get("status") : "";
    const query = status ? `?status=${encodeURIComponent(status)}` : "";

    const summaries = await apiFetch(`/api/incidents/severity-summary${query}`);

    // Підраховуємо загальну кількість інцидентів у масиві
    const totalCount = summaries.reduce((acc, item) => acc + item.count, 0);

    if (summaries.length === 0 || totalCount === 0) {
      summaryStatusElement.textContent = "Даних немає.";
      return;
    }

    summaryStatusElement.textContent = `Груп: ${summaries.length}`;
    for (const summary of summaries) {
      const item = document.createElement("li");
      item.textContent = `${summary.severity}: ${summary.count}`;
      summaryListElement.append(item);
    }
  } catch (error) {
    summaryStatusElement.textContent = "Не вдалося завантажити підсумок. Спробуйте пізніше.";
  }
}

summaryButton?.addEventListener("click", loadSeveritySummary);

filterForm.addEventListener("submit", (event) => {
  event.preventDefault();
  loadIncidents();
});

loadIncidents();