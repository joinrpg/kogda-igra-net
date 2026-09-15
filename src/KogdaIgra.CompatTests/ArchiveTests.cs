namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы архива <c>/archive/</c>.
/// <para>
/// <b>Страница сломана в проде.</b> Первый байт <c>archive/index.php</c> — перевод
/// строки, стоящий до <c>&lt;?php</c>. Он уходит в вывод сразу, HTTP-заголовки
/// отправляются, и <c>session_start()</c> в <c>user_funcs.php</c> уже не срабатывает.
/// Суперглобальная <c>$_SESSION</c> не создаётся, а <c>get_user_id()</c> делает по ней
/// <c>array_key_exists()</c> — на PHP 8 это фатальная ошибка. Вывод обрывается посреди
/// меню: ни заголовка <c>h1</c>, ни списка годов, ни закрывающего <c>&lt;/body&gt;</c>.
/// Проверено на https://kogda-igra.ru/archive/ — там ровно то же самое.
/// Заведено как joinrpg/kogda-igra#69.
/// </para>
/// <para>
/// Тесты ниже фиксируют это как есть: пока баг не исправлен в PHP-версии, порт
/// обязан вести себя так же, иначе сравнение перестанет быть честным. Когда багу
/// починят, эти тесты покраснеют — и это правильный сигнал обновить контракт.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ArchiveTests(PhpSiteUnderTest site)
{
    private async Task<HttpResponseMessage> GetArchiveAsync(HttpClient client)
        => await client.GetAsync("/archive/");

    [Test]
    public async Task RespondsWithTwoHundredDespiteFatalError()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await GetArchiveAsync(client);

        // Ровно та готча, ради которой существует SiteResponse: фатальная ошибка PHP
        // не мешает отдать 200. По статусу о работоспособности судить нельзя.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task TitleIsRenderedBeforeTheFatalError()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await GetArchiveAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        // Заголовок успевает вывестись: он идёт до вызова get_username().
        await Assert.That(SiteResponse.Parse(body).Title).IsEqualTo("КогдаИгра: Архив старых лет");
    }

    [Test]
    public async Task OutputIsTruncatedInsideTopMenu()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await GetArchiveAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        // Вывод обрывается на месте вызова get_username(): дальше в show() идёт блок
        // с кнопкой «Добавить игру», а после show() — h1 и список годов.
        await Assert.That(body).DoesNotContain("</body>");
        await Assert.That(body).DoesNotContain("<h1>");
        await Assert.That(body).DoesNotContain("Добавить");
    }

    [Test]
    public async Task YearLinksAreNeverRendered()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await GetArchiveAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        // Собственно содержимое архива — ссылки вида /<год>/ — до вывода не доходит.
        // Ссылка /<год> без завершающего слэша в теле есть, но это полоса годов
        // из верхнего меню, а не архив.
        await Assert.That(body).DoesNotContain($"<a href=\"/{TestData.CurrentYear}/\">");
    }
}
