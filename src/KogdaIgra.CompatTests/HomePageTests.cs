using AngleSharp.Dom;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт главной страницы — календаря игр.
/// Отбор игр описан в <c>appcode/logic/gamelist.php</c> (get_main_calendar):
/// год по дате начала, регион, <c>deleted_flag = 0</c>.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class HomePageTests(PhpSiteUnderTest site)
{
    private async Task<IDocument> GetHomePageAsync()
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return SiteResponse.Parse(body);
    }

    [Test]
    public async Task TitleIsSiteNamePlusRegionAndYear()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = await GetHomePageAsync();

        // top_menu.php: get_page_title() = SITENAME_MAIN . ': ' . region_name . nbsp . year,
        // где region_name для главной — название региона с id = 1 («Все игры»).
        // Заодно это проверка кодировки: если справочники приехали побитыми,
        // вместо кириллицы здесь будет псевдографика.
        await Assert.That(document.Title).IsEqualTo($"КогдаИгра: Все игры\u00a0{TestData.CurrentYear}");
    }

    [Test]
    public async Task ActiveGameIsListed()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = await GetHomePageAsync();

        await Assert.That(document.Body!.TextContent).Contains(TestData.ActiveGameName);
    }

    [Test]
    public async Task DeletedAndUnmoderatedGamesAreHidden()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = (await GetHomePageAsync()).Body!.TextContent;

        // deleted_flag = 1 и deleted_flag = -1 отсеиваются условием kg.deleted_flag = 0.
        await Assert.That(text).DoesNotContain("Удалённая тестовая игра");
        await Assert.That(text).DoesNotContain("Игра на модерации");
    }

    [Test]
    public async Task GameRowLinksToGameProfile()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = await GetHomePageAsync();

        // uifuncs.php: get_game_profile_link() — канонический вид ссылки /game/<id>/.
        var link = document.QuerySelectorAll("a")
            .FirstOrDefault(a => a.TextContent.Contains(TestData.ActiveGameName));

        await Assert.That(link).IsNotNull();
        await Assert.That(link!.GetAttribute("href")).IsEqualTo($"/game/{TestData.Games.Active}/");
    }

    [Test]
    public async Task PostponedDateIsShownAsSeparateRow()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = await GetHomePageAsync();

        // У активной игры две видимые даты: order = 0 и перенесённая order = 1.
        // _get_games не фильтрует по order, поэтому строк в календаре тоже две,
        // и у перенесённой статус — «Перенесена!» (calendar.php).
        var rows = document.QuerySelectorAll("#calendar tr")
            .Where(tr => tr.TextContent.Contains(TestData.ActiveGameName))
            .ToArray();

        await Assert.That(rows.Length).IsEqualTo(2);
        await Assert.That(rows.Any(r => r.TextContent.Contains("Перенесена"))).IsTrue();
    }
}
