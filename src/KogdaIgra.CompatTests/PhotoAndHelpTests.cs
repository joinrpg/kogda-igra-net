namespace KogdaIgra.CompatTests;

/// <summary>
/// Разделы фото, «Требуется информация», выгрузка в Excel и очередь заявок
/// с allrpg.info — то, что оставалось непокрытым последним.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class PhotoAndHelpTests(PhpSiteUnderTest site)
{
    [Test]
    public async Task PhotoSectionListsGamesWithPhotos()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/photo/");
        var document = SiteResponse.Parse(body);

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        // Заголовок снова без префикса — write_header() напрямую.
        await Assert.That(document.Title).IsEqualTo("Игры с фото/видео");

        // Отбор по счётчику photo_count > 0, как у отзывов по review_count.
        var text = document.Body!.TextContent;
        await Assert.That(text).Contains(TestData.WithReviewsGameName);
        await Assert.That(text).DoesNotContain(TestData.ActiveGameName);
    }

    [Test]
    public async Task GamePageEmbedsPhotoWithBrokenPreviewLink()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/game/{TestData.Games.WithReviews}/");

        // Фото выводится, автор подписан, отметка модератора видна.
        await Assert.That(body).Contains(TestData.PhotoAuthorName);
        await Assert.That(body).Contains("Выбор модератора");

        // А ссылка на превью ведёт в никуда: обработчика /photo/preview/<id> нет,
        // адрес отдаёт 404 (joinrpg/kogda-igra#75). Зафиксировано как есть.
        await Assert.That(body).Contains("/photo/preview/9601");

        using var preview = await client.GetAsync("/photo/preview/9601");
        await Assert.That((int)preview.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task YouTubeLinkIsRenderedAsVideoNotPhoto()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/game/{TestData.Games.WithReviews}/");

        // Тип определяется по подстроке в адресе (media.php): youtube.com даёт iframe,
        // и схема в нём — http, хотя сам сайт работает по https.
        await Assert.That(body).Contains("http://www.youtube.com/embed/TESTVIDEO");
    }

    [Test]
    public async Task PhotoEditingRequiresItsOwnPrivilege()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Четвёртое независимое право: редактор календаря сюда не попадает.
        using var moderator = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var denied = await moderator.GetAsync("/edit/photo/?id=9603");

        await Assert.That((int)denied.StatusCode).IsEqualTo(302);
    }

    [Test]
    public async Task PhotoModeratorCanOpenPhotoWithRegisteredAuthor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // У этого фото author_id заполнен, то есть автора указали через имя в ЖЖ.
        using var client = await site.CreateLoggedInClientAsync(TestData.PhotoModeratorUserId);
        var body = await client.GetStringAsync("/edit/photo/?id=9603");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(body).Contains(TestData.ModeratorUserName);
    }

    [Test]
    public async Task PhotoWithoutRegisteredAuthorBreaksEditPage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // А у этого author_id пустой — так сохраняется любое фото, автора которого
        // указали обычным текстом, без имени в ЖЖ (logic/photo.php). То есть случай
        // не экзотический, а обычный.
        //
        // action_edit зовёт get_user_by_id(NULL), в SQL уходит пустая строка вместо
        // числа, и дальше count() по false роняет страницу:
        //   PHP Fatal error: Uncaught TypeError: count(): Argument #1 ($value)
        //   must be of type Countable|array, false given in user_funcs.php:18
        //   #0 edit/photo/index.php(59): get_user_by_id(NULL)
        using var client = await site.CreateLoggedInClientAsync(TestData.PhotoModeratorUserId);
        using var response = await client.GetAsync("/edit/photo/?id=9601");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsTrue();
        await Assert.That(body).DoesNotContain("</body>");
    }

    [Test]
    public async Task HelpNeededPageIsTruncatedByFatalError()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync("/help-needed/");
        var body = await response.Content.ReadAsStringAsync();

        // Страница сломана: help-needed/index.php подключает только funcs.php и
        // calendar.php, а get_noemail_games() живёт в logic/problems.php, который
        // не приезжает ни оттуда, ни оттуда:
        //   PHP Fatal error: Call to undefined function get_noemail_games()
        //   #0 help-needed/index.php(27): EmailNeededCalendar->__construct()
        //
        // Вывод обрывается на первом же календаре: вводный текст есть, таблицы нет,
        // второй секции «Нет данных» нет, </body> нет. И всё это с HTTP 200.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("Нам очень не хватает e-mail мастеров");
        await Assert.That(body).DoesNotContain("Нет данных");
        await Assert.That(body).DoesNotContain("</body>");
        await Assert.That(body).DoesNotContain(TestData.FutureWithoutEmailGameName);
    }

    [Test]
    public async Task HelpNeededFormRequiresGameId()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/help-needed/email/");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task HelpNeededFormIsShownForGame()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Идентификатор передаётся путём. Форма со строкой запроса тут не работает:
        // правило ^help-needed/email/([0-9]*)/?$ совпадает и с пустым значением,
        // а свою строку запроса подставляет без флага QSA — переданный ?id= затирается
        // пустым, и страница уводит на главную. Тот же шаблон, что у /edit/users/.
        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/help-needed/email/912");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(SiteResponse.Parse(body).Title).IsEqualTo("Нет e-mail мастеров");
        await Assert.That(body).Contains("<form");
    }

    [Test]
    public async Task HelpNeededFormIgnoresQueryStringId()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Зафиксировано как есть: со строкой запроса идентификатор теряется
        // в правиле .htaccess, и страница уводит на главную, хотя игра существует.
        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/help-needed/email/?id=912");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task ExcelExportReturnsSpreadsheetAttachment()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync($"/data/export/excel/?year={TestData.CurrentYear}");

        await Assert.That((int)response.StatusCode).IsEqualTo(200);

        // Формат «как у Excel» — на деле это HTML-таблица с заголовками Excel,
        // а не настоящий .xls. Имя файла собирается из названия региона и года.
        await Assert.That(response.Content.Headers.ContentType!.MediaType)
            .IsEqualTo("application/vnd.ms-excel");
        await Assert.That(response.Content.Headers.ContentDisposition!.DispositionType)
            .IsEqualTo("attachment");
        await Assert.That(response.Content.Headers.ContentDisposition.FileName)
            .Contains(TestData.CurrentYear.ToString());

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains(TestData.ActiveGameName);
    }

    [Test]
    public async Task ExcelExportRejectsInvalidYear()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/data/export/excel/?year=1000");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task AllrpgSubmissionQueueRequiresModerator()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var anonymous = site.CreateClient(followRedirects: false);
        using var denied = await anonymous.GetAsync("/edit/zayavka/allrpg/");

        await Assert.That((int)denied.StatusCode).IsEqualTo(302);
        await Assert.That(denied.Headers.Location!.OriginalString).IsEqualTo("/");

        using var moderator = await site.CreateLoggedInClientAsync(TestData.ModeratorUserId);
        var body = await moderator.GetStringAsync("/edit/zayavka/allrpg/");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(SiteResponse.LooksLikeHtmlPage(body)).IsTrue();
    }
}
