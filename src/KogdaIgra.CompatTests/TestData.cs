using DotNet.Testcontainers.Containers;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Засев тестовых игр поверх схемы и справочников.
/// Сами данные — в <c>db/testdata/games.sql</c>; они придуманы, а не взяты из дампа,
/// потому что в реальных таблицах лежат адреса почты организаторов.
/// </summary>
public static class TestData
{
    /// <summary>Идентификаторы засеянных игр, чтобы тесты не писали магические числа.</summary>
    public static class Games
    {
        /// <summary>Обычная активная игра с двумя датами (текущей и перенесённой).</summary>
        public const int Active = 901;

        /// <summary>Игра с <c>deleted_flag = 1</c>.</summary>
        public const int Deleted = 902;

        /// <summary>Игра с <c>deleted_flag = -1</c> — добавлена пользователем, ждёт модерации.</summary>
        public const int AwaitingModeration = 903;

        /// <summary>Игра с <c>hide_email = 1</c>: адрес не должен утечь в разметку.</summary>
        public const int WithHiddenEmail = 904;

        /// <summary>Игра в субрегионе Санкт-Петербург (регион 2) — остальные в регионе 3.</summary>
        public const int InSaintPetersburg = 905;

        /// <summary>Игра с пустым <c>email</c> — попадает в очередь «Нет e-mail».</summary>
        public const int WithoutEmail = 906;

        /// <summary>Игра с отзывами: два видимых и один скрытый.</summary>
        public const int WithReviews = 913;

        /// <summary>
        /// Игра со статусом «???» (<c>problem_status = 1</c>) на полигоне-заглушке
        /// (<c>meta_polygon = 1</c>) — попадает сразу в две очереди.
        /// </summary>
        public const int WithUnclearStatus = 907;
    }

    /// <summary>Название активной игры — ожидаемое значение для ассертов.</summary>
    public const string ActiveGameName = "Тестовая игра про дракона";

    /// <summary>
    /// Слово, которое встречается только в поле <c>comment</c> активной игры.
    /// Поиск по нему ничего не находит — <c>comment</c> в поиск не входит.
    /// </summary>
    public const string CommentOnlyPhrase = "примечаниепроболото";

    /// <summary>
    /// Адрес, с которого сделана засеянная анонимная правка. Из документационного
    /// диапазона RFC 5737 — настоящим он быть не может.
    /// </summary>
    public const string AnonymousUpdateIp = "203.0.113.7";

    /// <summary>Идентификатор засеянного пользователя с правом EDIT_GAMES.</summary>
    public const int ModeratorUserId = 9900;

    /// <summary>Имя засеянного модератора — оно выводится в меню после входа.</summary>
    public const string ModeratorUserName = "testmoderator";

    /// <summary>Пользователь с правом USERS_CONTROL — он же администратор учёток.</summary>
    public const int AdminUserId = 9901;

    /// <summary>Имя администратора учёток.</summary>
    public const string AdminUserName = "testadmin";

    /// <summary>Пользователь без единой привилегии — «подопытный» для выдачи прав.</summary>
    public const int PlainUserId = 9902;

    /// <summary>Имя пользователя без привилегий.</summary>
    public const string PlainUserName = "testplain";

    /// <summary>Идентификатор права EDIT_GAMES в справочнике privs.</summary>
    public const int EditGamesPrivilegeId = 4;

    /// <summary>Пользователь с правом EDIT_POLYGONS.</summary>
    public const int PolygonEditorUserId = 9903;

    /// <summary>Пользователь с правом PHOTO — модератор фотографий.</summary>
    public const int PhotoModeratorUserId = 9904;

    /// <summary>Автор засеянной фотографии.</summary>
    public const string PhotoAuthorName = "Фотограф Тестовый";

    /// <summary>Полигон, на котором стоят игры, — удалить его нельзя.</summary>
    public const int PolygonWithGames = 900;

    /// <summary>Полигон без игр — единственный вид, который разрешено удалять.</summary>
    public const int EmptyPolygonId = 903;

    /// <summary>Название полигона без игр.</summary>
    public const string EmptyPolygonName = "Пустующий полигон";

    /// <summary>Полигон-заглушка (<c>meta_polygon = 1</c>) — в списке не показывается.</summary>
    public const string MetaPolygonName = "Полигон не указан";

    /// <summary>
    /// <c>allrpg_info_id</c> активной игры: заявку на уже известную игру
    /// <c>add_uri</c> отбрасывает.
    /// </summary>
    public const int KnownAllrpgId = 12345;

    /// <summary>Название игры из другого региона.</summary>
    public const string SaintPetersburgGameName = "Петербургская тестовая игра";

    /// <summary>Самая залайканная игра во всём наборе (регион Петербурга).</summary>
    public const int MostLikedGameId = 905;

    /// <summary>Самая залайканная игра в московском регионе.</summary>
    public const int MostLikedInMoscowId = 913;

    /// <summary>Название игры с отзывами.</summary>
    public const string WithReviewsGameName = "Игра с отзывами";

    /// <summary>Автор видимого отзыва.</summary>
    public const string VisibleReviewAuthor = "Автор Видимого";

    /// <summary>Автор скрытого отзыва (<c>show_review_flag = 0</c>).</summary>
    public const string HiddenReviewAuthor = "Автор Скрытого";

    /// <summary>Номер темы на forum.rpg.ru у третьего отзыва.</summary>
    public const int ForumTopicId = 777;

    /// <summary>Название игры с пустым адресом почты.</summary>
    public const string WithoutEmailGameName = "Игра без почты";

    /// <summary>Название игры со статусом «???».</summary>
    public const string UnclearStatusGameName = "Игра с неясным статусом";

    /// <summary>Игра со статусом «OK», чья дата заведомо в прошлом (предыдущий год).</summary>
    public const string PastGameName = "Игра, которая заведомо прошла";

    /// <summary>Игра со статусом «OK», чья дата заведомо в будущем (следующий год).</summary>
    public const string FutureGameName = "Игра, которая заведомо впереди";

    /// <summary>Название, под которым заведены две разные игры — кандидаты на слияние.</summary>
    public const string DuplicateGameName = "Игра-двойник";

    /// <summary>Игра с пустой почтой и датой в следующем году.</summary>
    public const string FutureWithoutEmailGameName = "Грядущая игра без адреса";

    /// <summary>
    /// Год, в котором PHP-приложение показывает календарь по умолчанию.
    /// Правило взято из <c>appcode/base_funcs.php</c>: год по московскому времени,
    /// а с ноября — уже следующий. Даты игр засеваются в этот же год, иначе они
    /// не попадут в выдачу: главная фильтрует по <c>date_part('year', begin)</c>.
    /// </summary>
    public static int CurrentYear
    {
        get
        {
            var moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, moscow);
            return now.Month > 10 ? now.Year + 1 : now.Year;
        }
    }

    public static async Task SeedAsync(
        IContainer database,
        string user,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        var script = new FileInfo(Path.Combine(RepositoryLayout.Database.FullName, "testdata", "games.sql"));
        if (!script.Exists)
        {
            throw new FileNotFoundException("Нет db/testdata/games.sql.", script.FullName);
        }

        await database.CopyAsync(script, "/tmp", ct: cancellationToken);

        var result = await database.ExecAsync(
            [
                "psql",
                "--username", user,
                "--dbname", databaseName,
                "--set", "ON_ERROR_STOP=1",
                "--set", $"year={CurrentYear}",
                "--file", $"/tmp/{script.Name}",
            ],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Не засеялись тестовые данные (код {result.ExitCode}): {result.Stderr}");
        }
    }
}
