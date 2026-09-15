namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт отзывов: блок на странице игры и раздел <c>/reviews/</c>.
/// Отзыв — это не текст на сайте, а ссылка на него вовне: либо тема на
/// forum.rpg.ru по <c>topic_id</c>, либо произвольный <c>review_uri</c>
/// (<c>appcode/review.php</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ReviewTests(PhpSiteUnderTest site)
{
    private async Task<string> GetGamePageAsync()
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/game/{TestData.Games.WithReviews}/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task VisibleReviewIsShownOnGamePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetGamePageAsync();

        await Assert.That(body).Contains(TestData.VisibleReviewAuthor);
        await Assert.That(body).Contains("https://example.com/отзыв");
    }

    [Test]
    public async Task HiddenReviewIsNotShownOnGamePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // get_reviews_for_game() добавляет show_review_flag = 1 — скрытый отзыв
        // виден только в форме редактирования.
        var body = await GetGamePageAsync();

        await Assert.That(body).DoesNotContain(TestData.HiddenReviewAuthor);
    }

    [Test]
    public async Task ForumReviewLinkIsBuiltFromTopicId()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await GetGamePageAsync();

        // При topic_id > 0 ссылка собирается из номера темы, а review_uri
        // игнорируется совсем. Схема — http, не https.
        await Assert.That(body)
            .Contains($"http://forum.rpg.ru/index.php?showtopic={TestData.ForumTopicId}");
    }

    [Test]
    public async Task ModeratorSeesHiddenReviewInEditForm()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // get_reviews_for_game_edit() идёт без фильтра по флагу: модератор должен
        // видеть и скрытые отзывы, иначе их нечем было бы вернуть обратно.
        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync($"/edit/game/?id={TestData.Games.WithReviews}");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(body).Contains(TestData.HiddenReviewAuthor);
        await Assert.That(body).Contains(TestData.VisibleReviewAuthor);
    }

    [Test]
    public async Task ReviewsSectionListsGamesWithReviews()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/reviews/");
        var text = SiteResponse.Parse(body).Body!.TextContent;

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(text).Contains(TestData.WithReviewsGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task ReviewsSectionTitleHasNoSiteNamePrefix()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var document = SiteResponse.Parse(await client.GetStringAsync("/reviews/"));

        // Ещё одна страница с write_header() напрямую: заголовок вкладки говорит
        // «Игры с рецензиями», хотя в меню раздел называется «Рецензии».
        await Assert.That(document.Title).IsEqualTo("Игры с рецензиями");
    }

    [Test]
    public async Task ReviewsSectionIsDrivenByCounterNotByRows()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Отбор идёт по денормализованному ki_games.review_count, а не по наличию
        // строк в ki_review. Само приложение счётчик поддерживает: при добавлении,
        // скрытии и восстановлении отзыва он пересчитывается запросом COUNT(*)
        // (см. ReviewEditingTests). Но целостность ничем не защищена — правка
        // в обход приложения разводит их, и заметить это негде. Здесь фикстуры
        // ставят счётчик 2 при трёх строках, из которых видимы две.
        var counter = await Database.ScalarAsync<long>(
            site.ConnectionString,
            $"SELECT review_count FROM ki_games WHERE id = {TestData.Games.WithReviews}");

        var rows = await Database.CountAsync(
            site.ConnectionString, "ki_review", $"game_id = {TestData.Games.WithReviews}");

        await Assert.That(counter).IsEqualTo(2);
        await Assert.That(rows).IsEqualTo(3);
    }

    [Test]
    public async Task LegacyReviewUrlRedirectsToGamePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/reviews/{TestData.Games.WithReviews}");

        // Здесь редирект постоянный (301), в отличие от 302 в остальных местах:
        // он задан правилом .htaccess с флагом R=permanent, а не PHP-кодом.
        await Assert.That((int)response.StatusCode).IsEqualTo(301);
        await Assert.That(response.Headers.Location!.OriginalString)
            .EndsWith($"/game/{TestData.Games.WithReviews}/");
    }
}
