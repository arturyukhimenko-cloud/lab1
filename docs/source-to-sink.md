
# Власна карта source-to-sink

Рівень: «Добре».

Поле: Incident.Description.

Стан: початковий, до виправлення XSS.

Vulnerable commit: d491dc6.

Вхід: Alice, локальний браузерний клієнт SecureLab.

Guid інциденту: 20000000-0000-0000-0000-000000000001.

## 1. Потік Incident.Description

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `src/SecureLab.Api/Data/DbSeeder.cs`, `SeedAsync` | `aliceIncident.Description` — «Одержано лист нібито від деканату. Вкладення не відкривалося.» | Початковий запис створено через seed. У звичайній роботі опис може вводити користувач. |
| Request | `src/SecureLab.Api/Client/browser-lab.js`, `load()` | `GET /api/incidents/20000000-0000-0000-0000-000000000001` | Network підтвердив GET 200. Тіло запиту відсутнє, Guid визначає запис. |
| Validation/mapping | `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` | `incident.Description` → `IncidentDetailsResponse` | Сервер передає опис до DTO. Це не робить текст безпечним HTML. |
| Storage | `Incident.Description`, PostgreSQL | `incidents.description` | Збережені користувацькі дані залишаються недовіреними для браузера. |
| Response | `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs`, `IncidentDetailsResponse` | JSON-поле `description` | Фактичне значення підтверджено у Network → Response. |
| Client transform | `src/SecureLab.Api/Client/browser-lab.js`, `load()` | `current = await response.json()` | JSON перетворюється на JavaScript-об'єкт. |
| Sink/context | `src/SecureLab.Api/Client/browser-lab.js`, `load()` | `#description.innerHTML = current.description` | Небезпечний HTML-контекст: браузер може обробити текст як HTML. |

Для створення нового інциденту користувач вводить опис у `textarea` на `browser-lab.html`.

`browser-lab.js` надсилає `description` через POST `/api/incidents`. У `Scaffolding/Lab02Endpoints.cs` сервер отримує `CreateIncidentRequest.Description`, виконує `Trim()`, перевіряє дані та зберігає опис через `Incident.Description` і `SaveChangesAsync()`.

Перевірка введених даних не усуває небезпеку використання `innerHTML` у браузері.

## 2. Потік IncidentComment.Text («Добре»)

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `src/SecureLab.Api/Data/DbSeeder.cs`, `SeedAsync` | `IncidentComment.Text` — «Заголовки листа передано аналітику.» | Навчальний коментар створено через seed. Автор — Morgan, `IsInternal = false`. |
| Request | `src/SecureLab.Api/Client/browser-lab.js`, `load()` | `GET /api/incidents/20000000-0000-0000-0000-000000000001` | Коментар повертається разом з інцидентом. Окремого POST endpoint для коментарів немає. |
| Validation/mapping | `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` | `incident.Comments`, `IsInternal`, `IncidentCommentResponse` | Сервер відбирає доступні коментарі, але не виконує HTML-очищення тексту. |
| Storage | `IncidentComment.Text`, PostgreSQL | `incident_comments.text` | Збережений текст не стає довіреним HTML. |
| Response | `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs`, `IncidentCommentResponse` | JSON-поле `comments[].text` | Фактичний текст підтверджено у Network → Response. |
| Client transform | `src/SecureLab.Api/Client/browser-lab.js`, `load()` | `for (const comment of current.comments ?? [])` | Для кожного коментаря створюється елемент `p`. |
| Sink/context | `src/SecureLab.Api/Client/browser-lab.js`, `load()` | `paragraph.innerHTML = comment.text` | Небезпечний HTML-контекст: браузер може обробити текст коментаря як HTML. |

У вкладці Elements підтверджено відображення коментаря всередині `div#comments` в елементі `p`.

## 3. Інвентаризація DOM sinks

Перевірено власні клієнтські файли в `src/SecureLab.Api/Client/`.

| Місце | Дані | Категорія даних | Контекст | Висновок |
|---|---|---|---|---|
| `browser-lab.js`, `#title.textContent` | `current.title` | Збережене значення користувача | Текстовий вузол | Безпечний спосіб відображення |
| `browser-lab.js`, `#status.textContent` | HTTP-статус відповіді | Значення, сформоване відповіддю сервера | Текстовий вузол | Безпечний спосіб відображення |
| `browser-lab.js`, `#description.innerHTML` | `current.description` | Збережене значення користувача | HTML parser | Потрібне безпечне DOM-відображення |
| `browser-lab.js`, `paragraph.innerHTML` | `comment.text` | Збережене значення коментаря | HTML parser | Потрібне безпечне DOM-відображення |
| `app.js`, `textContent` | Текстові дані штатного клієнта | Залежить від конкретного значення | Текстовий вузол | У перевірених місцях використовується безпечна текстова операція |
| `auth-lab.js`, `textContent` | Статуси й повідомлення входу | Дані застосунку та сервера | Текстовий вузол | У перевірених місцях використовується безпечна текстова операція |
| `auth-lab.js`, `.onclick` | Функції обробки натискань | Код застосунку | Обробник події | Функції задані в коді; рядок користувача як код не виконується |

Пошук `outerHTML`, `insertAdjacentHTML`, `eval`, `new Function`, динамічних URL та рядкових HTML-обробників не показав додаткових відповідних конструкцій у перевірених клієнтських файлах.

## 4. Висновок початкової інвентаризації

Виявлено два потенційно небезпечні місця відображення:

1. `Incident.Description` → `#description.innerHTML`.
2. `IncidentComment.Text` → `paragraph.innerHTML`.

Обидва поля за умовою роботи мають відображатися як звичайний текст, а не форматований HTML.

Для виправлення потрібно використовувати безпечні DOM API та один спільний helper для обох полів.

Практичне відтворення XSS і перевірка після виправлення будуть виконані на наступному етапі.
