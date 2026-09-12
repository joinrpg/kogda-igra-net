namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы игры <c>/game/&lt;id&gt;/</c>.
/// Слаг не используется: <c>ki_games.uri</c> — это внешняя ссылка на сайт игры,
/// а не адрес страницы (uifuncs.php: get_game_profile_link).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class GamePageTests(PhpSiteUnderTest site)
{
    private async Task<string> GetGamePageAsync(int gameId)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/game/{gameId}/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task TitleIsSiteNamePlusGameName()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var document = SiteResponse.Parse(await GetGamePageAsync(TestData.Games.Active));

        await Assert.That(document.Title).IsEqualTo($"КогдаИгра: {TestData.ActiveGameName}");
    }

    [Test]
    public async Task DeletedGameShowsRemovalNotice()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // deleted_flag = 1: страница отвечает, но вместо игры — сообщение об удалении.
        var text = SiteResponse.Parse(await GetGamePageAsync(TestData.Games.Deleted)).Body!.TextContent;

        await Assert.That(text).Contains("удалена");
    }

    [Test]
    public async Task GameAwaitingModerationShowsModerationNotice()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // deleted_flag = -1: игра добавлена пользователем и ещё не одобрена.
        var text = SiteResponse.Parse(
            await GetGamePageAsync(TestData.Games.AwaitingModeration)).Body!.TextContent;

        await Assert.That(text).Contains("модерации");
    }

    [Test]
    public async Task VisibleEmailIsRendered()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetGamePageAsync(TestData.Games.Active);

        await Assert.That(body).Contains("dragon@example.com");
    }

    [Test]
    public async Task HiddenEmailIsNotLeakedToAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // hide_email = 1 и пользователь не залогинен — адрес не должен появиться
        // в разметке ни ссылкой, ни текстом.
        var body = await GetGamePageAsync(TestData.Games.WithHiddenEmail);

        await Assert.That(body).DoesNotContain("secret@example.com");
    }
}
