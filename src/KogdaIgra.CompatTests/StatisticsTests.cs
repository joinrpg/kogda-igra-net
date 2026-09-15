namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы статистики <c>/stat/</c>. Открыта всем.
/// Таблица строится по годам из <c>ki_years_cache</c>, а числа считаются
/// подзапросами по <c>ki_games</c> (<c>appcode/logic/stat.php</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class StatisticsTests(PhpSiteUnderTest site)
{
    private async Task<string> GetStatisticsAsync()
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/stat/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task PageIsPublicAndTitled()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetStatisticsAsync());

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Статистика");
    }

    [Test]
    public async Task TableHasYearAndEventColumns()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetStatisticsAsync()).Body!.TextContent;

        await Assert.That(text).Contains("Год");
        await Assert.That(text).Contains("Мероприятия");
        await Assert.That(text).Contains("% Отмен и переносов");
    }

    [Test]
    public async Task CurrentYearIsListed()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetStatisticsAsync()).Body!.TextContent;

        // Год попадает в таблицу через ki_years_cache, который засеян фикстурами.
        await Assert.That(text).Contains(TestData.CurrentYear.ToString());
    }

    [Test]
    public async Task DisclaimerIsShown()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetStatisticsAsync()).Body!.TextContent;

        await Assert.That(text).Contains("предварительной, нечеткой и неофициальной");
    }
}
