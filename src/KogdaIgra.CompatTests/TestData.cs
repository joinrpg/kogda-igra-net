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
    }

    /// <summary>Название активной игры — ожидаемое значение для ассертов.</summary>
    public const string ActiveGameName = "Тестовая игра про дракона";

    /// <summary>
    /// Слово, которое встречается только в поле <c>comment</c> активной игры.
    /// Поиск по нему ничего не находит — <c>comment</c> в поиск не входит.
    /// </summary>
    public const string CommentOnlyPhrase = "примечаниепроболото";

    /// <summary>Идентификатор засеянного пользователя с правом EDIT_GAMES.</summary>
    public const int ModeratorUserId = 9900;

    /// <summary>Имя засеянного модератора — оно выводится в меню после входа.</summary>
    public const string ModeratorUserName = "testmoderator";

    /// <summary>
    /// <c>allrpg_info_id</c> активной игры: заявку на уже известную игру
    /// <c>add_uri</c> отбрасывает.
    /// </summary>
    public const int KnownAllrpgId = 12345;

    /// <summary>Название игры из другого региона.</summary>
    public const string SaintPetersburgGameName = "Петербургская тестовая игра";

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
