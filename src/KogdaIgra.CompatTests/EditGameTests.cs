namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт формы редактирования игры <c>/edit/game</c>.
/// Форма шлёт POST с полем <c>action</c>; сохранение существующей игры требует права
/// <c>EDIT_GAMES</c>, а вот завести новую может кто угодно — так работает кнопка
/// «Добавьте самостоятельно» на главной (<c>edit/game/index.php</c>).
/// <para>
/// Тесты меняют БД, поэтому перед каждым база возвращается в исходное состояние.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class EditGameTests(PhpSiteUnderTest site)
{
    // База восстанавливается и до, и после теста. «До» — чтобы тест не зависел от
    // соседей; «после» — чтобы соседи не зависели от него. Без второго последний
    // write-тест оставлял бы за собой переименованную или удалённую игру, и падали
    // бы уже read-тесты, идущие следом (прогон последовательный).
    [Before(HookType.Test)]
    [After(HookType.Test)]
    public async Task ResetDatabase()
    {
        if (site.StartupFailure is null)
        {
            await site.ResetDatabaseAsync();
        }
    }

    /// <summary>
    /// Адрес обработчика формы — обязательно со слэшем на конце.
    /// <c>/edit/game</c> — это каталог, и Apache отвечает на него 301 на
    /// <c>/edit/game/</c>. При переходе по редиректу POST вырождается в GET, тело
    /// теряется, и правка молча не применяется. Сама форма на странице постит на
    /// текущий адрес, который к этому моменту уже со слэшем.
    /// </summary>
    private const string EditGameUrl = "/edit/game/";

    /// <summary>
    /// Поля формы сохранения. Обработчик читает их из <c>$_POST</c> поимённо,
    /// отсутствующие считает пустыми — поэтому отправляем ровно то, что проверяем.
    /// </summary>
    private static FormUrlEncodedContent SaveForm(int gameId, string name) =>
        new(new Dictionary<string, string>
        {
            ["action"] = "save",
            ["id"] = gameId.ToString(),
            ["name"] = name,
            ["uri"] = "",
            ["type"] = "1",
            ["polygon"] = "900",
            ["mg"] = "МГ Тестовая",
            ["email"] = "dragon@example.com",
            ["show_flags"] = "0",
            ["status"] = "0",
            ["comment"] = "",
            ["sub_region"] = "3",
            ["players_count"] = "120",
        });

    private async Task<string?> GameNameAsync(int gameId)
        => await Database.ScalarAsync<string>(
            site.ConnectionString, $"SELECT name FROM ki_games WHERE id = {gameId}");

    private async Task<short> DeletedFlagAsync(int gameId)
        => await Database.ScalarAsync<short>(
            site.ConnectionString, $"SELECT deleted_flag FROM ki_games WHERE id = {gameId}");

    [Test]
    public async Task ModeratorCanRenameGame()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string newName = "Переименованная игра";

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(
            EditGameUrl, SaveForm(TestData.Games.Active, newName));

        await Assert.That(await GameNameAsync(TestData.Games.Active)).IsEqualTo(newName);
    }

    [Test]
    public async Task SaveRedirectsBackToEditForm()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.PostAsync(
            EditGameUrl, SaveForm(TestData.Games.Active, "Ещё одно имя"));

        // get_game_edit_link() возвращает адрес без слэша — по нему Apache потом
        // отдаст ещё один 301. Фиксируем ровно то, что присылает PHP.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString)
            .IsEqualTo($"/edit/game?id={TestData.Games.Active}");
    }

    [Test]
    public async Task EditIsLoggedInUpdatesTable()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var before = await Database.CountAsync(
            site.ConnectionString, "ki_updates", $"game_id = {TestData.Games.Active}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(
            EditGameUrl, SaveForm(TestData.Games.Active, "Имя ради журнала"));

        var after = await Database.CountAsync(
            site.ConnectionString, "ki_updates", $"game_id = {TestData.Games.Active}");

        // Правка пишется в журнал обновлений — на нём держится и лента, и INNER JOIN
        // в _get_games, так что без записи игра пропала бы из выдачи.
        await Assert.That(after).IsGreaterThan(before);
    }

    [Test]
    public async Task AnonymousVisitorCannotEditExistingGame()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.PostAsync(
            EditGameUrl, SaveForm(TestData.Games.Active, "Взлом"));

        // request_edit_priv() вызывает return_to_main() — это 302 на /.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
        await Assert.That(await GameNameAsync(TestData.Games.Active)).IsEqualTo(TestData.ActiveGameName);
    }

    [Test]
    public async Task AnonymousVisitorCanSubmitNewGameForModeration()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string name = "Игра от анонимного посетителя";

        // id = 0 — новая игра. Права при этом не требуются: это кнопка
        // «Добавьте самостоятельно». Игра заводится сразу с deleted_flag = -1.
        using var client = site.CreateClient();
        using var response = await client.PostAsync(EditGameUrl, SaveForm(0, name));

        var awaiting = await Database.CountAsync(
            site.ConnectionString, "ki_games", $"name = '{name}' AND deleted_flag = -1");

        await Assert.That(awaiting).IsEqualTo(1);
    }

    [Test]
    public async Task ModeratorCanDeleteGame()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "delete",
                ["id"] = TestData.Games.Active.ToString(),
            }));

        // Удаление мягкое: строка остаётся, меняется только флаг.
        await Assert.That(await DeletedFlagAsync(TestData.Games.Active)).IsEqualTo((short)1);
    }

    [Test]
    public async Task AnonymousVisitorCannotDeleteGame()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "delete",
                ["id"] = TestData.Games.Active.ToString(),
            }));

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(await DeletedFlagAsync(TestData.Games.Active)).IsEqualTo((short)0);
    }
}
