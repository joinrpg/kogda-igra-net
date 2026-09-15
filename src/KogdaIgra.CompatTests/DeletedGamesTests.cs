namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт раздела удалённых игр <c>/edit/deleted-games/</c> — парная операция
/// к мягкому удалению из формы редактирования.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class DeletedGamesTests(PhpSiteUnderTest site)
{
    private const string Url = "/edit/deleted-games/";

    [Test]
    public async Task SectionIsClosedToAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync(Url);

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task ModeratorSeesOnlyDeletedGames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync(Url);
        var text = SiteResponse.Parse(body).Body!.TextContent;

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        // Условие — ровно deleted_flag = 1. Игра на модерации (-1) сюда не попадает:
        // она не удалена, а ещё не одобрена.
        await Assert.That(text).Contains("Удалённая тестовая игра");
        await Assert.That(text).DoesNotContain("Игра на модерации");
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task TitleHasNoSiteNamePrefix()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var document = SiteResponse.Parse(await client.GetStringAsync(Url));

        // Снова write_header() напрямую — префикса нет. И снова «Удаленные» без ё.
        await Assert.That(document.Title).IsEqualTo("Удаленные игры");
    }

    [Test]
    public async Task GameDeletedThroughFormAppearsHere()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        try
        {
            using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
            using var deletion = await client.PostAsync("/edit/game/", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["action"] = "delete",
                    ["id"] = TestData.Games.InSaintPetersburg.ToString(),
                }));

            var text = SiteResponse.Parse(await client.GetStringAsync(Url)).Body!.TextContent;

            // Замыкание цикла: удалили через форму — игра тут же в списке удалённых.
            await Assert.That(text).Contains(TestData.SaintPetersburgGameName);
        }
        finally
        {
            // Тест меняет БД, а остальные ждут исходного состояния.
            await site.ResetDatabaseAsync();
        }
    }
}
