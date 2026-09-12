namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт региональных разделов календаря.
/// Короткие адреса вида <c>/msk/</c> и <c>/msk/2026</c> разворачиваются в
/// <c>/?region=3</c> правилами <c>.htaccess</c>, поэтому проверять надо именно их:
/// в порту эти маршруты придётся воспроизводить самостоятельно, Apache там не будет.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class RegionFilterTests(PhpSiteUnderTest site)
{
    private async Task<string> GetTextAsync(string url)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(url);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return SiteResponse.Parse(body).Body!.TextContent;
    }

    [Test]
    [Arguments("/msk/")]
    [Arguments("/?region=3")]
    public async Task MoscowSectionShowsOnlyMoscowGames(string url)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetTextAsync(url);

        await Assert.That(text).Contains(TestData.ActiveGameName);
        await Assert.That(text).DoesNotContain(TestData.SaintPetersburgGameName);
    }

    [Test]
    [Arguments("/spb/")]
    [Arguments("/?region=2")]
    public async Task SaintPetersburgSectionShowsOnlyItsOwnGames(string url)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetTextAsync(url);

        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task HomePageShowsGamesFromAllRegions()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Без региона фильтр — kr.region_experimental = 0, то есть все неэкспериментальные
        // регионы разом (gamelist.php: get_region_condition).
        var text = await GetTextAsync("/");

        await Assert.That(text).Contains(TestData.ActiveGameName);
        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task RegionColumnShowsSubRegionDisplayName()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync("/"));

        // В колонке «Регион» выводится sub_region_disp_name («Мск»), а не название
        // региона и не полное имя субрегиона (calendar.php).
        var row = document.QuerySelectorAll("#calendar tr")
            .First(tr => tr.TextContent.Contains(TestData.ActiveGameName));

        await Assert.That(row.TextContent).Contains("Мск");
    }

    [Test]
    public async Task RegionSectionTitleUsesRegionName()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync("/?region=3"));

        await Assert.That(document.Title)
            .IsEqualTo($"КогдаИгра: Москва и Центральный регион\u00a0{TestData.CurrentYear}");
    }
}
