namespace KogdaIgra.CompatTests;

/// <summary>
/// Проверка самого харнесса, а не сайта: поднимается ли PHP-оригинал как чёрный ящик
/// и отдаёт ли осмысленную главную страницу. Настоящие compat-тесты по разделам
/// сайта строятся поверх этого.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class SmokeTests(PhpSiteUnderTest site)
{
    [Test]
    public async Task HomePageIsRenderedFromDatabase()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        // Статус проверяем, но полагаться на него нельзя — см. SiteResponse.
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        var document = SiteResponse.Parse(body);
        await Assert.That(document.Title).IsNotNull();
    }

    [Test]
    public async Task HomePageRendersCalendarTable()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/");
        var document = SiteResponse.Parse(body);

        // Календарь — главный элемент страницы. На пустой базе таблица состоит из одной
        // строки заголовков: сами игры приезжают из БД, справочники на их состав не влияют.
        var calendar = document.GetElementById("calendar");
        await Assert.That(calendar).IsNotNull();

        var headers = calendar!.QuerySelectorAll("th").Select(th => th.TextContent).ToArray();
        await Assert.That(headers).Contains("Название");
        await Assert.That(headers).Contains("Регион");
        await Assert.That(headers).Contains("Тип игры");
    }
}
