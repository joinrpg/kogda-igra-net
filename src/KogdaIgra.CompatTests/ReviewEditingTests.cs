namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт добавления, удаления и восстановления отзывов
/// (<c>appcode/logic/review.php</c>, действия формы в <c>edit/game/index.php</c>).
/// <para>
/// Удаление здесь мягкое: строка остаётся, гасится <c>show_review_flag</c>.
/// После каждой операции пересчитывается <c>ki_games.review_count</c>.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ReviewEditingTests(PhpSiteUnderTest site)
{
    [Before(HookType.Test)]
    [After(HookType.Test)]
    public async Task ResetDatabase()
    {
        if (site.StartupFailure is null)
        {
            await site.ResetDatabaseAsync();
        }
    }

    private const string EditGameUrl = "/edit/game/";

    /// <summary>Идентификатор видимого отзыва, засеянного фикстурами.</summary>
    private const int VisibleReviewId = 9501;

    private async Task<long> VisibleReviewCountAsync()
        => await Database.CountAsync(
            site.ConnectionString, "ki_review",
            $"game_id = {TestData.Games.WithReviews} AND show_review_flag = 1");

    private async Task<long> StoredCounterAsync()
        => await Database.ScalarAsync<long>(
            site.ConnectionString,
            $"SELECT review_count FROM ki_games WHERE id = {TestData.Games.WithReviews}");

    [Test]
    public async Task ModeratorCanAddReview()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string author = "Новый автор";

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "add_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["author"] = author,
                ["topic_id"] = "0",
                ["review_uri"] = "https://example.com/новый-отзыв",
            }));

        // Новый отзыв заводится сразу видимым (show_review_flag = 1).
        var added = await Database.CountAsync(
            site.ConnectionString, "ki_review",
            $"game_id = {TestData.Games.WithReviews} AND author_name = '{author}' AND show_review_flag = 1");

        await Assert.That(added).IsEqualTo(1);
    }

    [Test]
    public async Task AddingReviewRecalculatesCounter()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "add_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["author"] = "Ещё автор",
                ["topic_id"] = "0",
                ["review_uri"] = "https://example.com/ещё",
            }));

        // Счётчик не инкрементируется, а пересчитывается запросом COUNT(*) по
        // видимым отзывам — поэтому он сходится с реальностью даже после того,
        // как фикстуры засеяли заведомо расходящееся значение.
        await Assert.That(await StoredCounterAsync()).IsEqualTo(await VisibleReviewCountAsync());
    }

    [Test]
    public async Task ModeratorCanHideReview()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "delete_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["review_id"] = VisibleReviewId.ToString(),
            }));

        // Удаление мягкое: строка на месте, погашен только флаг.
        var stillThere = await Database.CountAsync(
            site.ConnectionString, "ki_review", $"review_id = {VisibleReviewId}");
        var visible = await Database.CountAsync(
            site.ConnectionString, "ki_review",
            $"review_id = {VisibleReviewId} AND show_review_flag = 1");

        await Assert.That(stillThere).IsEqualTo(1);
        await Assert.That(visible).IsEqualTo(0);
    }

    [Test]
    public async Task HiddenReviewDisappearsFromGamePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var deletion = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "delete_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["review_id"] = VisibleReviewId.ToString(),
            }));

        using var anonymous = site.CreateClient();
        var body = await anonymous.GetStringAsync($"/game/{TestData.Games.WithReviews}/");

        await Assert.That(body).DoesNotContain(TestData.VisibleReviewAuthor);
    }

    [Test]
    public async Task ModeratorCanRestoreHiddenReview()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);

        // Скрытый фикстурами отзыв возвращаем обратно.
        const int hiddenReviewId = 9502;
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "restore_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["review_id"] = hiddenReviewId.ToString(),
            }));

        var visible = await Database.CountAsync(
            site.ConnectionString, "ki_review",
            $"review_id = {hiddenReviewId} AND show_review_flag = 1");

        await Assert.That(visible).IsEqualTo(1);

        using var anonymous = site.CreateClient();
        var body = await anonymous.GetStringAsync($"/game/{TestData.Games.WithReviews}/");

        await Assert.That(body).Contains(TestData.HiddenReviewAuthor);
    }

    [Test]
    public async Task AnonymousVisitorCannotAddReview()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var before = await VisibleReviewCountAsync();

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "add_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["author"] = "Самозванец",
                ["topic_id"] = "0",
                ["review_uri"] = "https://example.com/нет",
            }));

        // request_edit_priv() стоит перед действием — в отличие от /edit/merge/.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
        await Assert.That(await VisibleReviewCountAsync()).IsEqualTo(before);
    }

    [Test]
    public async Task ReviewWithLiveJournalAuthorCreatesUser()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string ljName = "новыйжжюзер";

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        using var response = await client.PostAsync(EditGameUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "add_review",
                ["id"] = TestData.Games.WithReviews.ToString(),
                ["author"] = "",
                ["author_lj"] = ljName,
                ["topic_id"] = "0",
                ["review_uri"] = "https://example.com/жж",
            }));

        // Если указано имя в ЖЖ, отзыв привязывается к учётной записи, а не к строке:
        // пользователь заводится на лету (get_user_id_from_name), author_name остаётся NULL.
        var createdUser = await Database.CountAsync(
            site.ConnectionString, "users", $"username = '{ljName}'");

        var linkedReview = await Database.CountAsync(
            site.ConnectionString, "ki_review",
            $"game_id = {TestData.Games.WithReviews} AND author_id IS NOT NULL AND author_name IS NULL");

        await Assert.That(createdUser).IsEqualTo(1);
        await Assert.That(linkedReview).IsEqualTo(1);
    }
}
