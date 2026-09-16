# Карта архітектури

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити, безпечно показує відповідь через DOM API |
| Presentation | `Presentation/` | Описує endpoints, читає зовнішні параметри, формує HTTP-відповідь |
| Application | `Application/` | Виконує сценарій отримання списку, деталей інциденту або підсумку за severity |
| Data | `Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері |

## Наскрізний маршрут: деталі інциденту

    клік по картці інциденту у Client/app.js
      → GET /api/incidents/{id}
      → Presentation/Endpoints/IncidentEndpoints.cs (GetDetailsAsync)
      → Application/Incidents/IncidentQueries.cs (GetDetailsAsync)
      → Data/SecureLabDbContext.cs (DbSet<Incident>)
      → PostgreSQL: таблиця incidents
      → IncidentDetailsResponse
      → JSON
      → textContent/createTextNode у Client/app.js

## Наскрізний маршрут: підсумок за severity (реалізовано в ЛР 1)

    клік по кнопці "Показати підсумок" у Client/index.html
      → обробник loadSeveritySummary у Client/app.js
      → GET /api/incidents/severity-summary
      → Presentation/Endpoints/IncidentEndpoints.cs (GetSeveritySummaryAsync)
      → Application/Incidents/IncidentQueries.cs (GetSeveritySummaryAsync)
      → Data/SecureLabDbContext.cs (DbSet<Incident>, GroupBy Severity)
      → PostgreSQL: таблиця incidents
      → IncidentSeveritySummaryResponse (масив)
      → JSON
      → textContent у Client/app.js

**Політика нульових груп**: обрано варіант "повний перелік рівнів" — після агрегації з БД (`GroupBy` + `Count()`) результат доповнюється значеннями `Enum.GetValues<IncidentSeverity>()`, тому відповідь завжди містить усі 4 рівні критичності (`Low`, `Medium`, `High`, `Critical`), навіть якщо якийсь із них відсутній у даних (у такому разі `count: 0`).

**Порядок елементів**: сталий лексикографічний порядок за назвою severity (`OrderBy(x => x.Severity)`), оскільки `Severity` зберігається в базі як текст (`HasConversion<string>()`).

## Межі довіри

| Межа | Дані, що її перетинають | Хибне припущення | Контроль у дослідженому маршруті |
|---|---|---|---|
| браузер → API | URL-параметри запиту (ідентифікатор `{id}`) | Що вхідний параметр завжди є валідним UUID | Маршрутне обмеження `:guid` у Minimal API (`IncidentEndpoints.cs`) |
| API → PostgreSQL | LINQ-запит до бази даних (id, GroupBy) | Що прочитані з БД дані потребують механізму відстеження змін | Використання оптимізації `.AsNoTracking()` у `IncidentQueries.cs` |
| API → браузер | JSON-відповідь (`IncidentDetailsResponse`, `IncidentSeveritySummaryResponse`) | Що клієнту потрібні внутрішні службові поля чи чутливі дані | Проєкція даних (`Select`), що відсікає зайві поля (наприклад, `OwnerUserId` чи email) |
| дані response → DOM | Поля `title`, `description` із відповіді сервера | Що текстові поля від сервера є безпечним HTML-кодом і не містять шкідливих скриптів | Виведення через властивість `textContent` у `app.js`, що екранує спецсимволи |

## Конфігураційні входи

- `global.json` — версія .NET SDK;
- `src/SecureLab.Api/appsettings*.json` — режим міграцій і локальний connection string;
- `infra/compose.yaml` — версія PostgreSQL, порт і локальні навчальні облікові дані;
- змінна середовища `ConnectionStrings__SecureLab` — безпечний спосіб перевизначити connection string поза репозиторієм.

## Відновлення відомого стану

    dotnet run --no-build --project src/SecureLab.Api -- --reset-database

Команда очищує лише відомі навчальні таблиці й повторно заповнює їх seed-даними; працює лише в Development environment.