# База данных

Здесь лежит схема БД kogda-igra.ru в том виде, в котором её накатывают тесты на чистый Postgres.

- `schema/schema.sql` — полная схема (21 таблица) плюс засеянные справочники.
- `migrations/*.sql` — миграции, скопированные из PHP-репозитория **дословно**.

Порядок применения: сначала `schema/schema.sql`, затем `migrations/*.sql` в алфавитном порядке —
ровно так же, как это происходит на живой базе PHP-версии. Реализация —
`src/KogdaIgra.CompatTests/DatabaseSchema.cs`.

## Откуда это взялось

`schema/schema.sql` **сконвертирован вручную** из legacy-дампов phpMyAdmin в MySQL-диалекте,
лежащих в PHP-репозитории: `..\kogda-igra\mysql-db-structure\*.sql`. Прод PHP-версии давно на
Postgres, но исторические дампы структуры остались в MySQL-формате.

Оригинальные дампы **остаются в PHP-репозитории как архив** и сюда не копируются. Их не трогать.

`migrations/` — копия `..\kogda-igra\db-migrations\`, файл в файл, без правок.

Таблицы: `ki_regions`, `ki_sub_regions`, `ki_game_types`, `ki_status`, `ki_update_types`, `users`,
`privs`, `user_privs`, `ki_polygons`, `ki_games`, `ki_game_date`, `ki_photo`, `ki_review`,
`ki_updates`, `ki_add_uri`, `ki_zayavka_allrpg`, `ki_years_cache`, `old_games`, `tags`, `news`,
`news_tags`.

Засеянные справочные данные: регионы, суб-регионы, типы игр, статусы, типы обновлений (`ki_update_types`),
привилегии (`privs`). Compat-тесты на них опираются — например, `SmokeTests` проверяет, что на
главной видна «Москва».

## Решения при конвертации MySQL → PostgreSQL

Имена таблиц и колонок сохранены **дословно** — приложение от них зависит. Кроме этого:

- **Нулевые даты.** MySQL-дефолт `'0000-00-00'` / `'0000-00-00 00:00:00'` не имеет эквивалента в
  PostgreSQL, поэтому такие DEFAULT просто убраны (места помечены комментарием в `schema.sql`).
  Это отличие поведения, о нём надо помнить при портировании кода, который эти значения читает.
- **Зарезервированные слова.** `order`, `begin`, `time`, `comment`, `year`, `desc` — идентификаторы
  в двойных кавычках (`"order"`), что сохраняет имя ровно таким, каким оно было в MySQL.
- **FULLTEXT.** MySQL-индекс `FULLTEXT KEY news_text` прямого аналога не имеет; заменён на GIN-индекс
  по `to_tsvector` с явной конфигурацией `'simple'` — явная конфигурация нужна, чтобы выражение было
  immutable и годилось для индекса:
  ```sql
  CREATE INDEX idx_news_text_fts ON news USING gin (to_tsvector('simple', news_text));
  ```
- **Последовательности.** Справочные строки вставляются с явными id, поэтому в конце файла
  последовательности сдвигаются `setval(pg_get_serial_sequence(...), max(...))` — иначе первый же
  INSERT приложения словил бы конфликт по первичному ключу.

## Как добавить миграцию

Когда в PHP-репозитории появился новый файл в `db-migrations/`:

1. Скопировать его **как есть** в `db/migrations/`. Не переписывать, не переименовывать, не
   «улучшать»: расхождение между репозиториями — источник неуловимых различий в поведении.
2. Убедиться, что алфавитный порядок имён даёт правильный порядок применения.
3. Прогнать compat-тесты — они накатывают схему с нуля и упадут с именем файла в сообщении, если
   миграция не применилась.

В `schema/schema.sql` изменения из миграции **не вносить**: схема — это исходная точка, миграции
накатываются поверх, как на живой базе.

## Как проверить, что схема накатывается

Быстрая проверка без тестов:

```bash
docker run --rm -d --name kogda-schema-check \
  -e POSTGRES_DB=kogdaigra -e POSTGRES_USER=kogdauser -e POSTGRES_PASSWORD=kogdapass \
  -p 7433:5432 postgres:14.2

PGPASSWORD=kogdapass psql -h localhost -p 7433 -U kogdauser -d kogdaigra \
  -v ON_ERROR_STOP=1 -f db/schema/schema.sql

PGPASSWORD=kogdapass psql -h localhost -p 7433 -U kogdauser -d kogdaigra \
  -v ON_ERROR_STOP=1 -f db/migrations/add_telegram_fields.sql

docker rm -f kogda-schema-check
```

`-v ON_ERROR_STOP=1` обязателен: без него `psql` проглотит ошибку и завершится с кодом 0.

Версия `postgres:14.2` — та же, что на проде PHP-версии и в `PhpSiteUnderTest`; проверять на другой
версии смысла мало.
