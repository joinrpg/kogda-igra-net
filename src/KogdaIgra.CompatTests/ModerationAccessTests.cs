namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт доступа к разделу модерации <c>/edit/</c>.
/// Права проверяются через <c>check_edit_priv()</c> — связку
/// <c>users</c> + <c>user_privs</c> + <c>privs</c> (<c>appcode/user_funcs.php</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ModerationAccessTests(PhpSiteUnderTest site)
{
    [Test]
    public async Task AnonymousVisitorSeesLoginButtonInsteadOfControlPanel()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var text = SiteResponse.Parse(await client.GetStringAsync("/")).Body!.TextContent;

        await Assert.That(text).Contains("Войти через JoinRPG");
        await Assert.That(text).DoesNotContain("Панель");
    }

    [Test]
    public async Task ModeratorSeesControlPanelAndOwnName()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var text = SiteResponse.Parse(await client.GetStringAsync("/")).Body!.TextContent;

        // Успешный вход виден по трём признакам сразу: имя пользователя, ссылка
        // на панель управления и исчезнувшая кнопка входа.
        await Assert.That(text).Contains(TestData.ModeratorUserName);
        await Assert.That(text).Contains("Панель");
        await Assert.That(text).DoesNotContain("Войти через JoinRPG");
    }

    [Test]
    public async Task ModerationDashboardIsRenderedForModerator()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync("/edit/");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(SiteResponse.LooksLikeHtmlPage(body)).IsTrue();
    }

    [Test]
    public async Task ModeratorSeesGameAwaitingModeration()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Игра с deleted_flag = -1 анонимному посетителю не видна нигде,
        // но модератор должен её найти — иначе модерировать нечего.
        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync($"/game/{TestData.Games.AwaitingModeration}/");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(body).Contains("Игра на модерации");
    }
}
