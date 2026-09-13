namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт добавления игры через <c>/api/game/add.php</c> — единственный публичный
/// write-эндпоинт. Он не создаёт игру сразу, а кладёт ссылку в очередь модерации
/// <c>ki_add_uri</c> (<c>appcode/logic/edit.php</c>, <c>add_uri</c>).
/// <para>
/// Тесты меняют состояние БД, поэтому перед каждым из них база возвращается
/// в исходное засеянное состояние (последовательность прогона обеспечена
/// <c>[assembly: NotInParallel]</c>, см. AssemblyAttributes.cs). Почта наружу
/// не уходит: в окружении контейнера намеренно нет <c>MAILGUN_KEY</c>.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class AddGameApiTests(PhpSiteUnderTest site)
{
    [Before(HookType.Test)]
    public async Task ResetDatabase()
    {
        if (site.StartupFailure is null)
        {
            await site.ResetDatabaseAsync();
        }
    }

    private async Task<long> QueueLengthAsync(string where)
        => await Database.CountAsync(site.ConnectionString, "ki_add_uri", where);

    [Test]
    public async Task AutomatedRequestReportsOkAndQueuesUri()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string uri = "https://example.com/новая-игра";

        using var client = site.CreateClient();
        var body = await client.GetStringAsync(
            $"/api/game/add.php?automated=1&uri={Uri.EscapeDataString(uri)}");

        // Ответ — не JSON, а маркер в квадратных скобках. Зафиксировано как есть:
        // на него ориентируются внешние скрипты.
        await Assert.That(body.Trim()).IsEqualTo("[add_game_status:ok]");

        await Assert.That(await QueueLengthAsync($"uri = '{uri}'")).IsEqualTo(1);
    }

    [Test]
    public async Task NonAutomatedRequestRedirectsToThanksPage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync(
            $"/api/game/add.php?uri={Uri.EscapeDataString("https://example.com/без-automated")}");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/game/thanks");
    }

    [Test]
    public async Task RequestWithoutUriRedirectsToEditForm()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/api/game/add.php");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/edit/game");

        // В очередь при этом ничего не попадает.
        await Assert.That(await QueueLengthAsync("1 = 1")).IsEqualTo(0);
    }

    [Test]
    public async Task SameUriIsNotQueuedTwice()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string uri = "https://example.com/дубликат";
        var url = $"/api/game/add.php?automated=1&uri={Uri.EscapeDataString(uri)}";

        using var client = site.CreateClient();
        var first = await client.GetStringAsync(url);
        var second = await client.GetStringAsync(url);

        // Ответ одинаковый в обоих случаях — снаружи повтор неотличим от первой заявки.
        await Assert.That(first.Trim()).IsEqualTo("[add_game_status:ok]");
        await Assert.That(second.Trim()).IsEqualTo("[add_game_status:ok]");

        // Но в очереди строка по-прежнему одна: _add_uri проверяет наличие по LIKE.
        await Assert.That(await QueueLengthAsync($"uri = '{uri}'")).IsEqualTo(1);
    }

    [Test]
    public async Task AllrpgLinkIsQueuedAsExternalIdInsteadOfUri()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Ссылку на allrpg.info add_uri разбирает особо: вытаскивает числовой id
        // события и кладёт его в allrpg_info_id, а не строку в uri.
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(
            "/api/game/add.php?automated=1&uri=http://inf.allrpg.info/events/54321/");

        await Assert.That(body.Trim()).IsEqualTo("[add_game_status:ok]");

        await Assert.That(await QueueLengthAsync("allrpg_info_id = 54321")).IsEqualTo(1);
        await Assert.That(await QueueLengthAsync("uri IS NOT NULL")).IsEqualTo(0);
    }

    [Test]
    public async Task AllrpgLinkForKnownGameIsNotQueued()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Если игра с таким allrpg_info_id уже заведена, заявка отбрасывается:
        // add_uri возвращает 0, письмо не отправляется, в очередь ничего не кладётся.
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(
            $"/api/game/add.php?automated=1&uri=http://inf.allrpg.info/events/{TestData.KnownAllrpgId}/");

        await Assert.That(body.Trim()).IsEqualTo("[add_game_status:ok]");
        await Assert.That(await QueueLengthAsync("1 = 1")).IsEqualTo(0);
    }
}
