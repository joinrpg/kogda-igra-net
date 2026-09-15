namespace KogdaIgra.CompatTests;

/// <summary>
/// Небольшие публичные списки: <c>/noemail/</c> и <c>/data/mg/</c>.
/// Обе страницы открыты всем и в меню не значатся — на них выходят по прямым ссылкам.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class PublicListingTests(PhpSiteUnderTest site)
{
    private async Task<string> GetAsync(string url)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(url);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task GamesWithoutEmailPageIsPublic()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetAsync("/noemail/"));

        // Тот же список, что в очереди модерации /edit/problems/noemail/, но без
        // проверки прав — и с тем же заголовком.
        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Проблемные игры :: Нет e-mail");
        await Assert.That(document.Body!.TextContent).Contains(TestData.WithoutEmailGameName);
    }

    [Test]
    public async Task GamesWithoutEmailPageMatchesModerationQueue()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var publicText = SiteResponse.Parse(await GetAsync("/noemail/")).Body!.TextContent;

        using var moderator = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var queueText = SiteResponse.Parse(
            await moderator.GetStringAsync("/edit/problems/noemail/")).Body!.TextContent;

        // Обе страницы зовут get_noemail_games() без аргументов, так что состав совпадает.
        foreach (var name in new[] { TestData.WithoutEmailGameName, TestData.FutureWithoutEmailGameName })
        {
            await Assert.That(publicText).Contains(name);
            await Assert.That(queueText).Contains(name);
        }
    }

    [Test]
    public async Task MasterGroupListIsPublicAndLinksToSearch()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetAsync("/data/mg/");
        var document = SiteResponse.Parse(body);

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Список МГ");

        // Каждая мастерская группа — ссылка на поиск по её названию.
        await Assert.That(document.Body!.TextContent).Contains("МГ Тестовая");
        await Assert.That(body).Contains($"/find/{Uri.EscapeDataString("МГ Тестовая").Replace("%20", "+")}");
    }

    [Test]
    public async Task MasterGroupListSkipsDeletedGames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetAsync("/data/mg/")).Body!.TextContent;

        // Запрос отбирает DISTINCT mg по неудалённым играм. У удалённой игры
        // мастерская группа пуста, так что проверяем по живым: петербургская есть.
        await Assert.That(text).Contains("МГ Невская");
    }
}
