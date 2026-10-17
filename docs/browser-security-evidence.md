
# Докази безпеки браузера (DEL-01)

Рівень: добрий («Добре»).

## Стенд і Git-стан (Для всіх)

ОС: Windows.

Браузер і версія: Chrome; точну версію та платформу перевірити в налаштуваннях браузера.

API: http://localhost:5080

CORS probes: http://localhost:4173 та http://localhost:4174

Release: `lab-05-start-v1`

Inherits: `v0.4.0`

Base commit: `42e0921b67d60526a5799f50a15f10c3d53d42f2`

Entry parent: `5f98649e33d1b2e53b1b965892a78ec1f84dd6ed`

Vulnerable commit: `d491dc67e29b8527194f2b7a8923496a48a6e424`

Підсумковий fixed commit ЛР №5: ще не створено.

Коміт завершення Етапу 2: `53b573e` (підтверджено Git push у `lab/5-browser-xss`).

Тег `v0.5.0`: ще не створено.

Гілка `main`: ще не оновлена.

Механізм сеансу перевірено в DevTools → Application → Storage → Cookies → http://localhost:5080.

Параметри cookie:

- Name: `.AspNetCore.Identity.Application`
- Domain: `localhost`
- Path: `/`
- HttpOnly: увімкнено.
- SameSite: `Lax`
- Secure: не встановлено.
- Expires: `Session`

Значення cookie у звіт не записуємо.

## Гіпотеза й REG-XSS-01 (Для всіх)

### Карта руху даних

Повна карта розміщена у файлі `docs/source-to-sink.md`.

Основне поле: `Incident.Description`.

Користувач вводить опис інциденту через форму браузерного клієнта. Дані надсилаються до API через `POST /api/incidents` у JSON-полі `description`, проходять перевірку та зберігаються в PostgreSQL.

Під час читання інциденту сервер повертає збережене значення в JSON-полі `description`.

У початковому `browser-lab.js` функція `load()` вставляла його в сторінку через:

`document.querySelector("#description").innerHTML = current.description;`

Після виправлення використовується:

`renderText(document.querySelector("#description"), current.description);`

### Гіпотеза XSS

Якщо користувач зберігає в описі інциденту текст, схожий на HTML, сервер повертає його через API, а браузер вставляє через `innerHTML`, цей текст може бути оброблений як HTML-розмітка замість звичайного тексту.

Причина проблеми — використання `innerHTML` для даних, які можуть контролюватися користувачем.

Під час Етапу 2 гіпотезу підтверджено за допомогою навчального XSS-marker. Після заміни `innerHTML` на безпечне відображення через `textContent` контрольна дія marker більше не виконувалася у перевірених сценаріях.

### Передумови досліду

Стенд: локальний SecureLab.

Основний користувач: Alice (Reporter).

Додаткові користувачі: Morgan (Analyst) та Bob (Reporter).

Seed-інцидент Alice: `20000000-0000-0000-0000-000000000001`.

Навчальний інцидент із HTML-подібним текстом: `20000000-0000-0000-0000-000000000003`.

Початковий стан Git: `d491dc6`.

PostgreSQL запущено, навчальні дані відновлено.

Після додавання нового seed-коментаря виконано повторне відновлення локальної бази даних.

Автоматичні тести після виправлення: 28 успішних, 0 помилок, 0 пропущених.

API працював на `http://localhost:5080`.

Навчальний XSS-marker використано з файла `resources/lab05-marker.txt` без зміни його вмісту.

GUID інциденту Alice до виправлення:

`8fcf0ef7-d5de-4f84-8ffe-d390de7adf20`

Після відновлення бази створено новий тестовий інцидент із тим самим навчальним marker для повторної перевірки.

### Перевірка звичайного тексту до виправлення

| Значення | Спостереження до виправлення |
|---|---|
| `Користувач O'Brien написав "терміново"` | Апостроф і подвійні лапки відображаються правильно |
| `5 < 7 і 9 > 3` | Символи порівняння відображаються правильно |
| `Київ — Ω` | Українські та грецькі символи відображаються правильно |
| `<b>жирний</b> текст` | Браузер створює HTML-елемент `b`, слово відображається жирним |
| Seed-інцидент із `<script>` | На сторінці видно лише частину опису; в Elements створено елемент `script` |
| 3900 символів `А` | У DOM отримано 3900 символів; у JSON-відповіді також 3900 |

Звичайні лапки, Unicode та символи порівняння самі по собі не виявляють проблему. HTML-подібні значення показали, що браузер інтерпретує частину тексту як розмітку.

На Етапі 1 виконання JavaScript цими перевірками ще не було доведено. На Етапі 2 виконання підтверджено окремим навчальним XSS-marker.

### Контрольна точка CP-01

Перевірено:

1. Побудовано карту `source-to-sink` для `Incident.Description` і `IncidentComment.Text`.
2. У Network підтверджено `GET 200` для інциденту Alice та отримання JSON.
3. У початковому `browser-lab.js` знайдено небезпечні операції `innerHTML`.
4. У Elements підтверджено створення HTML-елементів із текстових даних.
5. Визначено різницю між недовіреним користувацьким текстом і безпечним текстовим відображенням.

Висновок CP-01: початковий клієнт обробляв значення `Incident.Description` як HTML. На Етапі 2 цю проблему підтверджено навчальним XSS-marker, після чого використано безпечні DOM API.

### REG-XSS-01 — порівняння до та після виправлення

| Спостереження | До fix | Після fix | Посилання на доказ |
|---|---|---|---|
| Значення в БД/API | API повертав збережений навчальний marker у полі `description` | Для повторно створеного інциденту підтверджено `matches: true` між JSON та DOM | Network Response до fix; Console після fix |
| DOM основного Description | Усередині `div#description` створювався HTML-елемент `img` | Marker відображається як текстовий вузол, елемента `img` немає | Elements до/після fix |
| Дія навчального marker | У браузері Alice отримано `executed` | Після виправлення отримано `null` | Console до/після fix |
| Апостроф/лапки, `5 < 7`, Unicode, HTML-like рядок | Лапки й Unicode відображалися, але `<b>` інтерпретувався як HTML | Лапки, Unicode та `<b>жирний</b>` відображаються як текст | Скріншоти Етапів 1–2 |
| Довжина за правилами ЛР 2 | 3900 символів у DOM та JSON | 3900 символів у DOM; довжину JSON після fix окремо не перевіряли | Console до/після fix |
| Вікно Morgan: GET і перевірка marker; контроль Bob | Morgan: `GET 200`, `executed`; Bob: `GET 403`, `null` | Повторні перевірки Morgan і Bob після fix ще не виконували | Скріншоти сеансів Morgan і Bob до fix |
| IncidentComment.Text («Добре») | У другому `p` створювався `img`, marker повертав `executed` | Marker відображається текстом; HTML-елемента `img` немає; результат `null` | Elements і Console до/після fix |

Finding: виявлено stored XSS у браузерному відображенні збережених даних. Актив — браузерний сеанс і дані, доступні користувачу. Передумови — можливість збереження недовіреного тексту та подальше відкриття відповідного інциденту.

Першопричина — передавання значень `description` і `comments[].text` безпосередньо в `innerHTML`.

Вплив на стенді: навчальний marker виконав локальну DOM-дію та встановив `data-lab05-marker="executed"`. Інших дій або передавання даних за межі стенда не виконували.

Виправлення: замість `innerHTML` використано спільну функцію `renderText()` із `textContent`.

Retest: після виправлення обидва поля відображають marker як звичайний текст. Контрольна перевірка повертає `null`, а в Elements відсутній створений із marker елемент `img`.

Залишковий ризик: інші ще не перевірені DOM-sinks, сторонній JavaScript, URL-контексти та додаткові механізми браузерного захисту.

Повторювана REG-XSS-01: використано той самий навчальний marker із `resources/lab05-marker.txt`. Після відновлення бази перевірку опису повторено на новому інциденті. Негативний контроль Bob до виправлення підтвердив `403` і `null`.

### Контрольна точка CP-02

Перевірено:

1. Під Alice підтверджено виконання XSS-marker у полі `Incident.Description`.
2. Під Morgan підтверджено `GET 200` і повторне виконання збереженого marker.
3. Під Bob підтверджено `GET 403` та відсутність виконання marker (`null`).
4. Для рівня «Добре» додано публічний навчальний коментар `30000000-0000-0000-0000-000000000005`.
5. У `IncidentComment.Text` до виправлення підтверджено елемент `img` і результат `executed`.
6. У `browser-lab.js` додано спільну функцію `renderText()` та замінено обидва небезпечні виклики `innerHTML`.
7. Після виправлення для обох полів marker відображається як текст, а контрольна дія не виконується.
8. Для опису підтверджено `matches: true` між JSON-відповіддю та DOM.
9. Перевірено лапки, символи `<` і `>`, Unicode, HTML-подібний текст та опис довжиною 3900 символів.
10. Автоматичні тести: 28 виконано, 28 успішно, 0 помилок.

Висновок CP-02: для обох перевірених полів усунуто небезпечну HTML-інтерпретацію недовірених даних. Виправлення пройшло позитивну регресію та автоматичні тести.

Коміт завершення Етапу 2: `53b573e` (`lab5: fix stored XSS and complete stage 2`). Підсумковий fixed commit усієї ЛР №5 і тег `v0.5.0` ще не створено.

## CORS і REG-CORS-01 (Для всіх)

### Стенд, origins і прогноз (Етап 3, крок 1)

Стенд: навчальний API `http://localhost:5080`, який працює у `Development`.

Звичайний same-origin клієнт і API мають origin `http://localhost:5080`. Навчальні CORS-probes використовують `http://localhost:4173` (дозволений після налаштування) і `http://localhost:4174` (негативний контроль). Через різні порти ці адреси мають різні origins, хоча належать до одного site `localhost`.

Перед налаштуванням policy перевірено працездатність API через `/health` — `200 OK` із `{"status":"ready"}`.

Прогноз, складений до зміни CORS policy:

| Сценарій | Чи доходить request до API? | Чи може JavaScript прочитати response? | Чому |
|---|---|---|---|
| same-origin 5080 → API | Так, якщо API працює | Так за умови дозволу API | Між клієнтом і API немає переходу між origins; CORS не потрібен |
| simple GET 4173 → API | Так | До policy — ні; після allowlist — так | Browser SOP/CORS керує читанням cross-origin response |
| simple GET 4174 → API | Так | Ні, бо origin не дозволений | Відсутній `Access-Control-Allow-Origin` |
| curl/.http → API | Так | Не застосовується; прямий клієнт читає HTTP | Браузерний SOP/CORS не застосовується до `curl.exe` |
| preflighted GET 4173 → API («Добре») | Спочатку `OPTIONS`; GET — лише після успішного preflight | До policy — ні; після policy — так | Додатковий заголовок `X-Lab-Cors-Probe` спричиняє preflight |
| preflighted GET 4174 → API («Добре») | `OPTIONS` надсилається; GET не надсилається, якщо preflight не пройде | Ні | Preflight для забороненого origin не отримує CORS-дозволу |

### Запуск навчальних probes (Етап 3, крок 2)

API: `http://localhost:5080`. Застосунок доступний через `dotnet run --project src/SecureLab.Api`; PostgreSQL уже запущений окремо.

У PowerShell №3 з кореня `starter-project` запущено:

```powershell
py -3 -m http.server 4173 --bind localhost --directory demonstrations/cors-probe
```

У PowerShell №4 запущено:

```powershell
py -3 -m http.server 4174 --bind localhost --directory demonstrations/cors-probe
```

Обидві сторінки `/` успішно відкрилися в Chrome та містили поле API URL і кнопку GET. Це лише локальні демонстраційні origins; окремі фронтенд-збірки не потрібні.

### Стан до CORS policy (Етап 3, кроки 2–3)

Запит у Console із `http://localhost:4173` до `http://localhost:5080/health`: сервер повернув HTTP `200 OK`, але браузер показав `No 'Access-Control-Allow-Origin' header` і `TypeError: Failed to fetch`. У Network підтверджено `Origin: http://localhost:4173`.

Простий GET із `http://localhost:4174`: сервер так само повернув `200 OK`, але JavaScript не зміг прочитати відповідь через CORS.

Для preflight-запитів із заголовком `X-Lab-Cors-Probe: stage-3` браузер надіслав `OPTIONS` із `Access-Control-Request-Method: GET` і `Access-Control-Request-Headers: x-lab-cors-probe`. До налаштування policy для обох origins отримано `405 Method Not Allowed`, без CORS-дозволу. Основний GET після невдалого preflight не запускався.

### Налаштована policy та middleware (Етап 3, крок 3)

Файл: `src/SecureLab.Api/Program.cs`. Політика `Lab05BrowserProbe` реєструється лише в `Development` через `builder.Services.AddCors()` до `builder.Build()`.

Кінцевий навчальний варіант:

```csharp
policy.WithOrigins("http://localhost:4173")
      .WithMethods("GET")
      .WithHeaders("X-Lab-Cors-Probe")
      .AllowCredentials();
```

`UseRouting()` розміщено перед умовним `UseCors("Lab05BrowserProbe")`; далі йдуть `UseAuthentication()`, `UseAuthorization()` та `UseRateLimiter()`. Це дозволяє застосовувати CORS також до відповідей `401/403`. У Production ця навчальна policy не реєструється й не застосовується. `AllowAnyOrigin()` та неконтрольоване відбиття будь-якого Origin не використовуються.

Для основного браузерного GET/preflight на кроці 4 `AllowCredentials()` спочатку не застосовувався; його додано для cookie-досліду кроку 5, як вимагає рівень «Добре».

### REG-CORS-01 — фактичні результати до і після (Етап 3, кроки 4–7)

| Клієнт / запит | До policy | Fixed state | HTTP / CORS / доказ |
|---|---|---|---|
| Same-origin 5080 → API | API `/health`: `200 OK`; окремого browser trace для цього сценарію не фіксували | Same-origin не потребує CORS | PowerShell, `/health`; не приписуємо окрему браузерну перевірку |
| Простий GET із 4173 до `/health` | API `200`, але JavaScript: `Failed to fetch` | JS отримав `{status: 200, body: '{"status":"ready"}'}` | Network: `Access-Control-Allow-Origin: http://localhost:4173` |
| Простий GET із 4174 до `/health` | API `200`, JS заблокований CORS | API `200`, JS і надалі заблокований CORS | Відсутній `Access-Control-Allow-Origin`; Console `Failed to fetch` |
| Preflight 4173 із `X-Lab-Cors-Probe` | `OPTIONS 405`, actual GET не надіслано | `OPTIONS 204`, потім `GET 200`, JS читає відповідь | `Access-Control-Allow-Origin: http://localhost:4173`, `Access-Control-Allow-Methods: GET`, `Access-Control-Allow-Headers: X-Lab-Cors-Probe` |
| Preflight 4174 із `X-Lab-Cors-Probe` | `OPTIONS 405`, CORS-дозволу немає | `OPTIONS 204` без CORS-дозволу; actual GET заблоковано браузером | Network: `Origin: http://localhost:4174`, `Access-Control-Allow-Origin` відсутній |
| Авторизований probe 4173 → `/api/incidents` | Окремо до policy з cookie не перевіряли | HTTP `200`, JSON доступний сторінці | `Cookie` у request; `Access-Control-Allow-Origin: http://localhost:4173`, `Access-Control-Allow-Credentials: true` |
| Авторизований probe 4174 → `/api/incidents` | Окремо до policy з cookie не перевіряли | API `200`, але JS не читає відповідь | `Cookie` у request, без `Access-Control-Allow-Origin`; сторінка повідомляє про блокування |
| Неавторизований probe 4173 → `/api/incidents` | Не перевіряли | HTTP `401 Unauthorized`, JSON-помилка доступна JS | Приватне вікно без входу; ACAO для 4173 та `Access-Control-Allow-Credentials: true`; cookie відсутня |
| Прямий HTTP (`curl.exe`) → `/health` | Окремо до policy не перевіряли | HTTP `200`, тіло `{"status":"ready"}` доступне | Запити без `Origin`, з `Origin: 4174` (ACAO немає) та з `Origin: 4173` (ACAO і ACAC є) |

Для `OPTIONS 204` забороненого 4174 позитивного CORS-рішення немає: сам HTTP-статус `204` не означає дозвіл, якщо відсутній `Access-Control-Allow-Origin`.

### Cookie і credentials (Етап 3, крок 5, «Добре»)

Після ввімкнення `.AllowCredentials()` користувач Alice ввійшов через `http://localhost:5080/auth-lab.html` (`Login: 204`). У тому самому вікні з `http://localhost:4173` кнопка GET на `/api/incidents` повернула `200` і JSON. У Network підтверджено присутність cookie без потреби копіювати її значення. Response містив `Access-Control-Allow-Origin: http://localhost:4173` та `Access-Control-Allow-Credentials: true`.

Для `http://localhost:4174` запит `/api/incidents` також дійшов до API разом із cookie та отримав `200`, але браузер не дав сторінці прочитати відповідь: дозволу ACAO для 4174 немає.

Тимчасово вилучено `.AllowCredentials()` із `Program.cs` та перезапущено API. Із дозволеного `4173` credentialed-запит `/api/incidents` отримав `200` на сервері, але браузер показав помилку: `Access-Control-Allow-Credentials` відсутній, хоча режим credentials дорівнює `include`. Після негативного контролю `.AllowCredentials()` повернуто, API перезапущено.

У приватному вікні без авторизації GET із 4173 до `/api/incidents` повернув `401 Unauthorized`; JavaScript прочитав текст відмови. У Network підтверджено ACAO `http://localhost:4173`, ACAC `true` і відсутність cookie в запиті. Це підтверджує правильний порядок CORS перед авторизацією, але не замінює перевірку прав доступу.

**Конфіденційність доказів:** на ранніх скріншотах Network було видно повне значення session cookie. Перед включенням до звіту необхідно приховати це значення або повторно зняти скріншоти зі згорнутими Request Headers. Секретні cookie, паролі й токени не зберігати в документах або Git. Використаний навчальний сеанс слід завершити та оновити після тесту.

### Прямий HTTP і кінцевий стан (Етап 3, кроки 6–7)

У PowerShell №1 виконано:

```powershell
curl.exe -i http://localhost:5080/health
curl.exe -i -H "Origin: http://localhost:4174" "http://localhost:5080/health"
curl.exe -i -H "Origin: http://localhost:4173" "http://localhost:5080/health"
```

Усі три прямі запити повернули `HTTP/1.1 200 OK` та `{"status":"ready"}`. Для `Origin: http://localhost:4174` заголовок ACAO був відсутній; для `Origin: http://localhost:4173` сервер повернув `Access-Control-Allow-Origin: http://localhost:4173` і `Access-Control-Allow-Credentials: true`.

Прямий HTTP-клієнт не виконує браузерну SOP/CORS-перевірку, однак серверна автентифікація, авторизація та перевірка даних залишаються чинними. CORS не є механізмом серверного обмеження всіх HTTP-запитів.

Зафіксований кінцевий стан `Program.cs` для ЛР №5: одна навчальна Development policy, точний origin 4173, дозволені лише GET і `X-Lab-Cors-Probe`, `.AllowCredentials()`, без дозволу для 4174. Production перевірятиметься окремо на підсумковому етапі; тут не заявляємо про проведений Production-запуск.

### Review XSS, CORS, CSRF та авторизації (Етап 3, крок 8, «Добре»)

**XSS** — виконання недовіреного JavaScript через небезпечне відображення даних у браузері. У SecureLab Етапу 2 вразливі `Incident.Description` та `IncidentComment.Text` виправлено спільною функцією `renderText()` із `textContent`. Це зменшує ризик виконання збереженого HTML/JS як коду.

**CORS** — браузерний механізм дозволу JavaScript читати відповідь іншого origin. У нашому стенді 4173 дозволений, 4174 заблокований для читання, але GET із 4174 все одно міг дійти до API та повернути HTTP 200. CORS не є автентифікацією, авторизацією чи повноцінним захистом API від прямого HTTP-клієнта.

**CSRF** — ризик небажаного запиту від імені користувача з активною сесією. Навіть якщо браузер не дозволить зловмисній сторінці прочитати response, деякі запити з cookie можуть бути надіслані. Для state-changing дій потрібен окремий antiforgery/CSRF-контроль; реалізація належить до ЛР №6, а не до цього кроку.

**Авторизація** — перевірка сервером права користувача виконати операцію. Без входу запит із 4173 повернув `401 Unauthorized`, хоча CORS дозволив прочитати відповідь. Наявність CORS-дозволу не дає автоматично доступу до захищених даних.

Порти 4173, 4174 та 5080 — різні origins, але той самий `localhost` site; тому це не доказ поведінки SameSite для справді cross-site сценаріїв. Навчальна CORS policy обмежена Development, а кінцевий стан не використовує `AllowAnyOrigin()`.

**Для DEL-03:** цей короткий review перенести до підсумкового звіту ЛР №5 під час оформлення після Етапу 4; окремий файл для цього кроку не створюємо.

### Контрольна точка CP-03

Підтверджено фактичними скріншотами та результатами команд:

1. API `/health` працює (`200 OK`), сторінки probes 4173 та 4174 запускаються окремо.
2. До policy прості GET із 4173/4174 доходять до API, але JavaScript не читає responses.
3. До policy preflight `OPTIONS` із обох origins мав HTTP `405`, після policy дозволений 4173 має `204` із необхідними CORS-заголовками.
4. Заборонений 4174 після policy має preflight `204` без ACAO, тому actual request блоковано; простий GET дає `200` на API, але відповідь недоступна JS.
5. Користувач Alice із cookie отримав `200` і читабельний JSON через 4173; через 4174 JavaScript відповіді не отримав.
6. Тимчасове вимкнення `.AllowCredentials()` викликало очікувану browser CORS-помилку для credentialed GET; параметр повернуто.
7. В інкогніто без cookie отримано `401 Unauthorized` із ACAO для 4173 та ACAC `true`, доступний JavaScript.
8. `curl.exe` прочитав відповіді з HTTP `200` без браузерного SOP/CORS, також із вручну доданими Origins 4173/4174.
9. Вихідна policy — лише Development і точний `4173`; `4174` не дозволений; XSS/CORS/CSRF/authorization розрізнено в review.

**Результат CP-03:** практичні перевірки Етапу 3 на рівень «Добре» виконано. Фінальний commit ЛР №5, повторна регресія і тег `v0.5.0` належать до наступного етапу. Після зміни `Program.cs` автоматичні тести ще не запускалися: 28/28 — останній підтверджений результат Етапу 2.

## Додатково для «Добре»

### Другий потік IncidentComment.Text

Карта потоку: `docs/source-to-sink.md`.

Навчальний публічний коментар із marker додано через `DbSeeder.cs`.

GUID коментаря: `30000000-0000-0000-0000-000000000005`.

Інцидент Alice: `20000000-0000-0000-0000-000000000001`.

Автор: Morgan.

Параметр: `IsInternal = false`.

Після відновлення бази в JSON для інциденту Alice підтверджено два публічні коментарі: `...0001` та `...0005`.

Внутрішній коментар `...0003` не повертався в звичайній відповіді для Alice.

У `IncidentQueries.GetDetailsAsync` текст коментаря передається в `IncidentCommentResponse` як поле `comments[].text`.

У початковому `browser-lab.js` використовувалося:

`paragraph.innerHTML = comment.text;`

До виправлення у другому `<p>` всередині `div#comments` створювався HTML-елемент `img`, а marker повертав `executed`.

Після виправлення використовується:

`renderText(paragraph, comment.text);`

Єдиний safe helper для обох полів:

```javascript
function renderText(element, text) {
    element.textContent = text ?? "";
}
```

Після виправлення HTML-marker у коментарі відображається як звичайний текст. Команда перевірки повертає `null`, а в Elements другого `<p>` більше немає елемента `img`.

### Preflight і cookie

Етап 3 виконано. Для `4173` після CORS policy підтверджено `OPTIONS 204` із `Access-Control-Allow-Origin: http://localhost:4173`, `Access-Control-Allow-Methods: GET`, `Access-Control-Allow-Headers: X-Lab-Cors-Probe`. Для `4174` відповідь `OPTIONS 204` не містила ACAO, тому браузер не допустив actual GET.

Cookie-дослід виконано із `credentials: "include"`: авторизований Alice отримав `200` із 4173, а 4174 — CORS-блокування читання відповіді, хоча cookie була в request і сервер повернув `200`. Негативний контроль без `.AllowCredentials()` дав очікувану browser CORS-помилку. У приватному вікні без входу отримано читабельну відповідь `401` з ACAO/ACAC для 4173.

Фінальна `.AllowCredentials()` залишена тільки в `Development` policy для точного origin `http://localhost:4173`. Навчальні ports — same-site localhost, але cross-origin: цей дослід не є перевіркою cross-site SameSite.

Review XSS/CORS/CSRF/authorization виконано в розділі «CORS і REG-CORS-01», крок 8; після завершення Етапу 4 перенести стислий review до DEL-03.

### CSP

| Директива CSP | Фактичні ресурси застосунку | Обране джерело / причина |
|---|---|---|
| default-src | | |
| script-src | | |
| style-src | | |
| connect-src | | |
| img-src | | |
| frame-ancestors | | |

Місце CSP middleware: визначити під час Етапу 4.

Режим: enforcement.

Документ 200 і фактичний CSP header: перевірити.

Кероване порушення через `new Image()` до 4173: виконати та зафіксувати результат.

Для рівня «Добре» окремі докази report-only не обов'язкові.

### Захисні заголовки

| Header / control | Фактичне значення | Ризик і межа захисту / policy rationale |
|---|---|---|
| X-Content-Type-Options | | |
| Referrer-Policy | | |
| frame-ancestors | | |

## Підсумкова перевірка

Після виконання всіх етапів доповнити результати:

- REG-XSS-01 — виконано для двох полів; повторні перевірки Morgan/Bob після fix окремо не проводилися.
- REG-CORS-01 — браузерні simple/preflight та cookie-дослід Етапу 3 виконані; фінальну регресію повторимо на Етапі 4 перед тегом `v0.5.0`.
- Перевірка IncidentComment.Text — виконано до та після виправлення.
- CSP enforcement і захисні заголовки — попереду, Етап 4.
- Автоматичні тести — 28 із 28 пройшли після Етапу 2; після поточних CORS-змін тести ще не запускалися.
- Fixed commit — ще не створено.
- Тег `v0.5.0` — ще не створено.
- Стан `main` — ще не оновлено.

Підсумкова таблиця:

| До | Першопричина | Fix | Retest | Залишковий ризик | Доказ / commit |
|---|---|---|---|---|---|
| `innerHTML` для Description та Comment.Text; marker `executed` | Недовірений текст передавався в HTML parser | Спільний `renderText()` із `textContent` | Для обох полів marker — `null`, DOM містить текст, 28/28 тестів | Інші DOM-sinks і CSP ще не перевірено; CORS перевірено на Етапі 3 | Скріншоти Етапу 2; коміт `53b573e` |
| До policy: 4173 і 4174 блокувалися; `OPTIONS 405` | Відсутність CORS policy для окремого навчального origin | Development `Lab05BrowserProbe` для 4173, `UseCors` до authorization, `.AllowCredentials()` | 4173: GET 200, OPTIONS 204; 4174: без ACAO; cookie 200/401, прямий curl 200 | CORS не захищає від CSRF і не замінює права API | Скріншоти й `curl.exe` Етапу 3; підсумковий commit попереду |
