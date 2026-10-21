# Покрытие точек входа PHP-версии

Таблица показывает, какие страницы и эндпоинты оригинала уже закреплены compat-тестами,
а какие нет. Это основной ориентир: **портировать раздел, у которого нет зелёного
compat-теста, — значит портировать вслепую.**

Точки входа перечислены по файлам `*.php` в `public_html/` PHP-репозитория, исключая
`appcode/` и `common/` (это библиотеки, а не страницы).

Считано на 235 тестах. Покрыто **все 56** точек входа (100%).

## Покрыто

| Точка входа | Чем закреплено |
|---|---|
| `/` | `HomePageTests`, `RegionFilterTests`, `YearNavigationTests` |
| `/game/<id>/` | `GamePageTests`, `ReviewTests` |
| `/search/` (через `/find/<запрос>`) | `SearchTests` |
| `/search.php` | `SearchTests` (легаси-редирект) |
| `/archive/` | `ArchiveTests` |
| `/reviews/` | `ReviewTests` |
| `/stat/` | `StatisticsTests` |
| `/user/<имя>` | `UserProfileTests` |
| `/api/game/get-by-id.php` | `GameApiTests` |
| `/api/game/changed.php` | `ChangedGamesApiTests` |
| `/api/game/add.php` | `AddGameApiTests` |
| `/api/search-suggest/` | `SearchSuggestApiTests` |
| `/api/allrpg-info/find-by-id.php` | `AllrpgApiTests` (фиксирует поломку, [#74](https://github.com/joinrpg/kogda-igra/issues/74)) |
| `/api/allrpg-info/update-allrpg.php` | `AllrpgApiTests` (фиксирует поломку, [#74](https://github.com/joinrpg/kogda-igra/issues/74)) |
| `/edit/` | `ModerationAccessTests`, `ProblemQueueTests` |
| `/edit/game/` | `EditGameTests`, `ReviewTests` |
| `/edit/users/` | `UserAdminTests` |
| `/edit/polygons/` | `PolygonAdminTests` |
| `/edit/problems/*` — все 12 очередей | `ProblemQueueTests`, `ProblemQueueExtraTests` |
| `/lenta/*` — все 7 лент | `UpdatesFeedTests` |
| `/about/` | `AboutPageTests` |
| `/404/`, `/novice/spb/`, `/logout/`, `/service/message.php` | `ServicePagesTests` |
| `/edit/users/by-email/` | `ServicePagesTests` |
| `/api/`, короткие адреса `/api/game/<id>`, `/api/changed/<ts>`, `/api/allrpg-info/<id>` | `CanonicalApiUrlTests` |
| `/photo/`, `/edit/photo/` | `PhotoAndHelpTests` (правка фото ломается, [#81](https://github.com/joinrpg/kogda-igra/issues/81)) |
| `/help-needed/`, `/help-needed/email/` | `PhotoAndHelpTests` (список сломан, [#82](https://github.com/joinrpg/kogda-igra/issues/82)) |
| `/data/export/excel/` | `PhotoAndHelpTests` |
| `/edit/zayavka/allrpg/` | `PhotoAndHelpTests` |
| `/login/joinrpg/` | `OAuthLoginTests` (против поддельного сервера входа) |
| `/api/game/update-likes.php` | `VkLikesApiTests` (фиксирует поломку, [#77](https://github.com/joinrpg/kogda-igra/issues/77)) |
| `/noemail/` | `PublicListingTests` |
| `/data/mg/` | `PublicListingTests` |
| `/edit/deleted-games/` | `DeletedGamesTests` |
| `/edit/merge/` | `MergePageTests` (фиксирует поломку, [#76](https://github.com/joinrpg/kogda-igra/issues/76)) |

## Не покрыто

| Точка входа | Что это | Почему отложено |
|---|---|---|
| `/login/joinrpg/` — обратный вызов с настоящего сервера | Вход через OAuth | Сам поток покрыт против поддельного сервера (`OAuthLoginTests`). Не проверяется только совместимость с настоящим id.joinrpg.ru |

Всё остальное покрыто.

## Как это измерялось

Список точек входа — перечисление файлов; отметки о покрытии — сопоставление с адресами,
которые реально запрашивают тесты. Это **покрытие по эндпоинтам, а не по строкам**: оно
не говорит, сколько ветвлений внутри страницы проверено.

## Построчное покрытие строк

Снимается Xdebug'ом, **без единой правки в PHP-репозитории**: образ собирается поверх
опубликованного. Как снять и что означают цифры — [coverage/README.md](../coverage/README.md).

Последний замер на 235 тестах: **3112 строк из 3977 (78.2%)** — и это полная цифра,
без оговорок: теперь **все 95 PHP-файлов** хотя бы раз подключались, так что оценивать
незагруженную часть больше не нужно.

Оставшиеся аутсайдеры — сломанные разделы
([#74](https://github.com/joinrpg/kogda-igra/issues/74),
[#75](https://github.com/joinrpg/kogda-igra/issues/75),
[#76](https://github.com/joinrpg/kogda-igra/issues/76),
[#81](https://github.com/joinrpg/kogda-igra/issues/81),
[#82](https://github.com/joinrpg/kogda-igra/issues/82)): их код обрывается на фатальной
ошибке, и дальше строки просто не исполняются. Фиксировать контракт того, что всё равно
падает, смысла нет — сначала нужно решение по самим багам.
