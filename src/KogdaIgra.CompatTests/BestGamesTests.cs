namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт блока «Самая популярная игра» и раздела популярных игр.
/// Популярность меряется полем <c>vk_likes</c>, которое обновляется отдельным
/// эндпоинтом из ВКонтакте (<c>appcode/best_calendar.php</c>,
/// <c>get_best</c> в <c>appcode/logic/gamelist.php</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class BestGamesTests(PhpSiteUnderTest site)
{
    private async Task<string> GetAsync(string url)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(url);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task HomePageAdvertisesMostLikedGame()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetAsync("/");

        // Блок показывает одну игру — с наибольшим числом лайков за текущий год.
        await Assert.That(body).Contains("Самая популярная игра");
        await Assert.That(body).Contains($"/game/{TestData.MostLikedGameId}/");
        await Assert.That(body).Contains($"/best/{TestData.CurrentYear}/");
    }

    [Test]
    public async Task RegionalHomePageAdvertisesMostLikedGameOfThatRegion()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Самая залайканная игра набора — петербургская, но в московском разделе
        // должна рекламироваться московская.
        var body = await GetAsync("/?region=3");

        await Assert.That(body).Contains($"/game/{TestData.MostLikedInMoscowId}/");
        await Assert.That(body).DoesNotContain($"/game/{TestData.MostLikedGameId}/");
    }

    [Test]
    public async Task BestSectionListsLikedGamesInDescendingOrder()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // /best/<год>/ разворачивается .htaccess в /?year=<год>&best=1.
        var document = SiteResponse.Parse(await GetAsync($"/best/{TestData.CurrentYear}/"));
        var text = document.Body!.TextContent;

        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
        await Assert.That(text).Contains(TestData.WithReviewsGameName);
        await Assert.That(text).Contains(TestData.WithoutEmailGameName);

        // Порядок — по убыванию лайков: 50, 42, 17.
        var spb = text.IndexOf(TestData.SaintPetersburgGameName, StringComparison.Ordinal);
        var reviews = text.IndexOf(TestData.WithReviewsGameName, StringComparison.Ordinal);
        var noEmail = text.IndexOf(TestData.WithoutEmailGameName, StringComparison.Ordinal);

        await Assert.That(spb).IsLessThan(reviews);
        await Assert.That(reviews).IsLessThan(noEmail);
    }

    [Test]
    public async Task BestSectionHasLikesColumn()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetAsync($"/best/{TestData.CurrentYear}/"));

        // У этого календаря свой набор колонок: добавлена «Лайки».
        var headers = document.QuerySelectorAll("#calendar th").Select(th => th.TextContent).ToArray();

        await Assert.That(headers).Contains("Лайки");
        await Assert.That(headers).Contains("Название");
    }

    [Test]
    public async Task BestSectionSkipsGamesWithoutLikes()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(
            await GetAsync($"/best/{TestData.CurrentYear}/")).Body!.TextContent;

        // Условие vk_likes > 0 отсекает всё остальное, включая активную игру.
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task BestSectionIsEmptyForYearWithoutLikedGames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetAsync("/?year=2020&best=1"));

        // Отбор идёт и по году тоже: в 2020 году игр нет вообще.
        var calendar = document.GetElementById("calendar");

        await Assert.That(calendar).IsNotNull();
        await Assert.That(calendar!.QuerySelectorAll("tr").Length).IsEqualTo(1);
    }
}
