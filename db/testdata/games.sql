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
VALUES (900, 'Тестовый полигон', 3, 0, 0),
       (901, 'Полигон под Петербургом', 1, 0, 0);

-- Обычная активная игра.
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes, allrpg_info_id)
VALUES (901, 'Тестовая игра про дракона', 'https://example.com/dragon', 1, 900,
        'МГ Тестовая', 'dragon@example.com', 0, 0, 'примечаниепроболото', 3, 0, 0, 120, 0, 0, 0, 12345);

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

-- Игра в другом регионе: субрегион 1 (Санкт-Петербург) относится к региону 2,
-- тогда как остальные тестовые игры — к региону 3 (Москва). Нужна, чтобы проверять
-- фильтр по регионам: /spb/ против /msk/.
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes)
VALUES (905, 'Петербургская тестовая игра', '', 1, 901, 'МГ Невская', 'spb@example.com',
        0, 0, '', 1, 0, 0, 50, 0, 0, 0);

-- Даты. "order" = 0 — текущая дата игры; "time" — длительность В ДНЯХ, считая с единицы
-- (calendar.php: days = time - 1). hidden_flag = 1 убрал бы дату из выдачи совсем.
-- Год подставляется харнессом: PHP берёт текущий год из date() в зоне Europe/Moscow.
INSERT INTO ki_game_date (game_date_id, game_id, "begin", "time", hidden_flag, "order")
VALUES (9001, 901, make_date(:year, 7, 15), 3, 0, 0),
       -- Перенесённая старая дата той же игры: order > 0, показывается как «Перенесена!».
       (9002, 901, make_date(:year, 5, 1), 2, 0, 1),
       (9003, 902, make_date(:year, 7, 20), 1, 0, 0),
       (9004, 903, make_date(:year, 7, 25), 1, 0, 0),
       (9005, 904, make_date(:year, 8, 10), 1, 0, 0),
       (9006, 905, make_date(:year, 9, 5), 2, 0, 0);

-- ki_updates присоединяется через INNER JOIN (MAX(update_date) по игре).
-- Без строки здесь игра не появится ни на одной странице.
INSERT INTO ki_updates (ki_update_id, game_id, update_date, ki_update_type_id, user_id)
VALUES (9101, 901, make_date(:year, 1, 10), 1, 0),
       (9102, 902, make_date(:year, 1, 10), 1, 0),
       (9103, 903, make_date(:year, 1, 10), 1, 0),
       (9104, 904, make_date(:year, 1, 10), 1, 0),
       (9105, 905, make_date(:year, 1, 10), 1, 0);

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

-- Пользователь-модератор для тестов раздела /edit/.
-- Вход в PHP-версии идёт через OAuth JoinRPG, который в тестах намеренно заглушён
-- отсутствием ключей. Но сама авторизация опирается только на $_SESSION['user_id']
-- (appcode/user_funcs.php), а права — на таблицы privs/user_privs. Поэтому харнесс
-- подкладывает готовую сессию, а не проходит OAuth: PHP-код при этом не меняется.
INSERT INTO users (user_id, username, email, lastvisit, create_date, editor_flag)
VALUES (9900, 'testmoderator', 'moderator@example.com', NOW(), CURRENT_DATE, 1);

-- pid = 4 — это EDIT_GAMES, «Редактор календаря» (справочник privs).
INSERT INTO user_privs (uid, pid) VALUES (9900, 4);

-- Свежие записи журнала для лент изменений.
-- Ленты отбирают обновления за последние три месяца ((NOW() - INTERVAL '3 MONTHS')
-- < update_date, appcode/logic/updates.php), поэтому даты выше, привязанные к январю,
-- в них не попадают — нужны заведомо недавние.
--
-- Тип 2 — «Изменено описание игры».
INSERT INTO ki_updates (ki_update_id, game_id, update_date, ki_update_type_id, user_id, ip_address)
VALUES
  -- Правка модератора: видна в его личной ленте и НЕ видна в «Патруле»,
  -- который показывает чужие изменения.
  (9201, 901, NOW() - INTERVAL '1 day', 2, 9900, NULL),
  -- Анонимная правка с адресом: попадает и в «Патруль», и в ленту по IP.
  (9202, 905, NOW() - INTERVAL '2 days', 2, 0, '203.0.113.7');

-- Игры для очередей модерации /edit/problems/*.
-- Условия очередей — appcode/logic/problems.php.

-- Полигон-заглушка: meta_polygon = 1 означает «полигон толком не указан»,
-- на этом и держится очередь «Нет полигона».
INSERT INTO ki_polygons (polygon_id, polygon_name, sub_region_id, meta_polygon, deleted_flag)
VALUES (902, 'Полигон не указан', 3, 1, 0);

-- Пустой email (LENGTH(email) = 0) — очередь «Нет e-mail».
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes, allrpg_info_id)
VALUES (906, 'Игра без почты', '', 1, 900, 'МГ Безмолвная', '', 0, 0, '', 3, 0, 0, 30, 0, 0, 0, 20001);

-- Статус 2 («???») имеет problem_status = 1 — очередь «Прошедшие?».
-- Полигон 902 с meta_polygon = 1 — заодно очередь «Нет полигона».
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes, allrpg_info_id)
VALUES (907, 'Игра с неясным статусом', '', 1, 902, 'МГ Туманная', 'foggy@example.com',
        0, 2, '', 3, 0, 0, 40, 0, 0, 0, 20002);

INSERT INTO ki_game_date (game_date_id, game_id, "begin", "time", hidden_flag, "order")
VALUES (9007, 906, make_date(:year, 6, 1), 1, 0, 0),
       (9008, 907, make_date(:year, 6, 10), 1, 0, 0);

INSERT INTO ki_updates (ki_update_id, game_id, update_date, ki_update_type_id, user_id)
VALUES (9106, 906, make_date(:year, 1, 10), 1, 0),
       (9107, 907, make_date(:year, 1, 10), 1, 0);

-- Две игры для очереди «Прошедшие?». Она отбирает игры со статусом, у которого
-- future_only_status = 1 (это статус 0 «OK»), чья дата уже прошла: смысл очереди —
-- «стоит OK, а игра давно прошла, обновите статус».
--
-- Даты нарочно вынесены в соседние годы, а не в текущий: иначе результат зависел бы
-- от того, в каком месяце запускают тесты. На календарь текущего года эти игры
-- не влияют — он фильтрует по году.
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes, allrpg_info_id)
VALUES (908, 'Игра, которая заведомо прошла', '', 1, 900, 'МГ Прошлая', 'past@example.com',
        0, 0, '', 3, 0, 0, 25, 0, 0, 0, 20003),
       (909, 'Игра, которая заведомо впереди', '', 1, 900, 'МГ Будущая', 'future@example.com',
        0, 0, '', 3, 0, 0, 25, 0, 0, 0, 20004);

INSERT INTO ki_game_date (game_date_id, game_id, "begin", "time", hidden_flag, "order")
VALUES (9009, 908, make_date(:year - 1, 6, 1), 1, 0, 0),
       (9010, 909, make_date(:year + 1, 6, 1), 1, 0, 0);

INSERT INTO ki_updates (ki_update_id, game_id, update_date, ki_update_type_id, user_id)
VALUES (9108, 908, make_date(:year, 1, 10), 1, 0),
       (9109, 909, make_date(:year, 1, 10), 1, 0);

-- Две игры с одинаковым названием — очередь «Слияние» ищет именно дубли имён
-- (GROUP BY name HAVING COUNT(id) > 1), см. get_merge_candidates.
INSERT INTO ki_games (id, name, uri, type, polygon, mg, email, show_flags, status,
                      comment, sub_region_id, deleted_flag, hide_email, players_count,
                      review_count, photo_count, vk_likes, allrpg_info_id)
VALUES (910, 'Игра-двойник', '', 1, 900, 'МГ Первая', 'first@example.com',
        0, 0, '', 3, 0, 0, 10, 0, 0, 0, 20005),
       (911, 'Игра-двойник', '', 1, 900, 'МГ Вторая', 'second@example.com',
        0, 0, '', 3, 0, 0, 10, 0, 0, 0, 20006),
-- Пустая почта у игры в будущем: очереди с суффиксом -future добавляют
-- условие kgd.begin > NOW(). Год взят следующий, чтобы результат не зависел
-- от месяца прогона.
       (912, 'Грядущая игра без адреса', '', 1, 900, 'МГ Грядущая', '',
        0, 0, '', 3, 0, 0, 15, 0, 0, 0, 20007);

INSERT INTO ki_game_date (game_date_id, game_id, "begin", "time", hidden_flag, "order")
VALUES (9011, 910, make_date(:year, 4, 5), 1, 0, 0),
       (9012, 911, make_date(:year, 4, 6), 1, 0, 0),
       (9013, 912, make_date(:year + 1, 4, 7), 1, 0, 0);

INSERT INTO ki_updates (ki_update_id, game_id, update_date, ki_update_type_id, user_id)
VALUES (9110, 910, make_date(:year, 1, 10), 1, 0),
       (9111, 911, make_date(:year, 1, 10), 1, 0),
       (9112, 912, make_date(:year, 1, 10), 1, 0);

-- Администратор пользователей. Права в проекте разделены: раздел /edit/users/
-- закрыт правом USERS_CONTROL (pid = 2), а не EDIT_GAMES (pid = 4), поэтому
-- модератора календаря туда не пускают.
INSERT INTO users (user_id, username, email, lastvisit, create_date, editor_flag)
VALUES (9901, 'testadmin', 'admin@example.com', NOW(), CURRENT_DATE, 1),
       -- Ещё один пользователь без прав: на нём проверяются выдача и отзыв привилегий.
       (9902, 'testplain', 'plain@example.com', NOW(), CURRENT_DATE, 0);

INSERT INTO user_privs (uid, pid) VALUES (9901, 2);

-- Редактор полигонов: раздел /edit/polygons/ закрыт правом EDIT_POLYGONS (pid = 5),
-- то есть третьим, отличным и от EDIT_GAMES, и от USERS_CONTROL.
INSERT INTO users (user_id, username, email, lastvisit, create_date, editor_flag)
VALUES (9903, 'testpolygons', 'polygons@example.com', NOW(), CURRENT_DATE, 1);

INSERT INTO user_privs (uid, pid) VALUES (9903, 5);

-- Полигон, на котором не стоит ни одной игры: только такие разрешено удалять
-- (кнопка удаления рисуется, когда COUNT(kg.id) = 0).
INSERT INTO ki_polygons (polygon_id, polygon_name, sub_region_id, meta_polygon, deleted_flag)
VALUES (903, 'Пустующий полигон', 3, 0, 0);
