# Покрытие точек входа PHP-версии

Таблица показывает, какие страницы и эндпоинты оригинала уже закреплены compat-тестами,
а какие нет. Это основной ориентир: **портировать раздел, у которого нет зелёного
compat-теста, — значит портировать вслепую.**

Точки входа перечислены по файлам `*.php` в `public_html/` PHP-репозитория, исключая
`appcode/` и `common/` (это библиотеки, а не страницы).

Считано на 189 тестах. Покрыто **41 из 56** точек входа (73%).

## Покрыто

| Точка входа | Чем закреплено |
|---|---|
| `/` | `HomePageTests`, `RegionFilterTests`, `YearNavigationTests` |
| `/game/<id>/` | `GamePageTests`, `ReviewTests` |
| `/search/` (через `/find/<запрос>`) | `SearchTests` |
| `/search.php` | `SearchTests` (легаси-редирект) |
| `/archive/` | `ArchiveTests` (фиксирует поломку, [#69](https://github.com/joinrpg/kogda-igra/issues/69)) |
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
| `/noemail/` | `PublicListingTests` |
| `/data/mg/` | `PublicListingTests` |
| `/edit/deleted-games/` | `DeletedGamesTests` |
| `/edit/merge/` | `MergePageTests` (фиксирует поломку, [#76](https://github.com/joinrpg/kogda-igra/issues/76)) |

## Не покрыто

| Точка входа | Что это | Почему отложено |
|---|---|---|
| `/photo/` | Список игр с фото | Раздел сломан целиком — [#75](https://github.com/joinrpg/kogda-igra/issues/75). Фиксировать контракт до решения по превью бессмысленно |
| `/edit/photo/` | Модерация фото | То же |
| `/api/game/update-likes.php` | Обновление лайков ВКонтакте | Единственный исходящий вызов наружу (`api.vkontakte.ru`, устаревший эндпоинт). Нужен способ заглушить сеть, не подменяя типы |
| `/login/joinrpg/` | Вход через OAuth JoinRPG | Требует внешнего сервиса. Харнесс обходит вход засевом сессии |
| `/logout/` | Выход | Тривиален, но осмысленен только в паре с входом |
| `/about/`, `/novice/spb/`, `/404/` | Статические страницы | Логики нет, ценность теста низкая |
| `/api/` | Страница описания API | Статическая |
| `/help-needed/`, `/help-needed/email/` | Игры, которым нужна помощь, и форма отправки | Форма шлёт почту — покрывать вместе с проверкой, что письмо не уходит |
| `/edit/zayavka/allrpg/` | Очередь заявок из allrpg.info | Связано с [#74](https://github.com/joinrpg/kogda-igra/issues/74) |
| `/data/export/excel/` | Выгрузка в Excel | Ответ — бинарный файл, нужен отдельный способ проверки |
| `/service/message.php` | Показ произвольного сообщения | Вспомогательная |
| `/edit/users/by-email/` | — | **Файл пустой** (321 байт, без кода). Похоже, заготовка |

## Как это измерялось

Список точек входа — перечисление файлов; отметки о покрытии — сопоставление с адресами,
которые реально запрашивают тесты. Это **покрытие по эндпоинтам, а не по строкам**: оно
не говорит, сколько ветвлений внутри страницы проверено.

## Построчное покрытие строк

Снимается Xdebug'ом, **без единой правки в PHP-репозитории**: образ собирается поверх
опубликованного. Как снять и что означают цифры — [coverage/README.md](../coverage/README.md).

Последний замер на 189 тестах: **2679 строк из 3470 (77.2%)** в 77 файлах, плюс 18
файлов, не подключавшихся ни разу.

Низкое покрытие отдельных файлов в основном честно отражает сломанные разделы
([#69](https://github.com/joinrpg/kogda-igra/issues/69),
[#74](https://github.com/joinrpg/kogda-igra/issues/74),
[#75](https://github.com/joinrpg/kogda-igra/issues/75),
[#76](https://github.com/joinrpg/kogda-igra/issues/76)), а не пробелы в тестах.
Два пробела, которые замер выявил, уже закрыты: `appcode/best_calendar.php` (7% → 90%)
и `appcode/logic/review.php` (18% → 81%). Оба раздела по таблице точек входа выглядели
покрытыми — страницы открывались и отвечали, — но ветки внутри не исполнялись.
