# Архітектура системи (SecureLab - Лабораторна робота 1)

## 1. Компоненти системи
- **Browser Client**: Вебклієнт на HTML/JS у `src/SecureLab.Api/Client/` для взаємодії з інтерфейсом.
- **ASP.NET Core API**: Бекенд-додаток на .NET 10, що реалізує Minimal API ендпоінти.
- **EF Core**: ORM для мапінгу даних і виконання LINQ-запитів до бази даних.
- **PostgreSQL у Docker Compose**: Ізольована реляційна база даних, що розгортається через `infra/compose.yaml`.

## 2. Вибраний маршрут (Severity Summary)
- **Метод і URL**: `GET /api/incidents/severity-summary`
- **Ланцюжок викликів**:
  кнопка summary у `Client/index.html`
  → handler у `Client/app.js`
  → `GET /api/incidents/severity-summary`
  → `IncidentEndpoints.cs`
  → `IncidentQueries.cs` (`GetSeveritySummaryAsync`)
  → `SecureLabDbContext.Incidents` / таблиця `incidents`
  → `IncidentSeveritySummaryResponse` (DTO) як JSON
  → виведення через `textContent` у `Client/app.js`

## 3. Ключові файли
- **Клієнт**: `src/SecureLab.Api/Client/index.html`, `src/SecureLab.Api/Client/app.js`
- **Endpoint**: `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs`
- **Application Layer**: `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`
- **DTO**: `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs`
- **DbContext і таблиця**: `src/SecureLab.Api/Infrastructure/Persistence/SecureLabDbContext.cs` (таблиця `incidents`)

## 4. Межі довіри та дані
| Межа або перехід | Дані, що її перетинають | Контроль / Захист |
| :--- | :--- | :--- |
| **Браузер → API** | URL-параметри, шляхи запитів та HTTP-запити | Валідація маршрутів у Minimal API |
| **API → PostgreSQL** | LINQ-запити через EF Core | Використання `.AsNoTracking()`, параметризовані запити |
| **API → Браузер** | JSON-відповідь (DTO без чутливих полів) | Проєкція даних (`Select`), захист від XSS через `textContent` |

## 5. Конфігураційні входи
- `global.json`: фіксація версії SDK.
- `appsettings*.json`: конфігурація логування та параметрів середовища.
- `infra/compose.yaml`: конфігурація контейнера PostgreSQL та volume.
- Змінна середовища `ConnectionStrings__SecureLab`: рядок підключення до БД (без збереження реальних паролів/секретів у відкритому вигляді).

## 6. Спосіб повернення до відомого seed-стану
Скидання бази даних та повернення до початкового seed-стану виконується шляхом перестворення контейнера Docker Compose із залученням вбудованих міграцій та скриптів ініціалізації бази даних згідно з інструкцією проєкту.