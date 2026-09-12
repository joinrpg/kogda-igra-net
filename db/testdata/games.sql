-- Тестовые игры для compat-тестов.
--
-- Данные придуманы, а не взяты из дампа: в реальных таблицах лежат адреса почты
-- организаторов (в том числе скрытые флагом hide_email), их нельзя коммитить.
--
-- Состав набора продиктован тем, как PHP отбирает игры (appcode/logic/gamebase.php,
-- функция _get_games): почти все связи там INNER JOIN, поэтому игра без строки
-- в ki_polygons / ki_sub_regions / ki_game_types / ki_status / ki_updates
-- не покажется нигде. Особенно легко забыть ki_updates.
--
-- Идентификаторы взяты с большим отрывом (9xx), чтобы не столкнуться со справочниками.

-- Полигон. Субрегион 3 (Москва) и тип игры 1 (Полевая) уже есть в справочниках.
INSERT INTO ki_polygons (polygon_id, polygon_name, sub_region_id, meta_polygon, deleted_flag)
VALUES (900, 'Тестовый полигон', 3, 0, 0);

-- Обычная активная игра.
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes)
VALUES (901, 'Тестовая игра про дракона', 'https://example.com/dragon', 1, 900,
        'МГ Тестовая', 'dragon@example.com', 0, 0, '', 3, 0, 0, 120, 0, 0, 0);

-- Удалённая игра: deleted_flag = 1.
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes)
VALUES (902, 'Удалённая тестовая игра', '', 1, 900, '', '', 0, 0, '', 3, 1, 0, NULL, 0, 0, 0);

-- Игра на модерации: deleted_flag = -1 (добавлена пользователем, ещё не одобрена).
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes)
VALUES (903, 'Игра на модерации', '', 1, 900, '', '', 0, 0, '', 3, -1, 0, NULL, 0, 0, 0);

-- Игра со скрытой почтой: hide_email = 1. Анонимному пользователю PHP не отдаёт
-- адрес вообще (calendar.php: при hide_email и !check_username возвращается пустая строка).
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes)
VALUES (904, 'Игра со скрытой почтой', '', 1, 900, 'МГ Скрытная', 'secret@example.com',
        0, 0, '', 3, 0, 1, NULL, 0, 0, 0);

-- Даты. "order" = 0 — текущая дата игры; "time" — длительность В ДНЯХ, считая с единицы
-- (calendar.php: days = time - 1). hidden_flag = 1 убрал бы дату из выдачи совсем.
-- Год подставляется харнессом: PHP берёт текущий год из date() в зоне Europe/Moscow.
INSERT INTO ki_game_date (game_date_id, game_id, "begin", "time", hidden_flag, "order")
VALUES (9001, 901, make_date(:year, 7, 15), 3, 0, 0),
       -- Перенесённая старая дата той же игры: order > 0, показывается как «Перенесена!».
       (9002, 901, make_date(:year, 5, 1), 2, 0, 1),
       (9003, 902, make_date(:year, 7, 20), 1, 0, 0),
       (9004, 903, make_date(:year, 7, 25), 1, 0, 0),
       (9005, 904, make_date(:year, 8, 10), 1, 0, 0);

-- ki_updates присоединяется через INNER JOIN (MAX(update_date) по игре).
-- Без строки здесь игра не появится ни на одной странице.
INSERT INTO ki_updates (ki_update_id, game_id, update_date, ki_update_type_id, user_id)
VALUES (9101, 901, make_date(:year, 1, 10), 1, 0),
       (9102, 902, make_date(:year, 1, 10), 1, 0),
       (9103, 903, make_date(:year, 1, 10), 1, 0),
       (9104, 904, make_date(:year, 1, 10), 1, 0);

-- Кэш годов: на список игр не влияет, но без него пустует навигация по годам
-- (top_menu.php: write_years_list). Заполняется тем же запросом, что и
-- internal_do_update_year_index() в gamebase.php.
DELETE FROM ki_years_cache;
INSERT INTO ki_years_cache (year, region_id)
SELECT DISTINCT date_part('year', kgd."begin"), ksr.region_id
FROM ki_games kg
INNER JOIN ki_game_date kgd ON kgd.game_id = kg.id
INNER JOIN ki_sub_regions ksr ON ksr.sub_region_id = kg.sub_region_id
WHERE kg.deleted_flag = 0;
