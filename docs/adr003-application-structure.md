# ADR 0003: Структура приложения на .NET

## Статус

Принято

## Контекст

[ADR 0001](adr001-repo-and-testing-strategy.md) описал, как порт проверяется, но не то, из чего он
состоит. Compat-харнесс готов: 235 тестов закрывают все 56 точек входа PHP-версии, построчное
покрытие оригинала — 78.2% ([coverage.md](coverage.md)). Дальше нужен сам портал, а значит —
решение о раскладке кода, принятое один раз и записанное.

### Что мы портируем

PHP-версия — около 8300 строк процедурного кода без слоёв. Доступ к данным, бизнес-правила,
проверки прав и генерация HTML живут в одном файле: `edit/game/index.php` — 700 строк, где
`echo` соседствует с интерполяцией строк в SQL. Единственное, что похоже на слой, —
`public_html/appcode/logic/*`: десяток модулей (`gamebase`, `gamelist`, `problems`, `edit`,
`internal_log`, `review`, `photo`, `updates`, `stat`, `search`), в которых собраны запросы и
правила. Это и есть заготовка сервисного слоя, которую надо разложить по местам.

Предметные области видны отчётливо: календарь игр и даты, география (регионы, субрегионы,
полигоны), очереди модерации, журнал изменений и ленты, отзывы и фото, пользователи и права,
внешние интеграции, статистика.

### Три ограничения, которые определяют всё остальное

**1. Схема БД — не наша.** Порт работает с той же живой базой, что и PHP-версия: 21 таблица
`ki_*`, снимок в `db/init/01-schema.sql` PHP-репозитория (см. [db/README.md](../db/README.md)). Имена колонок легаси
(`deleted_flag`, `show_flags`, `order`, `mg`), внешних ключей в базе **нет вообще** —
целостность держится кодом. Пока PHP-версия может быть запущена (откат релиза, параллельная
работа во время миграции), схему менять нельзя, а денормализованные поля
(`review_count`, `photo_count`, `vk_likes`, таблица `ki_years_cache`) надо продолжать
поддерживать в актуальном состоянии.

**2. У нашего API есть .NET-потребитель.** Пакет `JoinRpg.Common.KogdaIgraClient` — это клиент
**к нам**: `IKogdaIgraApiClient.GetGameInfo(int)` и `GetChangedGamesSince(DateTimeOffset)`,
DTO `KogdaIgraGameInfo` с явными именами полей вроде `[JsonPropertyName("update_date")]`.
Им пользуется `rating.bastilia.ru` и синхронизация в joinrpg.ru. Плюс allrpg.info ходит на
`/api/game/{id}` и `/api/allrpg-info/{id}`. То есть JSON-контракт — публичный, и порт обязан
его сохранить. Он закреплён `GameApiTests`, `ChangedGamesApiTests`, `CanonicalApiUrlTests`.

**3. Часть зафиксированного поведения — это баги.** Compat-тесты честно закрепили и то, что
сломано: `/archive/` ([#69](https://github.com/joinrpg/kogda-igra/issues/69)),
`/api/allrpg-info/*` ([#74](https://github.com/joinrpg/kogda-igra/issues/74)),
`/photo/preview/` ([#75](https://github.com/joinrpg/kogda-igra/issues/75)),
`/edit/merge/` ([#76](https://github.com/joinrpg/kogda-igra/issues/76)),
обнуление лайков при недоступном VK ([#77](https://github.com/joinrpg/kogda-igra/issues/77)),
`/edit/photo/` ([#81](https://github.com/joinrpg/kogda-igra/issues/81)),
`/help-needed/` ([#82](https://github.com/joinrpg/kogda-igra/issues/82)).
Воспроизводить фатальную ошибку PHP на .NET бессмысленно. Значит, «1=1» из ADR 0001 —
правило с исключениями, и исключения надо перечислить, а не решать каждый раз заново.

### Что уже есть готового

Соседний проект того же автора публикует на nuget.org семейство `JoinRpg.Common.*`
(исходники — в `joinrpg-net/src/Common/`, версия `2026.8.12`, .NET 10):
`PrimitiveTypes` (+ генератор типизированных id), `EntityFrameworkCore`, `WebInfrastructure`,
`WebComponents`, `KogdaIgraClient`.

Образцы потребления — не сам joinrpg-net (там 90 проектов, EF6 на MS SQL и Autofac), а два
небольших приложения: `TwilightRandom` (Razor Pages, 5 проектов) и `bastilia-rating`
(Blazor Web App, 8 проектов). Они показывают живой, обкатанный набор вызовов.

## Решение

### 1. Раскладка решения

Пять проектов в `src/` плюс существующий тестовый:

| Проект | Роль | На что ссылается |
|---|---|---|
| `KogdaIgra.Domain` | Доменные типы, значения, правила, интерфейсы репозиториев и сервисов | `JoinRpg.Common.PrimitiveTypes` — и больше ничего |
| `KogdaIgra.Dal` | EF Core + Npgsql: `KogdaIgraDbContext`, `Entities/` (легаси-схема), репозитории | Domain, `JoinRpg.Common.EntityFrameworkCore`, `JoinRpg.Common.WebInfrastructure` |
| `KogdaIgra.Services` | Сценарии пользователя: одна операция = один метод. Проверки прав, журнал, письма | Domain |
| `KogdaIgra.Portal` | Только UI и HTTP: страницы, эндпоинты API, композиция DI | Services, Dal, Domain, `WebInfrastructure`, `WebComponents` |
| `KogdaIgra.Migrator` | Консольный мигратор, отдельный контейнер и k8s-job | Dal |
| `KogdaIgra.CompatTests` | Есть. Получает вторую реализацию `ISiteUnderTest` | — |

Правило зависимостей — внутрь: `Portal → Services → Domain`, `Dal → Domain`. `Services` **не
знает** про EF Core и работает через интерфейсы, объявленные в `Domain`; `Dal` подключается
в единственном месте — композиционном корне `Portal`. Это и есть весь CLEAN, который нам нужен.

Отдельный `Services` заведён сознательно, хотя приложение небольшое. Причина конкретная: почти
вся содержательная логика PHP-версии — это операции записи (`do_game_update` диффит поля, сам
выбирает тип события для журнала, рассылает письма мастерам, пересчитывает счётчики), и именно
они закреплены write-тестами. В PHP они живут внутри страниц — ровно та ошибка, которую порт
повторять не должен.

### 2. Доменные типы отдельно от таблиц

`KogdaIgra.Dal/Entities/*` повторяют легаси-схему **буквально**, с её именами колонок; доменные
типы в `KogdaIgra.Domain` — чистые records с осмысленными именами и без следов `ki_`. Между ними
явный маппинг в репозиториях.

Так делает `bastilia-rating` (`Database/Entities/` против `Domain/*.cs`); `TwilightRandom`
наоборот — мапит доменные POCO напрямую. Второй вариант дешевле, но у Twilight схема своя и
чистая, а у нас — чужая и исторически кривая. Если доменные типы получат поля `deleted_flag` и
`show_flags`, эти имена останутся в коде навсегда, и вычистить схему после отключения PHP-версии
станет нельзя.

Легаси-значения при этом переводятся в нормальные типы прямо на границе: `deleted_flag`
(`-1` / `0` / `1`) — в `GameModerationState { PendingModeration, Published, Deleted }`,
`ki_status` с его поведенческими флагами — в перечисление с правилами, `begin` + `time` —
в `DateRange` из `PrimitiveTypes`.

### 3. Типизированные идентификаторы

Ключи — генерируемые типы из `JoinRpg.Common.PrimitiveTypes` (атрибут `[TypedEntityId]`,
source generator): `GameIdentification`, `PolygonIdentification`, `RegionIdentification`,
`SubRegionIdentification`, `ReviewIdentification`, `PhotoIdentification`. Пользователь и почта
берутся готовыми — `UserIdentification` и `Email` уже есть в пакете.

Мотив не эстетический. В PHP всё — голый `int`, и типовая авария выглядит так: у фото автор
указан текстом, `author_id` остаётся `NULL`, страница зовёт `get_user_by_id(NULL)`, в SQL уходит
пустая строка, и `count(false)` роняет запрос
([#81](https://github.com/joinrpg/kogda-igra/issues/81)). Связка «отдельный тип + nullable
reference types» делает этот класс ошибок невыразимым: `UserIdentification?` нельзя молча
передать туда, где ждут `UserIdentification`.

Маппинг в EF Core — одной строкой в `ConfigureConventions`, как в `TwilightDbContext`:

```csharp
configurationBuilder.Properties<GameIdentification>()
    .HaveEntityIdValueConversion<GameIdentification, int>();
```

Честно про риск: **ни `TwilightRandom`, ни `bastilia-rating` своих типизированных id не
объявляют** — генератор подключён транзитивно и ничего не генерирует, а из пакета берут только
готовый `UserIdentification`. Мы будем первым настоящим потребителем `[TypedEntityId]`.
Модель-биндинга ASP.NET для этих типов тоже нет: оба образца биндят `int` и конструируют id
руками. Мы делаем так же — принимаем `int` в роуте и заворачиваем на входе в обработчик.

`KogdaIgraIdentification` из пакета не используем: это идентификатор **наших** игр на стороне
joinrpg, у нас для этого `GameIdentification`.

### 4. Инфраструктура — из `JoinRpg.Common.WebInfrastructure`

Берём целиком, по образцу `Twilight.Web/Program.cs`:

- `builder.Host.UseJoinSerilog("KogdaIgra.Portal")` и `app.UseJoinRequestLogging()`;
- `services.AddJoinWebPlatform(configuration, environment, "KogdaIgra.Portal",
  dataProtectionConnectionStringName: "DataProtection", telemetryServiceNames: [...])` —
  DataProtection в БД, OpenTelemetry, forwarded headers, `PerRequestCache`/`SingletonCache`;
- `services.AddJoinEfCoreDbContext<KogdaIgraDbContext>(configuration, environment, "KogdaIgra")`;
- `app.MapJoinHealthChecks()` → `/health`, `/health/live`, `/health/ready`;
- `app.UseOpenTelemetryPrometheusScrapingEndpoint()`;
- `KogdaIgra.Migrator` — `AddMigrationsLauncher()` + `RegisterMigrator<T>(...)`.

Две вещи из k8s-манифеста PHP-версии закрываются этим же решением. В
`manifests/base/deployment.yaml` стоит `replicas: 1` с комментарием «Before fix it need to invent
way to share sessions» — при cookie-аутентификации и ключах DataProtection в базе ограничение
снимается. А пробы `livenessProbe`/`readinessProbe` там **закомментированы**, и ведут они ровно
на `/health/live` и `/health/ready` — то есть на адреса, которые даёт `MapJoinHealthChecks()`.

Конфигурация — только типизированные options (`AddOptions<T>().BindConfiguration(...)`), без
чтения `IConfiguration[...]` по строке, и без URL внешних сервисов в коде. Это правило взято
из `bastilia-rating/CLAUDE.md` и там сформулировано явно.

### 5. Вход и права

Вход — `AddJoinRpgAuthentication<KogdaIgraAuthDbContext>(configuration)` +
`app.MapAuthEndpoints()` + собственный `IJoinUserLoginHandler`, который сопоставляет учётку
**по адресу почты**: именно так делает PHP (`try_login_user_by_email`), и это закреплено
`OAuthLoginTests.CallbackLinksExistingAccountByEmail`.

Таблицы OpenIddict живут в **отдельном** `KogdaIgraAuthDbContext` со своей строкой подключения,
а не в основном контексте. Причина — ограничение из контекста: основную схему трогать нельзя,
пока PHP-версия способна запуститься. Так же отделена и `DataProtectionDbContext`.

Семь привилегий PHP (`USERS_CONTROL`, `EDIT_GAMES`, `EDIT_POLYGONS`, `PHOTO`, `PHOTO_SELF`,
плюс мёртвые `BASTILIA_NEWS` и `VIEW_SPB_GAMES`) становятся политиками авторизации
(`[Authorize(Policy = ...)]`) вместо guard-строки `if (!check_edit_priv()) return_to_main();`
в начале каждого файла. Источник — те же таблицы `privs`/`user_privs`, читаются через
`IAuthorizationHandler` с `PerRequestCache` из `WebInfrastructure`, а не через claims: права
выдаются и отзываются на `/edit/users/`, и они не должны ждать перелогина.

Отказ должен остаться прежним — редирект на `/`, а не 403: на это опираются
`ModerationAccessTests`, `UserAdminTests`, `PolygonAdminTests` и другие.

Три места требуют проверки при реализации, и я их не считаю решёнными:
`AddJoinRpgAuthentication` / `MapAuthEndpoints` / `IJoinUserLoginHandler` **не вызываются нигде
в самом joinrpg-net** (это API «для сателлитов», обкатанное только в TwilightRandom и
bastilia-rating); PHP запрашивает scope `openid email`, а пакет — `openid profile`, и наличие
почты в claims надо подтвердить; адреса PHP-версии `/login/joinrpg/` и `/logout/` отличаются от
пакетных `/login`, `/signin-joinrpg`, `/logout` — понадобятся переходники.

### 6. UI — Blazor Web App со статическим рендером

Страницы — статический серверный рендер (SSR); интерактивные острова включаются точечно, там
где в PHP-версии был свой JavaScript (подбор заявок allrpg в форме редактирования). Так устроен
`bastilia-rating`. Общие элементы — `JoinRpg.Common.WebComponents` (`JoinIcon`, `Tooltip`,
`DateRangeDisplay`, `JoinRpgDatePicker`, ссылки на контакты).

Razor Pages, как в `TwilightRandom`, отклонён: библиотека переиспользуемых компонентов автора —
Blazor, и в Razor Pages она подключается через теги `<component render-mode="Static">`, то есть
окольным путём.

**Форма с POST — отдельная развилка, и её надо пройти сознательно.** PHP принимает обычные
`application/x-www-form-urlencoded` POST без всякой защиты от CSRF, и compat-тесты постят ровно
так (`EditGameTests`, `AddGameApiTests`, `ReviewEditingTests`). Формы Blazor SSR требуют
antiforgery-токен, и тесты на них упадут. Решение: на этапе «1=1» легаси-формы обслуживаются
обычными POST-эндпоинтами с тем же контрактом, а antiforgery включается отдельным шагом —
как **улучшение безопасности, ломающее зафиксированный контракт**, с одновременной правкой
тестов. Это не то, что можно сделать походя.

### 7. Реестр расхождений

Заводится `docs/divergences.md` — список мест, где порт сознательно **не** повторяет PHP,
с причиной и ссылкой на issue. Туда попадают семь известных поломок, потеря строки запроса
в правилах `.htaccess` без флага `[QSA]` (`/edit/users/`, `/help-needed/email/`) и включение
antiforgery.

Тесты, закрепляющие поведение, которое мы отказываемся воспроизводить, помечаются атрибутом
`[PhpOnly(reason, issue)]` и пропускаются, когда цель — .NET. Без такого механизма переключение
`ISiteUnderTest` на порт даст пачку красных тестов, и каждый придётся разбирать заново.

### 8. Внешние интеграции

За интерфейсами в `Domain`, реализации — типизированные `HttpClient` + options:
`IEmailSender` (Mailgun), `IVkLikesClient`, `IAllrpgClient`. Требование ADR 0001 про
подменяемость выполняется само; поведение «нет ключа — тихо ничего не делаем» сохраняется,
на нём стоит вся изоляция тестов.

Одно расхождение здесь появляется сразу: PHP при недоступном VK записывает в базу ноль и
затирает накопленные лайки ([#77](https://github.com/joinrpg/kogda-igra/issues/77)). Порт при
ошибке не пишет ничего.

### 9. Миграции и денормализованные поля

Первая миграция EF Core — **базовая**, снятая с существующей схемы и не меняющая ничего;
`__EFMigrationsHistory` проставляется как применённая. Дальше обычный поток
`dotnet ef migrations add` с прогоном через `KogdaIgra.Migrator` (k8s-job перед выкладкой),
как в обоих образцах.

Пока PHP-версия способна запуститься, порт:

- не меняет существующие таблицы (добавлять новые — можно, PHP их не заметит);
- продолжает обновлять `review_count`, `photo_count` и `ki_years_cache`;
- сохраняет дедупликацию записей в `ki_updates` (повтор того же события тем же пользователем в
  пределах 15 минут обновляет время, а не добавляет строку) — на это опираются ленты.

Внешние ключи в базе отсутствуют; в модели EF связи описываются явно, а добавление настоящих
FK-ограничений — отдельная задача после отключения PHP-версии, и почти наверняка она вскроет
висячие ссылки.

### 10. Правило «текущего года»

`get_current_year()` в PHP с ноября считает текущим годом следующий, и от этого зависит главная
страница. Это доменное правило: живёт в `Domain`, зависит от `TimeProvider`, покрывается
обычными unit-тестами. Харнесс уже повторяет его в `TestData.CurrentYear`.

## Альтернативы и почему отклонены

- **Повторить раскладку joinrpg-net.** Отклонено: там 90 проектов, EF6 на MS SQL рядом с
  EF Core на Postgres, Autofac поверх встроенного DI и слой `JoinRpg.Domain`, который в
  собственной документации помечен как устаревший. Для приложения на 8300 строк это оверхед,
  а часть решений там — наследие, а не образец.
- **Три проекта (Domain + Dal + Portal), без `Services`.** Дешевле, и так сделано в
  `TwilightRandom`. Отклонено: у Twilight ядро — чистый алгоритм, а веб лишь обёртка; у нас
  ядро — операции записи с журналом, письмами и пересчётом счётчиков, и класть их в обработчики
  страниц значит воспроизвести главную беду оригинала.
- **Доменные типы = EF-сущности** (вариант TwilightRandom). Отклонено: см. раздел 2 —
  легаси-имена колонок навсегда протекут в домен.
- **Свои типизированные id вместо пакета** (так сделано в `bastilia-rating`, где в
  `Domain/Common/` лежит собственная копия `SingleValueType`/`UserIdentification`). Отклонено:
  копия без source generator, а пакет уже опубликован и поддерживается.
- **Обычные `int` для ключей** (то, что реально используют оба образца). Отклонено осознанно:
  именно путаница идентификаторов и необработанный `NULL` дают тот класс аварий, который мы
  видели в оригинале.
- **Razor Pages вместо Blazor.** См. раздел 6.
- **CQRS/MediatR, отдельные read-модели на каждый запрос.** Отклонено: масштаб не тот.
  Чтение идёт через репозитории, возвращающие доменные records.
- **Держать порт на голом ASP.NET без `JoinRpg.Common.*`.** Отклонено: логирование, телеметрия,
  health checks, DataProtection и вход через id.joinrpg.ru пришлось бы написать заново, причём
  вход — один в один то, что уже лежит в пакете.
- **`JoinRpg.Common.Telegram`** не берём: он тянет `JoinRpg.Services.Interfaces` и
  `JoinRpg.Markdown`, то есть домен чужого приложения, и на nuget.org не публикуется.

## Последствия

- Плюс: порт переиспользует обкатанную инфраструктуру, и объём «пишем с нуля» сводится к
  предметной части.
- Плюс: снимается ограничение `replicas: 1` и включаются health-пробы, уже написанные в
  манифесте PHP-версии.
- Плюс: типизированные id и nullable reference types делают невыразимым целый класс аварий
  оригинала.
- Минус: мы первый потребитель `[TypedEntityId]` и первый потребитель
  `AddJoinRpgAuthentication` за пределами двух маленьких приложений. Часть граблей соберём
  собой; о найденных заводим issue в joinrpg-net.
- Минус: раздельные доменные типы и EF-сущности — это руками написанный маппинг, которого при
  совмещённой модели не было бы.
- Минус: пять проектов вместо одного — больше церемонии на каждое изменение.
- Минус: формы Blazor SSR и antiforgery конфликтуют с зафиксированным контрактом; развилка
  отложена, но не устранена.
- Требование: версии `JoinRpg.Common.*` пиннуются в [Directory.Packages.props](../Directory.Packages.props)
  и **объявляются явно** во всех потребляющих проектах. В `bastilia-rating` `PrimitiveTypes`
  приезжает транзитивно через `WebInfrastructure` и используется без объявления — так делать
  не надо.
- Требование: схема БД заморожена на изменения до отключения PHP-версии; новые таблицы
  разрешены, правка существующих — нет.
- Требование: каждое отступление от поведения оригинала попадает в реестр расхождений,
  а не остаётся в голове у автора правки.
