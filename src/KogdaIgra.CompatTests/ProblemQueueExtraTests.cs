namespace KogdaIgra.CompatTests;

/// <summary>
/// Остальные очереди модерации: дубли имён, неясный статус, комментарии,
/// варианты «только будущие» и правка автора рецензии.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ProblemQueueExtraTests(PhpSiteUnderTest site)
{
    private async Task<string> GetQueueTextAsync(string queue)
    {
        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync($"/edit/problems/{queue}/");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return SiteResponse.Parse(body).Body!.TextContent;
    }

    [Test]
    public async Task CommentQueueListsGamesWithNonEmptyComment()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("comment");

        // Условие — comment <> ''. У активной игры комментарий есть, у остальных пуст.
        await Assert.That(text).Contains(TestData.ActiveGameName);
        await Assert.That(text).DoesNotContain(TestData.SaintPetersburgGameName);
    }

    [Test]
    public async Task UnclearStatusQueueListsGamesWithProblemStatus()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("noinfo");

        // Вот здесь как раз problem_status = 1 — статусы «???» и «Дата?».
        // Не путать с очередью passed, которая устроена совсем иначе.
        await Assert.That(text).Contains(TestData.UnclearStatusGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task UnclearStatusQueueTitleIsRendered()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var document = SiteResponse.Parse(await client.GetStringAsync("/edit/problems/noinfo/"));

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Проблемные игры :: Неясный статус");
    }

    [Test]
    public async Task MergeQueueListsDuplicateNames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var text = await GetQueueTextAsync("merge");

        // Очередь показывает не игры, а названия, встречающиеся больше одного раза:
        // SELECT name, COUNT(id) ... GROUP BY name HAVING COUNT(id) > 1.
        await Assert.That(text).Contains(TestData.DuplicateGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task MergeQueueTitleHasNoSiteNamePrefix()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var document = SiteResponse.Parse(await client.GetStringAsync("/edit/problems/merge/"));

        // Как и ленты, эта страница зовёт write_header() напрямую — префикса нет,
        // хотя у соседних очередей он есть. Зафиксировано как есть.
        await Assert.That(document.Title).IsEqualTo("Проблемные игры");
    }

    [Test]
    public async Task FutureQueueExcludesGamesWhoseDateHasGone()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var all = await GetQueueTextAsync("noemail");
        var future = await GetQueueTextAsync("noemail-future");

        // Обычная очередь показывает обе игры без почты, «будущая» — только ту,
        // чья дата ещё впереди (добавляется условие kgd.begin > NOW()).
        // Названия у игр нарочно непересекающиеся: если одно окажется подстрокой
        // другого, DoesNotContain начнёт срабатывать ложно.
        await Assert.That(all).Contains(TestData.WithoutEmailGameName);
        await Assert.That(all).Contains(TestData.FutureWithoutEmailGameName);

        await Assert.That(future).Contains(TestData.FutureWithoutEmailGameName);
        await Assert.That(future).DoesNotContain(TestData.WithoutEmailGameName);
    }

    [Test]
    [Arguments("noemail-future")]
    [Arguments("noplayers-count-future")]
    [Arguments("allrpg-info-future")]
    public async Task FutureQueueIsClosedToAnonymousVisitor(string queue)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/edit/problems/{queue}/");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task UpdateAuthorWithoutParameterRedirectsToHomePage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.GetAsync("/edit/problems/update-author/");

        // Без параметра author страница не имеет смысла — return_to_main().
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task UpdateAuthorShowsFormForGivenAuthor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await client.GetStringAsync("/edit/problems/update-author/?author=Некто");
        var document = SiteResponse.Parse(body);

        await Assert.That(document.Title).IsEqualTo("Исправление автора");
        await Assert.That(document.Body!.TextContent).Contains("Рецензии :: Исправление автора");

        // Имя автора подставляется в скрытое поле формы.
        await Assert.That(body).Contains("name=\"author\" value=\"Некто\"");
    }
}
