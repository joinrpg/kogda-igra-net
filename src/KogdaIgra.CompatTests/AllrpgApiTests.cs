namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт интеграции с allrpg.info — <c>/api/allrpg-info/*</c>.
/// Интеграция входящая: allrpg.info сам обращается сюда, чтобы связать свою заявку
/// с игрой календаря.
/// <para>
/// <b>Оба эндпоинта сейчас нерабочие</b>, каждый по своей причине — см. комментарии
/// в тестах. Тесты фиксируют это как есть: пока PHP-версия ведёт себя так, порт
/// обязан вести себя так же, иначе сравнение перестанет быть честным. Когда починят,
/// тесты покраснеют — и это правильный сигнал обновить контракт.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class AllrpgApiTests(PhpSiteUnderTest site)
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

    private const string UpdateUrl = "/api/allrpg-info/update-allrpg.php";

    private static FormUrlEncodedContent LinkForm(int gameId, int allrpgId) =>
        new(new Dictionary<string, string>
        {
            ["id"] = gameId.ToString(),
            ["allrpg"] = allrpgId.ToString(),
        });

    private async Task<long> LinkCountAsync(int gameId, int allrpgId)
        => await Database.CountAsync(
            site.ConnectionString, "ki_games", $"id = {gameId} AND allrpg_info_id = {allrpgId}");

    [Test]
    [Arguments(TestData.KnownAllrpgId)]
    [Arguments(987654)]
    public async Task FindByIdIsCompletelyBroken(int allrpgId)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync($"/api/allrpg-info/find-by-id.php?id={allrpgId}");
        var body = await response.Content.ReadAsStringAsync();

        // Эндпоинт не работает ни при каких входных данных: find-by-id.php подключает
        // logic/edit.php, funcs.php и logic/allrpg.php, но не api.php, где определена
        // send_json_header() — и падает на первой же её строке:
        //   PHP Fatal error: Call to undefined function send_json_header()
        //
        // Наружу уходит HTTP 200 и тело из одного байта — перевода строки, который
        // стоит в файле до <?php. Проверено на проде: ответ там ровно такой же.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body.Trim()).IsEmpty();
    }

    [Test]
    public async Task UpdateWithoutParametersEchoesZeroesInsteadOfNotFound()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.PostAsync(UpdateUrl, new FormUrlEncodedContent([]));
        var body = await response.Content.ReadAsStringAsync();

        // В коде тут стоит header("HTTP/1.1 404 Not Found"), но он не срабатывает:
        // файл начинается с перевода строки до <?php, вывод уже начался, и заголовки
        // отправлены. Клиент получает 200, а телом — отладочная строка "0 0".
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body.Trim()).IsEqualTo("0 0");
    }

    [Test]
    public async Task UpdateNeverLinksAnythingEvenForModerator()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Тот же ведущий перевод строки мешает session_start(), $_SESSION остаётся null,
        // и check_edit_priv() падает внутри array_key_exists():
        //   PHP Fatal error: Uncaught TypeError: array_key_exists(): Argument #2
        //   ($array) must be of type array, null given
        //   #4 /var/www/html/api/allrpg-info/update-allrpg.php(16): check_edit_priv()
        //
        // То есть связь не проставляется никому и никогда, даже модератору.
        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.PostAsync(
            UpdateUrl, LinkForm(TestData.Games.InSaintPetersburg, 31337));

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(await LinkCountAsync(TestData.Games.InSaintPetersburg, 31337)).IsEqualTo(0);
    }

    [Test]
    public async Task UpdateDoesNothingForAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.PostAsync(
            UpdateUrl, LinkForm(TestData.Games.InSaintPetersburg, 31337));

        // Проверка прав тут тоже падает, но результат безопасный: до set_allrpg()
        // выполнение не доходит, данные не меняются. Редиректа на главную при этом
        // нет — вместо ожидаемого 302 приходит 200.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(await LinkCountAsync(TestData.Games.InSaintPetersburg, 31337)).IsEqualTo(0);
    }

    [Test]
    public async Task ExistingLinksStillWorkForOtherEndpoints()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Сама связь в данных жива и используется: get-by-id отдаёт allrpg_info_id,
        // и add_uri по нему отбрасывает повторные заявки. Сломаны именно два
        // эндпоинта под /api/allrpg-info/, а не интеграция на уровне данных.
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(
            $"/api/game/get-by-id.php?id={TestData.Games.Active}");

        await Assert.That(body).Contains($"\"allrpg_info_id\"");
        await Assert.That(await LinkCountAsync(TestData.Games.Active, TestData.KnownAllrpgId))
            .IsEqualTo(1);
    }
}
