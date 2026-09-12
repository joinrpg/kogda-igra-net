# Схема БД

## Откуда берётся

`db/schema/*.sql` — снимок **живой dev-базы** PHP-версии, снятый `utils/fetch-dev-db.ps1`.
Dev и prod — две базы на одном сервере PostgreSQL 15.18 в Yandex Cloud, поэтому снимок с dev
отражает и структуру прода.

| Файл | Что внутри | Чем снят |
|---|---|---|
| `schema/schema.sql` | Структура: 21 таблица, индексы, последовательности | `pg_dump --schema-only` |
| `schema/reference-data.sql` | Справочники: регионы, субрегионы, типы игр, статусы, типы обновлений, привилегии | `pg_dump --data-only --table=...` |
| `local/snapshot.sql` | Полный дамп с данными | `pg_dump`, только с `-WithData` |

`db/local/` **заигнорен и не коммитится**: в `users` и `ki_games` лежат адреса электронной
почты, причём у игр есть флаг `hide_email`. Эти данные не должны попадать в репозиторий.

## Почему снимок, а не миграции

Раньше здесь лежала схема, переведённая вручную из MySQL-дампов 2012 года
(`mysql-db-structure/*.sql` в PHP-репозитории), плюс каталог `db/migrations/` с
инкрементальными миграциями. От этого отказались: снимок живой базы всегда точен, а дампы
2012 года требовали ручной конвертации диалекта и отставали на все миграции.

Каталога `db/migrations/` больше нет **намеренно**. Снимок уже содержит все применённые
миграции, и повторное их применение упало бы с `column already exists`.

## Как обновить после изменений в PHP-версии

Когда в PHP-репозитории появилась миграция и её накатили на dev — переснять снимок:

```powershell
$env:KOGDA_DEV_PASSWORD = '<пароль kogda-dev>'
./utils/fetch-dev-db.ps1
```

Отдельно копировать `db-migrations/*.sql` из PHP-репозитория не нужно.

Версия `pg_dump` не может быть ниже версии сервера. Если сервер подняли — передать нужную:

```powershell
./utils/fetch-dev-db.ps1 -PostgresImage postgres:16
```

## Как применяется в тестах

`DatabaseSchema` копирует оба файла в контейнер и выполняет их через `psql`, а не через
Npgsql. Это принципиально: вывод `pg_dump` — скрипт для `psql`, а не чистый SQL. В нём есть
мета-команды (`\restrict`, `\unrestrict`) и блоки `COPY ... FROM stdin` с терминатором `\.`,
которые обычное соединение выполнить не может.

Порядок применения: `schema.sql`, затем `reference-data.sql`.

## Как проверить вручную

```bash
docker run -d --name kicheck -e POSTGRES_PASSWORD=pass -e POSTGRES_DB=kogdaigra postgres:15.18
docker cp db/schema/schema.sql kicheck:/schema.sql
docker cp db/schema/reference-data.sql kicheck:/reference.sql
docker exec kicheck psql -U postgres -d kogdaigra -v ON_ERROR_STOP=1 -f /schema.sql
docker exec kicheck psql -U postgres -d kogdaigra -v ON_ERROR_STOP=1 -f /reference.sql
docker exec kicheck psql -U postgres -d kogdaigra -c "\dt"
docker rm -f kicheck
```

Проще — `dotnet test`: харнесс делает ровно это.
