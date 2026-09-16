namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы «О нас» <c>/about/</c>. Логики почти нет, но страница
/// собирает ссылки на пользователей и почту редакции из настроек, и это стоит
/// закрепить: при портировании такие мелочи теряются незаметно.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class AboutPageTests(PhpSiteUnderTest site)
{
    private async Task<string> GetAboutAsync()
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/about/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task PageIsPublicAndTitled()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetAboutAsync());

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: О нас");
    }

    [Test]
    public async Task ContactsSectionIsRendered()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetAboutAsync()).Body!.TextContent;

        await Assert.That(text).Contains("Контакты");
        await Assert.That(text).Contains("Команда сайта");
    }

    [Test]
    public async Task EditorialEmailIsBuiltFromSiteHost()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetAboutAsync();

        // SITENAME_EDITORS_EMAIL собирается как "rpg@" плюс SITENAME_HOST, а тот
        // приходит из окружения. В тестах хост — localhost с выданным портом,
        // поэтому проверяется схема сборки, а не конкретный адрес.
        await Assert.That(body).Contains($"mailto:rpg@{site.BaseUrl.Authority}");
    }

    [Test]
    public async Task TeamMembersLinkToTheirProfiles()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetAboutAsync();

        // Имена в списке команды прописаны в разметке и ведут на /user/<имя>.
        await Assert.That(body).Contains("/user/leotsarev");
    }
}
