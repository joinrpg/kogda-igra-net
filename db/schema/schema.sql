-- kogda-igra.ru database schema (PostgreSQL dialect).
-- Converted from the legacy phpMyAdmin MySQL dumps of the PHP site.
-- Column and table names are preserved verbatim; the application depends on them.
-- Reserved words ("order", "desc", "time", "begin", "year", "comment") are
-- double-quoted, which keeps the identifier exactly as it was in MySQL.

-- =====================================================================
-- Reference / dictionary tables
-- =====================================================================

CREATE TABLE ki_regions (
  region_id smallserial PRIMARY KEY,
  region_name varchar(100) NOT NULL DEFAULT '',
  region_code varchar(10) NOT NULL DEFAULT '',
  region_experimental smallint NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX idx_ki_regions_name_code ON ki_regions (region_name, region_code);
CREATE INDEX idx_ki_regions_experimental ON ki_regions (region_experimental);

CREATE TABLE ki_sub_regions (
  sub_region_id serial PRIMARY KEY,
  sub_region_name varchar(100) NOT NULL DEFAULT '',
  sub_region_disp_name varchar(20) NOT NULL DEFAULT '',
  region_id integer NOT NULL DEFAULT 0
);

CREATE INDEX idx_ki_sub_regions_region_id ON ki_sub_regions (region_id);

CREATE TABLE ki_game_types (
  game_type_id serial PRIMARY KEY,
  game_type_name varchar(50) NOT NULL DEFAULT '',
  show_all_regions smallint NOT NULL DEFAULT 0,
  game_type_style varchar(50) NOT NULL DEFAULT '',
  game_type_real_game smallint NOT NULL DEFAULT 0
);

CREATE INDEX idx_ki_game_types_show_all_regions ON ki_game_types (show_all_regions);

CREATE TABLE ki_status (
  status_id integer NOT NULL PRIMARY KEY,
  status_name varchar(20) NOT NULL,
  status_style varchar(20) NOT NULL,
  problem_status smallint NOT NULL,
  future_only_status smallint NOT NULL,
  cancelled_status smallint NOT NULL DEFAULT 0,
  show_review_flag smallint NOT NULL DEFAULT 0,
  show_date_flag smallint NOT NULL DEFAULT 0,
  good_status smallint NOT NULL DEFAULT 0
);

CREATE TABLE ki_update_types (
  ki_update_type_id serial PRIMARY KEY,
  ki_update_type_name varchar(50) NOT NULL DEFAULT '',
  update_type_polygon_flag smallint NOT NULL DEFAULT 0,
  update_type_game_flag smallint NOT NULL DEFAULT 0,
  update_type_photo_flag smallint NOT NULL DEFAULT 0,
  update_type_review_flag smallint NOT NULL DEFAULT 0,
  -- advertise_update_flag: "Рекламировать ли на главной странице"
  advertise_update_flag smallint NOT NULL DEFAULT 0,
  update_type_user_text varchar(50) DEFAULT NULL
);

-- =====================================================================
-- Users and privileges
-- =====================================================================

CREATE TABLE users (
  user_id serial PRIMARY KEY,
  username varchar(50) NOT NULL DEFAULT '',
  email varchar(100) NOT NULL DEFAULT '',
  -- MySQL default was the zero date '0000-00-00 00:00:00', which PostgreSQL
  -- cannot represent; the column is nullable, so the default is simply dropped.
  lastvisit timestamp DEFAULT NULL,
  create_date date DEFAULT NULL,
  editor_flag smallint NOT NULL DEFAULT 0
);

CREATE TABLE privs (
  id serial PRIMARY KEY,
  name varchar(50) NOT NULL DEFAULT '',
  "desc" varchar(150) NOT NULL DEFAULT '',
  hidden_flag smallint NOT NULL DEFAULT 0
);

CREATE TABLE user_privs (
  uid integer NOT NULL DEFAULT 0,
  pid integer NOT NULL DEFAULT 0,
  PRIMARY KEY (uid, pid)
);

-- =====================================================================
-- Core site tables
-- =====================================================================

CREATE TABLE ki_polygons (
  polygon_id serial PRIMARY KEY,
  polygon_name varchar(100) NOT NULL DEFAULT '',
  sub_region_id integer NOT NULL DEFAULT 0,
  meta_polygon integer NOT NULL DEFAULT 0,
  deleted_flag smallint NOT NULL DEFAULT 0
);

CREATE TABLE ki_games (
  id serial PRIMARY KEY,
  name varchar(100) NOT NULL DEFAULT '',
  uri varchar(100) NOT NULL DEFAULT '',
  type smallint NOT NULL DEFAULT 0,
  polygon integer NOT NULL DEFAULT 0,
  mg varchar(100) NOT NULL DEFAULT '',
  email varchar(100) NOT NULL DEFAULT '',
  show_flags integer NOT NULL DEFAULT 0,
  status smallint NOT NULL DEFAULT 0,
  "comment" varchar(100) NOT NULL DEFAULT '',
  sub_region_id integer NOT NULL DEFAULT 0,
  deleted_flag smallint NOT NULL DEFAULT 0,
  hide_email smallint NOT NULL DEFAULT 0,
  players_count integer DEFAULT NULL,
  review_count integer NOT NULL DEFAULT 0,
  allrpg_info_id integer DEFAULT NULL,
  photo_count integer NOT NULL DEFAULT 0,
  redirect_id integer DEFAULT NULL,
  vk_likes integer NOT NULL DEFAULT 0,
  vk_club varchar(40) DEFAULT NULL,
  lj_comm varchar(40) DEFAULT NULL,
  fb_comm varchar(40) DEFAULT NULL
);

CREATE INDEX idx_ki_games_type ON ki_games (type);
CREATE INDEX idx_ki_games_polygon ON ki_games (polygon);

CREATE TABLE ki_game_date (
  game_date_id serial PRIMARY KEY,
  game_id integer NOT NULL,
  "begin" date NOT NULL,
  "time" integer NOT NULL,
  hidden_flag smallint NOT NULL,
  "order" integer NOT NULL
);

CREATE INDEX idx_ki_game_date_game_id_order ON ki_game_date (game_id, "order");

CREATE TABLE ki_photo (
  photo_id serial PRIMARY KEY,
  game_id integer NOT NULL,
  photo_author varchar(40) DEFAULT NULL,
  photo_uri varchar(300) NOT NULL,
  author_id integer DEFAULT NULL,
  photo_comment varchar(200) DEFAULT NULL,
  photo_good_flag smallint NOT NULL DEFAULT 0
);

CREATE INDEX idx_ki_photo_game_id ON ki_photo (game_id);

CREATE TABLE ki_review (
  review_id serial PRIMARY KEY,
  game_id integer NOT NULL,
  author_name varchar(100) DEFAULT NULL,
  topic_id integer NOT NULL,
  review_uri varchar(200) DEFAULT NULL,
  show_review_flag smallint NOT NULL,
  author_id integer DEFAULT NULL
);

CREATE INDEX idx_ki_review_game_id ON ki_review (game_id);
CREATE INDEX idx_ki_review_game_review ON ki_review (game_id, review_id);

CREATE TABLE ki_updates (
  ki_update_id serial PRIMARY KEY,
  ki_update_type_id integer NOT NULL DEFAULT 0,
  user_id integer NOT NULL DEFAULT 0,
  -- MySQL default '0000-00-00 00:00:00' dropped (no PostgreSQL equivalent).
  update_date timestamp NOT NULL,
  game_id integer DEFAULT NULL,
  polygon_id integer DEFAULT NULL,
  photo_id integer DEFAULT NULL,
  updated_user_id integer DEFAULT NULL,
  msg varchar(300) NOT NULL,
  ip_address varchar(45) DEFAULT NULL,
  review_id integer DEFAULT NULL,
  add_uri_id integer DEFAULT NULL
);

CREATE INDEX idx_ki_updates_date_game ON ki_updates (update_date, game_id);
CREATE INDEX idx_ki_updates_photo_id ON ki_updates (photo_id);

CREATE TABLE ki_add_uri (
  add_uri_id serial PRIMARY KEY,
  uri varchar(300) DEFAULT NULL,
  allrpg_info_id integer DEFAULT NULL,
  resolved smallint NOT NULL DEFAULT 0
);

CREATE INDEX idx_ki_add_uri_resolved ON ki_add_uri (resolved);

-- allrpg_zayvka_id is spelled exactly as in the legacy dump (not a typo to fix);
-- it carries the external allrpg id, so it has no AUTO_INCREMENT.
CREATE TABLE ki_zayavka_allrpg (
  allrpg_zayvka_id integer NOT NULL PRIMARY KEY,
  game_id integer DEFAULT NULL,
  name varchar(500) NOT NULL,
  opened smallint NOT NULL DEFAULT 1
);

-- Derived cache table: no primary key in the original dump, only a unique key.
CREATE TABLE ki_years_cache (
  "year" smallint NOT NULL DEFAULT 0,
  region_id smallint NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX idx_ki_years_cache_year_region ON ki_years_cache ("year", region_id);
CREATE INDEX idx_ki_years_cache_region_id ON ki_years_cache (region_id);

CREATE TABLE old_games (
  old_game_id serial PRIMARY KEY,
  game_date varchar(300) NOT NULL,
  game_name varchar(300) NOT NULL,
  game_region varchar(300) NOT NULL,
  game_uri varchar(300) NOT NULL
);

-- =====================================================================
-- LEGACY: Bastilia news tables (originally cp1251). Kept because old code
-- may still reference them; not used by the current calendar.
-- =====================================================================

CREATE TABLE tags (
  id serial PRIMARY KEY,
  uri varchar(30) NOT NULL DEFAULT '',
  tag_name varchar(50) NOT NULL DEFAULT ''
);

CREATE UNIQUE INDEX idx_tags_uri ON tags (uri);
CREATE UNIQUE INDEX idx_tags_name ON tags (tag_name);

-- LEGACY table `news` ("News information" in the MySQL dump).
CREATE TABLE news (
  news_id serial PRIMARY KEY,
  -- MySQL default '0000-00-00 00:00:00' dropped (no PostgreSQL equivalent).
  news_date timestamp NOT NULL,
  news_author varchar(15) NOT NULL DEFAULT '',
  news_header varchar(80) NOT NULL DEFAULT '',
  news_text text NOT NULL
);

CREATE UNIQUE INDEX idx_news_date ON news (news_date);
-- MySQL FULLTEXT KEY `news_text` has no direct equivalent; replaced by a GIN
-- index over to_tsvector with an explicit ('simple') config so it is immutable.
CREATE INDEX idx_news_text_fts ON news USING gin (to_tsvector('simple', news_text));

-- LEGACY link table between `news` and `tags`.
CREATE TABLE news_tags (
  nid integer NOT NULL DEFAULT 0,
  tid integer NOT NULL DEFAULT 0,
  PRIMARY KEY (nid, tid)
);

-- =====================================================================
-- Reference data
-- =====================================================================

INSERT INTO ki_regions (region_id, region_name, region_code, region_experimental) VALUES
(1, 'Россия', '', 0),
(2, 'Северо-Запад', 'spb', 0),
(3, 'Москва и Центральный регион', 'msk', 0),
(5, 'Урал', 'ural', 0),
(6, 'Сибирь', 'sibir', 0),
(7, 'Юг России', 'south', 0),
(8, 'Дальний восток', 'dv', 0),
(9, 'Поволжье', 'volga', 0);

INSERT INTO ki_sub_regions (sub_region_id, sub_region_name, sub_region_disp_name, region_id) VALUES
(1, 'Санкт-Петербург', 'СПб', 2),
(2, 'Ленинградская область', 'СПб', 2),
(3, 'Москва', 'Мск', 3),
(4, 'Московская область', 'Мск', 3),
(5, 'Тверская область', 'Тверь', 3),
(6, 'Владимирская область', 'Влад', 3),
(7, 'Архангельская область', 'Арх', 2),
(8, 'Новгородская область', 'Новг', 2),
(9, 'Карелия', 'Карел', 2),
(10, 'Коми', 'Коми', 2),
(11, 'Вологодская область', 'Волог', 2),
(12, 'Калининградская область', 'Клнград', 2),
(13, 'Мурманская область', 'Мурм', 2),
(14, 'Псковская область', 'Псков', 2),
(15, 'Ненецкий автономный округ', 'Ненец', 2),
(17, 'Татарстан', 'Казань', 9),
(18, 'Томская область', 'Томск', 6),
(19, 'Башкортостан', 'Бшк', 9),
(20, 'Нижегородская область', 'Нижн', 9),
(21, 'Самарская область', 'Смр', 9),
(22, 'Тульская область', 'Тула', 3),
(23, 'Рязанская область', 'Ряз', 3),
(24, 'Свердловская область', 'Сврд', 5),
(25, 'Челябинская область', 'Члб', 5),
(26, 'Орловская область', 'Орёл', 3),
(27, 'Калужская область', 'Калуга', 3),
(28, 'Пермский край', 'Пермь', 9),
(29, 'Оренбургская область', 'Орен', 9),
(30, 'Тюменская область', 'Тюм', 5),
(31, 'Курганская область', 'Кург', 5),
(32, 'Курская область', 'Курск', 3),
(33, 'Белгородская область', 'Белг', 3),
(34, 'Воронежская область', 'Врнж', 3),
(35, 'Ханты-Мансийский автономный округ', 'ХМАО', 5),
(36, 'Ямало-Ненецкий автономный округ', 'ЯНАО', 5),
(37, 'Ярославская область', 'Яров', 3),
(38, 'Красноярский край', 'Крс', 6),
(39, 'Новосибирская область', 'Нвсиб', 6),
(40, 'Адыгея', 'Адг', 7),
(41, 'Астраханская область', 'Астрх', 7),
(42, 'Волгоградская область', 'Влгр', 7),
(43, 'Калмыкий', 'Клмк', 7),
(44, 'Ростовская область', 'Рств', 7),
(45, 'Краснодарский край', 'Крсдар', 7),
(46, 'Ставропольский Край', 'Стврп', 7),
(47, 'Тамбовская область', 'Тмбв', 3),
(48, 'Бурятия', 'Бурят', 6),
(49, 'Алтай (Республика)', 'Алт', 6),
(50, 'Забайкальский край', 'Збкл', 6),
(51, 'Хакасия', 'Хкс', 6),
(52, 'Тыва', 'Тыв', 6),
(53, 'Алтайский край', 'Алткр', 6),
(59, 'Саха (Якутия)', 'Якт', 8),
(55, 'Иркутская область', 'Иркт', 6),
(56, 'Кемеровская область', 'Кмрв', 6),
(57, 'Омская область', 'Омск', 6),
(60, 'Хабаровский край', 'Хабр', 8),
(61, 'Амурская область', 'Амур', 8),
(62, 'Камчатский край', 'Камч', 8),
(63, 'Магаданская область', 'Магад', 8),
(64, 'Сахалинская область', 'Схлн', 8),
(65, 'Еврейская автономная область', 'ЕАО', 8),
(66, 'Чукотский автономный округ', 'ЧАО', 8),
(67, 'Приморский край', 'Влдвст', 8),
(68, 'Костромская область', 'Костром', 3),
(69, 'Ижевская область', 'Ижв', 9),
(70, 'Кировская область', 'Крв', 9),
(71, 'Республика Марий Эл', 'МрЭл', 9),
(72, 'Мордовия', 'Мрдв', 9),
(73, 'Пензенская область', 'Пенза', 9),
(74, 'Саратовская область', 'Сртв', 9),
(75, 'Удмуртская республика', 'Удмрт', 9),
(76, 'Ульяновская область', 'Улвн', 9),
(77, 'Чувашская область', 'Чвш', 9);

INSERT INTO ki_game_types (game_type_id, game_type_name, show_all_regions, game_type_style, game_type_real_game) VALUES
(1, 'Полевая', 0, '', 1),
(2, 'Городская', 0, '', 1),
(3, 'На&nbsp;турбазе', 0, '', 1),
(4, 'Павильонная', 0, '', 0),
(5, 'Конвент', 1, '', 0),
(6, 'Бал', 0, '', 0),
(7, 'Маневры', 0, '', 0),
(8, 'Городская + Полевая', 0, '', 1),
(9, 'Городская + На турбазе', 0, '', 1),
(10, 'Турнир', 0, '', 0),
(11, 'Подземная', 0, '', 1),
(12, 'страйкбол', 0, '', 0);

INSERT INTO ki_status (status_id, status_name, status_style, problem_status, future_only_status, cancelled_status, show_review_flag, show_date_flag, good_status) VALUES
(0, 'OK', 'status-ok', 0, 1, 0, 1, 1, 1),
(1, 'Прошла', 'status-finish', 0, 0, 0, 1, 1, 1),
(2, '???', 'status-unknown', 1, 0, 0, 0, 1, 0),
(3, 'Отложена', 'status-postponedd', 0, 0, 1, 0, 1, 0),
(4, 'Дата?', 'status-date', 1, 0, 0, 0, 0, 0),
(5, 'Отменена', 'status-canceled', 0, 0, 1, 0, 1, 0);

INSERT INTO ki_update_types (ki_update_type_id, ki_update_type_name, update_type_polygon_flag, update_type_game_flag, update_type_photo_flag, update_type_review_flag, advertise_update_flag, update_type_user_text) VALUES
(1, 'Добавлена новая игра', 0, 1, 0, 0, 1, 'игра %game%'),
(2, 'Изменено описание игры', 0, 1, 0, 0, 0, NULL),
(3, 'Игра удалена из календаря', 0, 1, 0, 0, 0, NULL),
(5, 'Изменена дата игры', 0, 1, 0, 0, 0, NULL),
(6, 'Добавлена новость', 0, 0, 0, 0, 1, 'на сайте:'),
(7, 'Игра восстановлена в календаре', 0, 1, 0, 0, 0, NULL),
(8, 'У игры изменился статус', 0, 1, 0, 0, 0, NULL),
(9, 'Удален полигон', 1, 0, 0, 0, 0, NULL),
(10, 'Добавлен новый полигон', 1, 0, 0, 0, 0, NULL),
(11, 'Полигон переименован', 1, 0, 0, 0, 0, NULL),
(12, 'Полигон восстановлен', 1, 0, 0, 0, 0, NULL),
(13, 'Добавлена новая рецензия', 0, 1, 0, 1, 1, '%review_link% на игру %game% от %updated_user%'),
(14, 'Удалена рецензия', 0, 1, 0, 1, 0, NULL),
(15, 'Добавлен фотоотчет', 0, 1, 1, 0, 1, '%photo% к игре %game% от %updated_user%'),
(16, 'Изменен фотоотчет', 0, 1, 1, 0, 0, NULL),
(17, 'Удален фотоотчет', 0, 1, 1, 0, 0, NULL),
(18, 'Автор ассоциирован с ЖЖ', 0, 0, 0, 0, 0, NULL),
(19, 'Игра добавлена пользователем', 0, 1, 0, 0, 0, NULL),
(20, 'Игра проверена редактором', 0, 1, 0, 0, 1, 'игра %game%'),
(21, 'Рецензия восстановлена', 0, 1, 0, 1, 0, NULL);

INSERT INTO privs (id, name, "desc", hidden_flag) VALUES
(1, 'BASTILIA_NEWS', 'Новости на Бастилии', 1),
(2, 'USERS_CONTROL', 'Администратор пользователей', 0),
(3, 'VIEW_SPB_GAMES', 'Приватные игры в СПБ', 1),
(4, 'EDIT_GAMES', 'Редактор календаря', 0),
(5, 'EDIT_POLYGONS', 'Редактор полигонов', 0),
(6, 'PHOTO', 'Фотомодератор', 0),
(7, 'PHOTO_SELF', 'Доверенный фотограф', 0);

-- Reference rows above carry explicit ids, so the serial sequences must be
-- advanced past them or the next INSERT would collide.
SELECT setval(pg_get_serial_sequence('ki_regions', 'region_id'), (SELECT max(region_id) FROM ki_regions));
SELECT setval(pg_get_serial_sequence('ki_sub_regions', 'sub_region_id'), (SELECT max(sub_region_id) FROM ki_sub_regions));
SELECT setval(pg_get_serial_sequence('ki_game_types', 'game_type_id'), (SELECT max(game_type_id) FROM ki_game_types));
SELECT setval(pg_get_serial_sequence('ki_update_types', 'ki_update_type_id'), (SELECT max(ki_update_type_id) FROM ki_update_types));
SELECT setval(pg_get_serial_sequence('privs', 'id'), (SELECT max(id) FROM privs));
