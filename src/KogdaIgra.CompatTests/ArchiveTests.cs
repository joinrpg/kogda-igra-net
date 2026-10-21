namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы архива <c>/archive/</c>: заголовок и ссылки на все годы из
/// <c>ki_years_cache</c> вида <c>/&lt;год&gt;/</c>, через « :: », по возрастанию.
/// <para>
/// До joinrpg/kogda-igra#69 страница обрывалась посреди верхнего меню: перевод строки
/// перед <c>&lt;?php</c> в <c>archive/index.php</c> не давал стартовать сессии, и
/// <c>get_user_id()</c> падала с фатальной ошибкой на <c>$_SESSION</c> = null.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ArchiveTests(PhpSiteUnderTest site)
{
    private async Task<string> GetArchiveAsync()
    {
        using var client = site.CreateClient();
        using var response = await client.GetAsync("/archive/");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        return body;
    }

    [Test]
    public async Task PageIsRenderedToTheEnd()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetArchiveAsync();

        // По статусу судить нельзя (200 отдавался и при фатале), поэтому проверяем,
        // что вывод дошёл до конца: меню с кнопкой «Добавить» и закрывающие теги.
        await Assert.That(SiteResponse.LooksLikeHtmlPage(body)).IsTrue();
        await Assert.That(body).Contains("</body>");
        await Assert.That(body).Contains("Добавить");
    }

    [Test]
    public async Task HasTitleAndHeading()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetArchiveAsync());

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Архив старых лет");
        await Assert.That(document.QuerySelector("h1")?.TextContent).IsEqualTo("Архив старых лет");
    }

    [Test]
    public async Task ListsYearsFromCache()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetArchiveAsync());

        // Ссылки архива идут после h1 и, в отличие от полосы годов в верхнем меню,
        // заканчиваются слэшем. Фикстуры засевают в ki_years_cache только текущий год.
        var links = document.QuerySelectorAll("h1 ~ a").Select(a => a.GetAttribute("href") ?? "").ToList();
        await Assert.That(links).IsEquivalentTo([$"/{TestData.CurrentYear}/"]);
    }
}
