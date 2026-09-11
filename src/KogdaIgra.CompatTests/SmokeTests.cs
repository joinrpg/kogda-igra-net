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
    public async Task RegionsDictionaryIsVisibleOnHomePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/");

        // Справочник регионов засеян схемой из db/ — если он не доехал, страница отрендерится пустой.
        await Assert.That(body).Contains("Москва");
    }
}
