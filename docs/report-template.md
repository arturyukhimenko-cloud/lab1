# Лабораторна робота № 3. Автентифікація та безпечна робота з обліковими даними

**Дисципліна:** Прикладні технології програмування в інформаційній безпеці
**Виконав:** Юхименко Артур Володимирович, КБ-43/2
**Репозиторій:** https://github.com/arturyukhimenko-cloud/lab1
**Гілка:** `lab/3-authentication`
**Базова версія:** `v0.2.0`
**Scaffold:** `lab-03-start-v1`
**Фінальний коміт коду:** `4636f8a`
**Анотований тег:** `v0.3.0`

## Мета

Замінити тимчасове джерело користувача з ЛР2 на перевірену автентифікацію ASP.NET Core Identity, EF Core stores і cookie-сеанс. Реалізувати безпечну реєстрацію, вхід, профіль, вихід, захист створення інциденту та автоматизовані регресійні сценарії.

## Етап 1. Підготовка контрольованого стану

- Роботу розпочато з перевіреного тегу ЛР2 `v0.2.0` у гілці `lab/3-authentication`.
- Встановлено scaffold `lab-03-start-v1` окремим комітом; перевірено метадані `.scaffolds/lab-03.json`.
- Використано .NET 10, локальний PostgreSQL у Docker (`Healthy`), Identity users `alice`, `bob`, `morgan`, `admin` зі сталими seed GUID і ролями.
- Локальний пароль seed-акаунтів задавався через `SeedUsers__Password` поза Git без внесення секретів у звіт.
- Перевірено відтворюваність seed/reset. Початковий регресійний прогін — 8/8.

## Етап 2. Реєстрація, контракти DTO та CP-01

- `POST /api/auth/register` перевіряє обов'язковість, формат і допустимі довжини `UserName`, `Email`, `DisplayName`.
- Реєстрація через `UserManager.CreateAsync` та призначення ролі Reporter; пароль обробляє стандартний Identity password hasher.
- `RegistrationResponse` містить тільки `Id`, `UserName`, `DisplayName`; паролі та службові поля Identity не повертаються.
- Коректна реєстрація — HTTP 201; повторна з тією самою обліковою назвою/email — 409. Додано регресійний тест duplicate registration.
- Схема `identity_users` містить `PasswordHash`, але не окреме поле відкритого пароля. Результат регресії після Етапу 2 — 9/9.

## Етап 3. Login, cookie, профіль, logout та CP-02

- `POST /api/auth/login` використовує `SignInManager.PasswordSignInAsync` і повертає 204 за успіху або однаковий безпечний 401 за невдалого входу.
- `GET /api/me` вимагає автентифікації та читає поточного користувача з перевіреного `ClaimTypes.NameIdentifier` у `HttpContext.User`.
- Клієнтський `X-Demo-UserId` більше не визначає профіль користувача.
- `POST /api/auth/logout` виконує `SignInManager.SignOutAsync`; наступний `GET /api/me` повертає 401.
- Cookie-конфігурація: `HttpOnly = true`, `SameSite = Lax`, `SecurePolicy.Always` у production, `SameAsRequest` у development; 401/403 повертаються без HTML redirect.
- Політика `auth-login`: 10 запитів за 5 секунд за IP-адресою, черга 0; перевищення ліміту — 429, після вікна відновлюється приймання запитів.
- У auth-логах зберігаються тільки статус `success`/`failure` та `TraceId`, без credentials і cookie.
- Підсумок Етапу 3 — 16/16 тестів.

## Етап 4. Захищене створення інцидентів, браузер та CP-03

- `POST /api/incidents` захищено через `.RequireAuthorization()`.
- `Incident.OwnerUserId` обчислюється сервером із `HttpContext.User`, а не з JSON чи стороннього header.
- Анонімний POST повернув 401. Для Bob сценарії без `ownerUserId` (A-04) і з підробленим `ownerUserId` Alice (A-05) створили інциденти з фактичним `OwnerUserId = Bob`; обидва тести успішні.
- Перевірки ЛР2 на помилку валідації (400) і duplicate title (201/409) переведено на cookie-aware клієнт без послаблення очікувань.
- Браузерний сценарій: анонімний стан → register 201 → login 204 → `/api/me` 200 → create 201 без owner у DTO → logout 204 → `/api/me` 401; після logout профіль очищено.
- Local Storage порожній, у Session Storage зафіксовано лише сторонній запис розширення Dark Reader, Console показала очікувані 401 після logout без облікових даних.

**Перевірений маршрут довіри:** `Login → Identity password verification → authentication cookie → cookie handler → HttpContext.User → ClaimTypes.NameIdentifier → Incident.OwnerUserId`. Поле `ownerUserId` у JSON, приховане поле форми та `X-Demo-UserId` не є засобами контролю доступу.

## Етап 5. Регресійні тести, S-01 та поглиблення

### Контрольні сценарії

| ID | Очікувано | Фактичний результат | Доказ у Word-звіті |
|---|---|---|---|
| T-01 | Коректна реєстрація; відповідь без секретів | 201, поля Id/UserName/DisplayName | Рис. 22, 24 |
| T-02 | Безпечна відмова на некоректну/повторну реєстрацію | 409 для duplicate registration | Рис. 24, 26–27 |
| T-03 | Пароль не зберігається відкрито | Схема identity_users: PasswordHash без поля plain password | Рис. 25 |
| A-01 | Анонімний захищений запит → 401, не redirect | GET /api/me 401; POST /api/incidents 401 | Рис. 54–55, 87 |
| A-02 | Login → /api/me із verified principal | Профіль Alice та 200 | Рис. 56–57 |
| A-03 | Header X-Demo-UserId не змінює principal | Cookie Alice залишає identity Alice | Рис. 58–59 |
| A-04 | Create без owner бере principal | Новий incident від Bob з owner Bob | Рис. 90–92 |
| A-05 | Підроблений owner не замінює principal | У БД owner Bob, хоча надіслано Alice | Рис. 90–92 |
| A-06 | Logout інвалідовує поточний сеанс | Після logout GET /api/me → 401 | Рис. 60–61 |
| A-07 | Unknown user і wrong password мають однаковий результат | Обидва випадки → безпечний 401 | Рис. 49–52 |
| S-01 | Не розкривати password/hash/cookie/token у відповідях, логах і diff | У перевірених фрагментах витоків не виявлено | Рис. 104–106, 108–111 |

Повний прогін `dotnet test tests/SecureLab.Api.Tests -c Release`: **18/18 пройдено, failed 0, skipped 0; Build succeeded** (рис. 107, 117). Власні тести A-01–A-07, duplicate registration, cookie flags, rate limiting; попередні предметні перевірки ЛР2 збережено.

### Поглиблення 1. Threat Analysis процесу входу

**Активи:** паролі користувачів, cookie-сеанси, профілі й інциденти.
**Точки входу:** `POST /api/auth/login`, `POST /api/auth/register`, `GET /api/me`, `POST /api/incidents`.
**Межі довіри:** браузер → API; сервер → Identity/БД; клієнтські поля й headers → перевірений principal.
**Загроза:** автоматизований перебір паролів і розкриття існування облікового запису.
**Захист:** стандартний verifier Identity, однаковий 401 для двох невдалих login, ліміт 10/5 с на IP, мінімальні auth events і cookie flags.
**Залишковий ризик:** розподілений перебір з різних IP і хибні блокування користувачів за спільною IP. Duplicate registration з 409 може підказувати наявність акаунта, але допомагає користувачу виправити помилку; для API це усвідомлений компроміс. `SameSite=Lax` зменшує, але не усуває CSRF-ризик: повний захист розглядається у ЛР6.

Логи навмисно не включають `UserName`, пароль, cookie, hash, token, Authorization header чи повний request body. IP обрано ключем ліміту, оскільки його можна застосувати до входу ще до встановлення особи; недолік — спільні та розподілені IP (рис. 112–114).

### Поглиблення 2. Готовність стандартного PasswordHasher до оновлення

Використано `AddIdentity<ApplicationUser, IdentityRole<Guid>>()`, `AddEntityFrameworkStores<SecureLabDbContext>()` та `UserManager.CreateAsync(user, password)` (рис. 115–116). Пароль хешується стандартним Identity `IPasswordHasher`, а не власним алгоритмом. Стандартна перевірка підтримує `PasswordVerificationResult.SuccessRehashNeeded` для раніше створених хешів, що дозволяє оновлювати параметри хешування під час коректної аутентифікації. **Фактичний експеримент із перехешуванням у цій ЛР окремо не проводився.**

## Публікація та підсумкова перевірка

- Змінені файли пройшли `git diff --cached --check`.
- Підсумкові тести: 18/18, помилок 0.
- Після тестів зафіксовано коміт `4636f8a` (`Complete Lab03 authentication and security regression tests`), створено анотований тег `v0.3.0`.
- Гілку `lab/3-authentication` і тег `v0.3.0` опубліковано на GitHub; підтвердження — рис. 118–121 у Word-звіті.

## Використання ШІ

ChatGPT використовувався як допоміжний інструмент для аналізу вимог, пояснення реалізації та підготовки матеріалів звіту. Фактичні результати підтверджено власними тестами й скріншотами. Короткий журнал: аналіз критеріїв, пояснення виправлень, звірка доказів, редагування викладу.

## Висновок

У ЛР3 реалізовано cookie-автентифікацію через ASP.NET Core Identity та перевірений контекст користувача для `GET /api/me` і `POST /api/incidents`. Додано безпекові сценарії A-01–A-07, налаштовано захист cookie, обмеження спроб входу та безпечне журналювання. За результатами автоматизованих тестів 18 із 18 сценаріїв пройдено успішно. Проведено S-01 і два поглиблення для рівня «Відмінно». Реалізацію зафіксовано комітом `4636f8a` і тегом `v0.3.0` на GitHub.
