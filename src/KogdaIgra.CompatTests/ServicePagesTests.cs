namespace KogdaIgra.CompatTests;

/// <summary>
/// Мелкие служебные страницы: ошибка 404, выход, статические тексты и
/// страница-заглушка с сообщениями.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ServicePagesTests(PhpSiteUnderTest site)
{
    [Test]
    public async Task UnknownUrlServesNotFoundPageWithRealStatus()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync("/такой-страницы-точно-нет/");
        var body = await response.Content.ReadAsStringAsync();

        // ErrorDocument 404 /404/ в .htaccess, а сама страница выставляет статус
        // явно через header(). Тут приложение честно отдаёт 404 — в отличие от
        // фатальных ошибок, которые уходят с 200.
        await Assert.That((int)response.StatusCode).IsEqualTo(404);
        await Assert.That(SiteResponse.Parse(body).Body!.TextContent).Contains("Страница не найдена");
    }

    [Test]
    public async Task NotFoundPageOpenedDirectlyAlsoReturns404()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync("/404/");

        await Assert.That((int)response.StatusCode).IsEqualTo(404);

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(SiteResponse.Parse(body).Title).IsEqualTo("КогдаИгра: 404");
    }

    [Test]
    public async Task LogoutClearsSessionAndRedirects()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);

        // До выхода панель управления видна.
        using (var before = await client.GetAsync("/"))
        {
            var body = await before.Content.ReadAsStringAsync();
            await Assert.That(body).Contains(TestData.ModeratorUserName);
        }

        using var logout = await client.GetAsync("/logout/");
        await Assert.That((int)logout.StatusCode).IsEqualTo(302);
        await Assert.That(logout.Headers.Location!.OriginalString).IsEqualTo("/");

        // После — снова аноним: cookie сессии та же, но user_id из неё вычищен.
        using var after = await client.GetAsync("/");
        var afterBody = await after.Content.ReadAsStringAsync();

        await Assert.That(afterBody).DoesNotContain(TestData.ModeratorUserName);
        await Assert.That(afterBody).Contains("Войти через JoinRPG");
    }

    [Test]
    public async Task ThanksPageIsShownAfterGameIsSubmitted()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Сюда ведёт редирект из api/game/add.php, когда запрос не автоматический.
        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/game/thanks");
        var document = SiteResponse.Parse(body);

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Добавление игры");
        await Assert.That(document.Body!.TextContent).Contains("Спасибо за добавление игры");
    }

    [Test]
    public async Task AlreadyProcessedPageHasItsOwnMessage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var text = SiteResponse.Parse(
            await client.GetStringAsync("/edit/already")).Body!.TextContent;

        // Обе страницы обслуживает один service/message.php, а текст выбирается
        // по REQUEST_URI — то есть по адресу, а не по параметру.
        await Assert.That(text).Contains("Ссылка уже обработана");
    }

    [Test]
    public async Task MessagePageWithUnknownUriRendersEmptyMessage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Прямое обращение к service/message.php: такого ключа в таблице сообщений
        // нет, $message остаётся null. Страница всё равно отдаётся, с пустым
        // заголовком и без текста — зафиксировано как есть.
        using var client = site.CreateClient();
        using var response = await client.GetAsync("/service/message.php");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(SiteResponse.LooksLikeHtmlPage(body)).IsTrue();
        // AngleSharp обрезает пробелы в заголовке, поэтому сравнение без хвостового.
        await Assert.That(SiteResponse.Parse(body).Title).IsEqualTo("КогдаИгра:");
    }

    [Test]
    public async Task NoviceGuideIsPublic()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/novice/spb/");
        var document = SiteResponse.Parse(body);

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Собираешься впервые на игру?");
        await Assert.That(document.Body!.TextContent).Contains("впервые");
    }

    [Test]
    public async Task LookupByEmailRequiresPrivilegeAndUsername()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Вопреки первому впечатлению, edit/users/by-email — не заготовка:
        // это рабочий обработчик, который ищет пользователя по адресу почты
        // и перенаправляет на его страницу. Без права USERS_CONTROL или без
        // параметра username он молча уводит на главную.
        using var anonymous = site.CreateClient(followRedirects: false);
        using var denied = await anonymous.PostAsync(
            "/edit/users/by-email/", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["username"] = "moderator@example.com" }));

        await Assert.That((int)denied.StatusCode).IsEqualTo(302);
        await Assert.That(denied.Headers.Location!.OriginalString).IsEqualTo("/");

        using var admin = await site.CreateLoggedInClientAsync(
            TestData.AdminUserId, followRedirects: false);
        using var withoutUsername = await admin.PostAsync(
            "/edit/users/by-email/", new FormUrlEncodedContent([]));

        await Assert.That((int)withoutUsername.StatusCode).IsEqualTo(302);
        await Assert.That(withoutUsername.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task LookupByEmailRedirectsToExistingUser()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var admin = await site.CreateLoggedInClientAsync(
            TestData.AdminUserId, followRedirects: false);
        using var response = await admin.PostAsync(
            "/edit/users/by-email/", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["username"] = "moderator@example.com" }));

        // Найденный пользователь — модератор из фикстур.
        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString)
            .IsEqualTo($"/edit/users/{TestData.ModeratorUserId}");
    }
}
