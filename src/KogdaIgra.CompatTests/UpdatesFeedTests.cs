namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт лент изменений <c>/lenta/*</c> — рабочий инструмент модераторов.
/// Все ленты закрыты правом <c>EDIT_GAMES</c> и показывают записи <c>ki_updates</c>
/// за последние три месяца (<c>appcode/logic/updates.php</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class UpdatesFeedTests(PhpSiteUnderTest site)
{
    private async Task<string> GetAsModeratorAsync(string url)
    {
        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync(url);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    [Arguments("/lenta/internal/")]
    [Arguments("/lenta/patrol/")]
    [Arguments("/lenta/photo/")]
    [Arguments("/lenta/photo-patrol/")]
    [Arguments("/lenta/polygon/")]
    public async Task FeedIsClosedToAnonymousVisitor(string url)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync(url);

        // check_edit_priv() не прошёл — return_to_main(), то есть 302 на /.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    [Arguments("/lenta/internal/", "Лента изменений :: Внутреняя")]
    [Arguments("/lenta/patrol/", "Лента изменений :: Патруль")]
    [Arguments("/lenta/photo/", "Лента изменений :: Фотоотчеты")]
    [Arguments("/lenta/polygon/", "Лента изменений :: Полигоны")]
    public async Task FeedTitleIsRenderedForModerator(string url, string pageName)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetAsModeratorAsync(url));

        // Заголовок БЕЗ префикса «КогдаИгра: », в отличие от всех остальных страниц:
        // ленты зовут write_header() напрямую, минуя TopMenu::get_page_title().
        // Опечатка «Внутреняя» — тоже из исходника, воспроизводится как есть.
        await Assert.That(document.Title).IsEqualTo(pageName);
    }

    [Test]
    public async Task InternalFeedShowsRecentUpdates()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetAsModeratorAsync("/lenta/internal/")).Body!.TextContent;

        // Общая лента показывает все свежие правки — и модератора, и анонимные.
        await Assert.That(text).Contains(TestData.ActiveGameName);
        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task PatrolFeedHidesOwnUpdates()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(await GetAsModeratorAsync("/lenta/patrol/")).Body!.TextContent;

        // «Патруль» — это ku.user_id <> мой: свои правки проверять незачем.
        // Правка модератора сделана над активной игрой, анонимная — над петербургской.
        await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task PersonalFeedShowsOnlyThatUsersUpdates()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // /lenta/user/<id> разворачивается .htaccess в index.php?id=<id>.
        var body = await GetAsModeratorAsync($"/lenta/user/{TestData.ModeratorUserId}");
        var document = SiteResponse.Parse(body);

        await Assert.That(document.Title)
            .IsEqualTo($"Лента изменений {TestData.ModeratorUserName}");

        var text = document.Body!.TextContent;
        await Assert.That(text).Contains(TestData.ActiveGameName);
        await Assert.That(text).DoesNotContain(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task PersonalFeedWithoutUserRedirectsToHomePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.GetAsync("/lenta/user/index.php");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task IpFeedShowsUpdatesFromThatAddress()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetAsModeratorAsync($"/lenta/ip/{TestData.AnonymousUpdateIp}");
        var document = SiteResponse.Parse(body);

        // У ленты по IP заголовок задаётся только через TopMenu, write_header там
        // вызывается без аргумента — поэтому префикс на месте, в отличие от других лент.
        await Assert.That(document.Title).IsEqualTo($"КогдаИгра: Лента {TestData.AnonymousUpdateIp}");
        await Assert.That(document.Body!.TextContent).Contains(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task IpFeedRejectsMalformedAddress()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Адрес проверяется filter_var(FILTER_VALIDATE_IP); «999.999.999.999»
        // проходит маршрут .htaccess (цифры и точки), но валидацию — нет.
        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.GetAsync("/lenta/ip/999.999.999.999");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task AnonymousUpdateIsAttributedToAnonymous()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = SiteResponse.Parse(
            await GetAsModeratorAsync($"/lenta/ip/{TestData.AnonymousUpdateIp}")).Body!.TextContent;

        // user_id = 0 не совпадает ни с одним пользователем, username остаётся пустым,
        // и строка подписывается «Аноним» (show_updates.php).
        await Assert.That(text).Contains("Аноним");
    }
}
