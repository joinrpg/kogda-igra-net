namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт выбора года в календаре.
/// Диапазон задан <c>validate_year</c> в <c>common/common.php</c>: строго больше 1980
/// и строго меньше 2030. Всё остальное — редирект на главную.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class YearNavigationTests(PhpSiteUnderTest site)
{
    [Test]
    public async Task PastYearRendersEmptyCalendar()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync("/?year=2020"));

        // Год валиден, игр в нём нет: заголовок с этим годом, календарь без игр.
        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Все игры\u00a02020");
        await Assert.That(document.Body!.TextContent).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    [Arguments(1980)]
    [Arguments(1000)]
    [Arguments(2030)]
    [Arguments(99999)]
    public async Task YearOutsideAllowedRangeRedirectsToHomePage(int year)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/?year={year}");

        // header("Location: /") без явного статуса — это 302.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    [Arguments(1981)]
    [Arguments(2029)]
    public async Task YearAtRangeBoundaryIsAccepted(int year)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/?year={year}");

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task RegionalYearUrlIsRewrittenToRegionAndYear()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // .htaccess: /msk/2026 -> /?region=3&year=2026. В порту этот маршрут придётся
        // реализовать самостоятельно — Apache там не будет.
        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync($"/msk/{TestData.CurrentYear}"));

        await Assert.That(document.Title)
            .IsEqualTo($"КогдаИгра: Москва и Центральный регион\u00a0{TestData.CurrentYear}");
        await Assert.That(document.Body!.TextContent).Contains(TestData.ActiveGameName);
    }
}
