namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы пользователя <c>/user/&lt;имя&gt;</c>.
/// Обратите внимание: в адресе стоит **имя**, а не числовой идентификатор —
/// <c>.htaccess</c> разворачивает путь в <c>?id=&lt;имя&gt;</c>, а PHP ищет
/// через <c>get_user_by_name()</c>. Страница открыта всем.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class UserProfileTests(PhpSiteUnderTest site)
{
    [Test]
    public async Task ProfileIsPublic()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/user/{TestData.ModeratorUserName}");
        var document = SiteResponse.Parse(body);

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(document.Title)
            .IsEqualTo($"КогдаИгра: Пользователь {TestData.ModeratorUserName}");
    }

    [Test]
    public async Task ProfileShowsGrantedPrivileges()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var text = SiteResponse.Parse(
            await client.GetStringAsync($"/user/{TestData.ModeratorUserName}")).Body!.TextContent;

        // Выводится человекочитаемое описание права из справочника privs,
        // а не его код: «Редактор календаря» вместо EDIT_GAMES.
        await Assert.That(text).Contains("Редактор календаря");
    }

    [Test]
    public async Task ProfileShowsEmailToAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/user/{TestData.ModeratorUserName}");

        // Зафиксировано как есть: адрес почты пользователя виден любому посетителю,
        // без входа и без каких-либо проверок прав (user/index.php печатает его
        // безусловно). Для игр такая настройка есть — hide_email, — а для
        // пользователей нет.
        await Assert.That(body).Contains("moderator@example.com");
    }

    [Test]
    public async Task UnknownUserRedirectsToHomePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/user/такого-пользователя-нет");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task EditFeedLinkIsShownOnlyToModerators()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var feedLink = $"/lenta/user/{TestData.ModeratorUserId}";

        using var anonymous = site.CreateClient();
        var anonymousBody = await anonymous.GetStringAsync($"/user/{TestData.ModeratorUserName}");

        using var moderator = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var moderatorBody = await moderator.GetStringAsync($"/user/{TestData.ModeratorUserName}");

        // Счётчик правок виден всем, а ссылка на ленту правок — только тем,
        // у кого есть право их смотреть.
        await Assert.That(moderatorBody).Contains(feedLink);
        await Assert.That(anonymousBody).DoesNotContain(feedLink);
    }

    [Test]
    public async Task ProfileCountsRecentEdits()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var text = SiteResponse.Parse(
            await client.GetStringAsync($"/user/{TestData.ModeratorUserName}")).Body!.TextContent;

        // У модератора засеяна одна свежая правка, поэтому блок со счётчиками
        // выводится (он появляется только при ненулевых счётчиках).
        await Assert.That(text).Contains("Правок в базе за 3 месяца");
    }
}
