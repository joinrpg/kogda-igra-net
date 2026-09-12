namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт поиска. Запрос ищется подстрокой (<c>ILIKE '%term%'</c>) сразу по нескольким
/// полям игры — <c>name</c>, <c>mg</c>, <c>email</c>, <c>uri</c>, <c>lj_comm</c>,
/// <c>fb_comm</c> и названию полигона (<c>appcode/logic/search.php</c>).
/// Ни года, ни региона поиск не учитывает.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class SearchTests(PhpSiteUnderTest site)
{
    /// <summary>
    /// Адрес поиска. Завершающий слэш ставить нельзя: правило <c>^find/(.+)/?$</c>
    /// жадное, и слэш попадёт внутрь самого запроса.
    /// </summary>
    private static string FindUrl(string term) => $"/find/{Uri.EscapeDataString(term)}";

    private async Task<string> SearchTextAsync(string term)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(FindUrl(term));
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return SiteResponse.Parse(body).Body!.TextContent;
    }

    [Test]
    public async Task TitleIsSearchWithoutYear()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync(FindUrl("дракон")));

        // На странице поиска calendar_mode выключен, поэтому ни региона, ни года в заголовке.
        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Поиск");
    }

    [Test]
    public async Task FindsGameByName()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        await Assert.That(await SearchTextAsync("дракон")).Contains(TestData.ActiveGameName);
    }

    [Test]
    public async Task FindsGameByMasterGroup()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // «МГ Невская» — поле mg игры 905, в названии этой строки нет.
        await Assert.That(await SearchTextAsync("Невская")).Contains(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task FindsGameByPolygonName()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        await Assert.That(await SearchTextAsync("Петербургом")).Contains(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task DoesNotSearchInComment()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Зафиксировано как есть: comment в список полей поиска не входит,
        // хотя интуитивно кажется, что должен.
        await Assert.That(await SearchTextAsync(TestData.CommentOnlyPhrase))
            .DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task IgnoresDeletedAndUnmoderatedGames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Слово «тестовая» есть в названиях всех трёх игр, но выдаются только живые.
        var text = await SearchTextAsync("тестовая");

        await Assert.That(text).DoesNotContain("Удалённая тестовая игра");
        await Assert.That(text).DoesNotContain("Игра на модерации");
    }

    [Test]
    public async Task ShowsOnlyPrimaryDateRow()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync(FindUrl("дракон")));

        // В отличие от главной, поиск добавляет kgd."order" = 0, поэтому перенесённая
        // дата отдельной строкой не показывается — строка ровно одна.
        var rows = document.QuerySelectorAll("#calendar tr")
            .Where(tr => tr.TextContent.Contains(TestData.ActiveGameName))
            .ToArray();

        await Assert.That(rows.Length).IsEqualTo(1);
    }

    [Test]
    public async Task IgnoresRegionAndYear()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Поиск не фильтрует ни по региону, ни по году: игры из обоих регионов рядом.
        var text = await SearchTextAsync("тестовая");

        await Assert.That(text).Contains(TestData.ActiveGameName);
        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task NothingFoundRendersEmptyCalendarWithoutMessage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync(FindUrl("заведомо-ничего-не-найдётся"));
        var document = SiteResponse.Parse(body);

        // Отдельного сообщения «ничего не найдено» нет — остаётся пустая таблица
        // с одной строкой заголовков.
        var calendar = document.GetElementById("calendar");
        await Assert.That(calendar).IsNotNull();
        await Assert.That(calendar!.QuerySelectorAll("tr").Length).IsEqualTo(1);
    }

    [Test]
    public async Task LegacySearchUrlRedirectsToFind()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/search.php?search=дракон");

        // Именно 302: header("Location: ...") без явного статуса.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString)
            .IsEqualTo($"/find/{Uri.EscapeDataString("дракон")}");
    }
}
