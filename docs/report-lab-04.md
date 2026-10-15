
# Звіт ЛР04

## Контекст і межа виконання

- Рівень виконання: добрий («Добре»).
- Предметна область: Incident, baseline 2-A.
- Початковий тег: v0.3.0.
- Початковий commit: 4636f8a.
- Робоча гілка: lab/4-authorization.
- Головна операція: GET /api/incidents/{id}.
- Обрана політика відмови для чужого ресурсу: 403 Forbidden.
- Операції рівня «Добре»: list, details, create, update, delete, triage та доступ до дочірніх коментарів.
- Репозиторій: https://github.com/arturyukhimenko-cloud/lab1
- Видимість репозиторію: Private.
- Vulnerable commit: 65f3ce67a0c30e14fedbe4d14d7e20e025cebf11.
- Fixed commit: 42e0921.
- Підсумковий анотований тег: v0.4.0.

## Матриця і server-side рішення

Матриця контролю доступу наведена у файлі `docs/access-matrix.md`.

Визначено правила доступу для Reporter, Analyst та Administrator з урахуванням власника ресурсу, ролі користувача й поточного статусу інциденту.

Для чужого ресурсу обрано політику 403 Forbidden. Для неіснуючого ресурсу передбачено 404 Not Found.

Серверні перевірки доступу централізовано у файлі `IncidentEndpoints.cs` через спільний метод `CanAccessIncident`.

Метод приймає автентифікованого користувача, його ідентифікатор, ідентифікатор власника ресурсу, дію та ознаку внутрішнього коментаря.

Він використовується у п'яти операціях:

- GetDetailsAsync — перегляд деталей інциденту.
- UpdateIncidentAsync — оновлення інциденту.
- DeleteIncidentAsync — видалення інциденту.
- GetIncidentCommentAsync — перегляд дочірнього коментаря.
- TriageIncidentAsync — привілейована зміна статусу.

Для перегляду списку використовується обмеження запиту за власником ресурсу у `IncidentQueries.cs`.

Автентифікація визначає користувача через серверний principal. Дані `role`, `ownerUserId` і `status`, передані клієнтом, не використовуються як підстава для надання прав.

Докази централізації:

- 75_Centralized_Authorization.png — спільний метод перевірки.
- 76_Centralized_Authorization_Calls.png — п'ять місць його використання.
- 77_Overposting_Protection.png — захищене оновлення та DTO.
- 78_Triage_Authorization_And_History.png — перевірка triage, аудит та історія.
- 81_Authorization_After_Fix.png — перевірки details, delete і comments.

## Вразливий стан і PoC

Для перевірки IDOR/BOLA використано локальний навчальний застосунок SecureLab та облікові записи Alice і Bob.

### Доступ Alice до власного інциденту

Виконано вхід під обліковим записом Alice та запит на перегляд власного інциденту.

Команда:

`py -3 ../lr04-resources/commands/login-and-read.py --user alice --incident alice`

Очікуваний результат: 200 OK.

Фактичний результат:

- login_status=204
- read_status=200
- ID інциденту: 20000000-0000-0000-0000-000000000001
- Назва: Підозрілий лист із вкладенням
- Власник: Аліса Коваль

Alice успішно отримала доступ до власного інциденту.

Доказ: `11_Alice_Own_Incident.png`.

### Доступ Alice до інциденту Bob

Від імені Alice виконано запит на перегляд інциденту Bob.

Команда:

`py -3 ../lr04-resources/commands/login-and-read.py --user alice --incident bob`

Очікуваний результат за матрицею: 403 Forbidden.

Фактичний результат у вразливому стані:

- login_status=204
- read_status=200
- ID інциденту: 20000000-0000-0000-0000-000000000002
- Назва: Невідома спроба входу
- Власник: Боб Мельник

Сервер повернув Alice дані чужого інциденту, хоча вона не була його власником.

Отриманий результат підтверджує вразливість IDOR/BOLA: автентифікований користувач отримав доступ до чужого ресурсу через підстановку його ідентифікатора.

Доказ: `12_IDOR_Before.png`.

Vulnerable commit hash: `65f3ce67a0c30e14fedbe4d14d7e20e025cebf11`.

### Аналіз вразливого коду

У вразливому стані метод `GetDetailsAsync` отримував інцидент за його ідентифікатором без перевірки права поточного користувача на доступ до ресурсу.

Аналогічна проблема існувала в `UpdateIncidentAsync`: перевірка автентифікації не гарантувала права змінювати конкретний інцидент.

Вразливий код збережено в окремому Git-коміті.

Команда перегляду:

`git show 65f3ce6:src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs`

Доказ: `80_Vulnerable_Code_Before.png`.

## Виправлення та retest

Для усунення IDOR/BOLA реалізовано серверну перевірку прав доступу до конкретного інциденту.

Правила враховують роль автентифікованого користувача, власника ресурсу та дозволену дію.

Перевірка доступу виконується до повернення захищених даних або зміни стану інциденту.

До виправлення Alice могла прочитати інцидент Bob — HTTP 200 OK.

Після виправлення отримано такі результати:

- Alice читає власний інцидент — HTTP 200 OK.
- Alice не може прочитати інцидент Bob — HTTP 403 Forbidden.
- Morgan (Analyst) може прочитати інцидент Alice — HTTP 200 OK.
- Alice бачить лише власні інциденти у списку.
- Morgan може переглядати інциденти для аналізу.
- Недозволені UPDATE і DELETE повертають HTTP 403 Forbidden.
- Після заборонених операцій дані інциденту залишаються незмінними.
- Анонімний GET захищеного ресурсу повертає HTTP 401 Unauthorized.
- Анонімний POST /api/incidents повертає HTTP 401 Unauthorized.

Для авторизації використано спільний метод `CanAccessIncident`.

Докази:

- `22_Alice_Own_After.png`
- `23_IDOR_Read_After.png`
- `24_Analyst_Read_After.png`
- `25_Alice_Filtered_List.png`
- `26_Analyst_Full_List.png`
- `29_A04_Forbidden_Update.png`
- `30_A04_Bob_Unchanged.png`
- `31_Anonymous_Details_After.png`
- `41_Update_Delete_Final_Retest.png`
- `74_Anonymous_POST_401.png`
- `75_Centralized_Authorization.png`
- `76_Centralized_Authorization_Calls.png`
- `81_Authorization_After_Fix.png`

## Доказова таблиця

| ID | Рівень | Сценарій | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| A-01 | Усі | Анонімний доступ | 401, без redirect | GET і POST повертають 401 | A01_Anonymous_CannotAccessProtectedIncidents; 74_Anonymous_POST_401.png |
| A-02 | Достатній | Власний ресурс Alice | 200 OK | 200 OK | A02_Alice_CanReadOwnIncident; 22_Alice_Own_After.png |
| A-03 | Достатній | Чужий ресурс Bob | 403 Forbidden | До виправлення 200, після — 403 | A03_Alice_CannotReadBobIncident; 12_IDOR_Before.png; 23_IDOR_Read_After.png |
| A-03R | Достатній | Analyst читає ресурс Alice | 200 OK | 200 OK | A03R_Analyst_CanReadAliceIncident; 24_Analyst_Read_After.png |
| A-04 | Добрий | UPDATE і DELETE чужого ресурсу | 403, стан без змін | 403, дані без змін | A04_Alice_CannotUpdateOrDeleteBobIncident; 41_Update_Delete_Final_Retest.png |
| A-05 | Добрий | Фільтрація списку | Без чужих записів | Alice бачить власні записи | A05_Reporter_SeesOnlyOwnIncidents; 25_Alice_Filtered_List.png |
| A-06 | Добрий | Reporter виконує triage | 403, статус без змін | 403, статус New | A06_Reporter_CannotTriageOwnIncident; 47_Reporter_Triage_Forbidden.png |
| A-07 | Добрий | Analyst виконує triage | 204, New → Triaged | 204, статус Triaged | A07_Analyst_CanTriageNewIncident; 48_Analyst_Triage_Allowed.png |
| A-08a | Добрий | Overposting | Owner і status без змін, role не надає прав | Захищені поля не змінено | A08a_Overposting_ProtectsFields; 77_Overposting_Protection.png |
| A-08b | Альтернатива | Інший контракт overposting | Не застосовується | Обрано A-08a | — |
| A-09 | Добрий | Аудиторська подія | authorization.denied, traceId | Подію записано, traceId збігається | 50_Triage_Audit_Log.png; 51_Triage_Audit_TraceId.png |
| A-10 | Політика 404 | Нерозрізнюваність чужого й відсутнього ресурсу | Не застосовується | Обрано 403 | — |
| A-11 | Відмінний | Search і severity-summary | Не вимагається | Не виконувалося для рівня «Добре» | — |
| A-12 | Добрий | Child resource та internal comment | 200/403/401/404 за правилами доступу | Усі відповідні сценарії перевірено | A12_ChildComments_Authorization; 81_Authorization_After_Fix.png |

Повний перелік автоматичних сценаріїв підтверджено командою:

`dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj -c Release --list-tests`

Доказ: `73_Authorization_Test_List.png`.

Підсумок автоматичного тестування:

- Усього: 28 тестів.
- Успішно: 28.
- Помилок: 0.
- Пропущено: 0.
- Результат збірки: Build succeeded.

Доказ: `72_All_Authorization_Tests_Passed.png`.

## Основний finding і залишковий ризик

Під час перевірки читання інцидентів виявлено IDOR/BOLA.

Alice отримала HTTP 200 OK та дані інциденту Bob замість очікуваної відповіді 403 Forbidden.

Першопричина — відсутність серверної перевірки доступу до конкретного ресурсу.

У вразливому стані `GetDetailsAsync` шукав інцидент лише за ID без перевірки власника.

Для виправлення реалізовано централізований метод `CanAccessIncident`.

Після виправлення Alice отримує 403 Forbidden при зверненні до інциденту Bob.

Недозволені операції не змінюють захищені дані.

Залишковий ризик: cookie-authenticated змінювальні маршрути потребують окремого захисту від CSRF. Поглиблена перевірка цієї загрози виходить за межі обраного рівня лабораторної роботи.

## Browser flow

Перевірено браузерний клієнт SecureLab.

Alice успішно виконала вхід та отримала список доступних їй інцидентів.

Перегляд власного інциденту «Підозрілий лист із вкладенням» працює правильно.

Звичайний коментар відображається, внутрішній коментар аналітика прихований.

Під час спроби відкрити інцидент Bob сервер повернув 403 Forbidden.

У консолі браузера підтверджено результат:

`Alice -> Bob: 403`

У `Client/app.js` використано `createTextElement` і `textContent` для безпечного відображення даних.

Використання небезпечного `innerHTML` не виявлено.

Докази:

- `42_Browser_Alice_Own_Allow.png`
- `43_Browser_Bob_Forbidden.png`
- `44_Browser_Safe_TextContent.png`
- `82_Browser_Alice_Own_Incident.png`
- `83_Browser_Alice_Bob_403.png`

## Добрий рівень: роль, transition, child, audit

### Привілейована дія triage

Реалізовано `POST /api/incidents/{id}/triage`.

За матрицею доступу виконувати цю дію може лише Analyst, якщо поточний статус інциденту New.

Сервер сам визначає наступний статус Triaged.

Поля role, ownerUserId і status із клієнтського JSON не використовуються для надання привілеїв.

Alice (Reporter) отримала 403 Forbidden. Після забороненого запиту статус залишився New.

Morgan (Analyst) отримав 204 No Content. Статус інциденту змінився з New на Triaged.

Повторний triage вже обробленого інциденту повернув 403 Forbidden. Статус залишився Triaged.

У PostgreSQL підтверджено запис в `incident_status_history` із попереднім статусом New, новим статусом Triaged та ідентифікатором Morgan.

Докази:

- `47_Reporter_Triage_Forbidden.png`
- `48_Analyst_Triage_Allowed.png`
- `49_Triage_History_Database.png`
- `78_Triage_Authorization_And_History.png`
- `79_Repeated_Triage_403.png`

### Доступ до дочірніх коментарів

Реалізовано `GET /api/incident-comments/{id}` із перевіркою прав через батьківський інцидент.

Reporter має доступ до звичайних коментарів власного інциденту.

Чужі та внутрішні коментарі для Reporter недоступні.

Analyst може переглядати внутрішні коментарі відповідно до матриці доступу.

Результати:

- 200 OK — дозволений доступ.
- 403 Forbidden — заборонений доступ.
- 401 Unauthorized — анонімний користувач.
- 404 Not Found — відсутній коментар.

Докази: ручні HTTP-перевірки, `40_Internal_Comments_Details_Retest.png`, автоматичний тест `A12_ChildComments_Authorization`.

### Захист від overposting

У методі `UpdateIncidentAsync` використано DTO:

`UpdateIncidentRequest(string Title, string Description)`

Контракт оновлення містить лише дозволені поля Title та Description.

Клієнт не може змінити власника, роль користувача або статус інциденту через додавання полів у JSON.

При спробі передати `ownerUserId`, `role` та `status` захищений стан залишається незмінним.

Дозволені текстові поля можуть бути оновлені власником ресурсу.

Підміна ролі не надає користувачу додаткових прав.

Докази:

- `45_Overposting_Protected_Fields.png`
- `46_Overposting_Database_Verification.png`
- `77_Overposting_Protection.png`
- Автоматичний тест `A08a_Overposting_ProtectsFields`.

### Аудит відмов

Для забороненої привілейованої дії реалізовано журналювання `authorization.denied`.

Подія містить:

- server-verified subject;
- action;
- resource;
- result;
- occurredAtUtc;
- traceId.

Паролі, cookie, токени й повне тіло запиту не записуються.

Під час перевірки Alice отримала HTTP 403 Forbidden.

У журналі API створено подію `authorization.denied`.

Значення traceId у журналі та HTTP-відповіді збіглися.

Докази:

- `50_Triage_Audit_Log.png`
- `51_Triage_Audit_TraceId.png`
- `78_Triage_Authorization_And_History.png`

## Відмінний рівень

Не виконувався — обрано рівень «Добре».

A-11 не входить до обов'язкових перевірок обраного рівня.

## Перевірки, відтворення і подання

### Початковий стан

- Гілка: lab/4-authorization.
- Базовий тег: v0.3.0.
- Базовий commit: 4636f8a.
- PostgreSQL: secure-lab-postgres-1, статус healthy.
- Початкове тестування: 18 успішних тестів, 0 помилок.

### Проміжні перевірки

Під час виконання лабораторної роботи проводилися послідовні перевірки реалізації.

Проміжні результати:

- 7 успішних тестів, 0 помилок.
- 22 успішні тести, 0 помилок.

Ці результати відображають попередні етапи реалізації, а не остаточний стан лабораторної роботи.

### Фінальне тестування

Після доповнення автоматичних HTTP-тестів проведено повний запуск:

`dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj -c Release`

Фактичний результат:

- Total: 28.
- Failed: 0.
- Succeeded: 28.
- Skipped: 0.
- Build succeeded.

Додатково виконано команду:

`dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj -c Release --list-tests`

Список містить усі обов'язкові автоматизовані сценарії для рівня «Добре».

Докази:

- `72_All_Authorization_Tests_Passed.png`
- `73_Authorization_Test_List.png`

### Фінальні HTTP-перевірки

Підтверджено:

- Анонімний POST /api/incidents — 401.
- Alice читає власний інцидент — 200.
- Alice не читає інцидент Bob — 403.
- Morgan читає інцидент Alice — 200.
- UPDATE чужого інциденту — 403.
- DELETE чужого інциденту — 403.
- Reporter не виконує triage — 403.
- Analyst виконує дозволений triage — 204.
- Повторний triage — 403.
- Захищені поля не змінюються під час overposting.
- Внутрішні коментарі доступні відповідно до ролі.

### Git-артефакти

Для роботи використано окрему гілку `lab/4-authorization`.

Вразливий стан збережено в окремому коміті:

`65f3ce67a0c30e14fedbe4d14d7e20e025cebf11`

Після виправлення авторизації та успішного проходження автоматичних тестів створено fixed commit:

`42e0921`

Повідомлення коміту:

`Fix Lab 4 authorization and add access tests`

На fixed commit створено анотований тег:

`v0.4.0`

Опис тега:

`Lab 4 fixed authorization`

Гілку `lab/4-authorization` успішно передано на GitHub командою:

`git push -u origin lab/4-authorization`

Тег успішно передано командою:

`git push origin v0.4.0`

Командою `git show --no-patch v0.4.0` підтверджено, що анотований тег посилається на виправлений коміт.

Репозиторій `lab1` переведено в режим Private відповідно до вимог методичних рекомендацій щодо зберігання навчального вразливого стану.

Доказ:

`84_Git_Fixed_Commit_Tag.png`

### Файли подання

- Матриця доступу: `docs/access-matrix.md`.
- Звіт у репозиторії: `docs/report-lab-04.md`.
- Автоматичні тести: `tests/SecureLab.Api.Tests/IncidentEndpointTests.cs`.
- Головний код авторизації: `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs`.
- Обмеження списку за власником: `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`.
- Git-гілка: `lab/4-authorization`.
- Git-тег: `v0.4.0`.

## Висновок

У лабораторній роботі №4 досліджено та усунуто вразливість IDOR/BOLA в навчальному застосунку SecureLab.

У вразливому стані автентифікований користувач Alice міг отримати дані інциденту Bob через підстановку ідентифікатора ресурсу.

Першопричиною була відсутність серверного рішення про доступ до конкретного інциденту.

Для виправлення реалізовано спільний метод `CanAccessIncident`, який враховує роль користувача, власника ресурсу, дозволену дію та видимість внутрішніх коментарів.

Захист застосовано до details, update, delete, дочірніх коментарів і triage. Для списку інцидентів використано обмеження запиту за власником.

Вертикальне підвищення привілеїв через triage заблоковано. Дозволений перехід New → Triaged виконує лише Analyst. Відмови супроводжуються безпечною аудиторською подією з traceId.

DTO оновлення захищає власника, роль та статус від підміни через клієнтський JSON.

Проведено ручні HTTP-перевірки, перевірки браузерного клієнта та автоматизоване тестування.

**Фінальний результат — 28 успішних тестів із 28, 0 помилок, Build succeeded.**

Вразливий та виправлений стани збережено в окремих Git-комітах. Створено й відправлено анотований тег `v0.4.0`. Репозиторій переведено в приватний режим.

Реалізовані сценарії та зібрані докази відповідають технічному обсягу лабораторної роботи №4 для рівня «Добре».
