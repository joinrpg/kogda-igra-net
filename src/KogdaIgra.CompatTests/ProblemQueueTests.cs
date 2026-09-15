namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт очередей модерации <c>/edit/problems/*</c>. Каждая очередь — это календарь,
/// отфильтрованный по признаку «с игрой что-то не так»
/// (<c>appcode/logic/problems.php</c>). Все закрыты правом <c>EDIT_GAMES</c>.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ProblemQueueTests(PhpSiteUnderTest site)
{
    private async Task<string> GetQueueTextAsync(string queue)
    {
        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync($"/edit/problems/{queue}/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return SiteResponse.Parse(body).Body!.TextContent;
    }

    [Test]
    [Arguments("noemail")]
    [Arguments("noplayers-count")]
    [Arguments("allrpg-info")]
    [Arguments("nopolygon")]
    [Arguments("passed")]
    [Arguments("comment")]
    public async Task QueueIsClosedToAnonymousVisitor(string queue)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/edit/problems/{queue}/");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    [Arguments("noemail", "Проблемные игры :: Нет e-mail")]
    [Arguments("noplayers-count", "Проблемные игры :: Нет кол-ва игроков")]
    [Arguments("allrpg-info", "Проблемные игры :: Нет ссылки на allrpg.info")]
    [Arguments("nopolygon", "Проблемные игры :: Нет полигона")]
    [Arguments("passed", "Проблемные игры :: Прошедшие?")]
    [Arguments("comment", "Проблемные игры :: Есть комментарий")]
    public async Task QueueTitleIsRendered(string queue, string pageName)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var document = SiteResponse.Parse(await client.GetStringAsync($"/edit/problems/{queue}/"));

        // Здесь префикс «КогдаИгра: » на месте: страницы задают только pagename
        // и отдают заголовок через TopMenu — в отличие от лент /lenta/*.
        await Assert.That(document.Title).IsEqualTo($"КогдаИгра: {pageName}");
    }

    [Test]
    public async Task NoEmailQueueListsGamesWithEmptyEmailOnly()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("noemail");

        // Условие — LENGTH(email) = 0. Скрытая почта (hide_email = 1) сюда
        // не относится: адрес есть, просто не показывается.
        await Assert.That(text).Contains(TestData.WithoutEmailGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task NoPlayersCountQueueListsGamesWithNullCount()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("noplayers-count");

        // players_count IS NULL — именно NULL, а не ноль.
        await Assert.That(text).Contains("Игра со скрытой почтой");
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task AllrpgQueueListsGamesWithoutExternalId()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("allrpg-info");

        // У активной игры allrpg_info_id проставлен, у петербургской — нет.
        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task NoPolygonQueueListsGamesOnPlaceholderPolygon()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("nopolygon");

        // Признак — meta_polygon = 1 у самого полигона, а не пустое поле у игры.
        await Assert.That(text).Contains(TestData.UnclearStatusGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task PassedQueueListsOkGamesWhoseDateHasGone()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("passed");

        // Условие — ks.future_only_status = 1 (это статус «OK») И (begin + time) < NOW().
        // Смысл очереди: у игры стоит «OK», хотя она уже прошла, статус пора обновить.
        // Игра со статусом «???» сюда НЕ попадает, хотя по названию очереди
        // «Проблемные игры» этого можно было бы ожидать.
        await Assert.That(text).Contains(TestData.PastGameName);
        await Assert.That(text).DoesNotContain(TestData.FutureGameName);
        await Assert.That(text).DoesNotContain(TestData.UnclearStatusGameName);
    }

    [Test]
    public async Task DeletedGamesNeverAppearInQueues()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        foreach (var queue in new[] { "noemail", "noplayers-count", "allrpg-info", "nopolygon", "passed" })
        {
            var text = await GetQueueTextAsync(queue);

            await Assert.That(text).DoesNotContain("Удалённая тестовая игра");
            await Assert.That(text).DoesNotContain("Игра на модерации");
        }
    }

    [Test]
    public async Task DashboardLinksToProblemQueues()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync("/edit/");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        // Панель управления — точка входа модератора: с неё должны вести ссылки
        // на очереди, иначе до них не добраться.
        foreach (var queue in new[] { "noemail", "noplayers-count", "nopolygon" })
        {
            await Assert.That(body).Contains($"/edit/problems/{queue}");
        }
    }
}
