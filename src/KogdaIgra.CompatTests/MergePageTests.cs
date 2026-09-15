namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт страницы слияния дублей <c>/edit/merge/</c>.
/// <para>
/// <b>Страница нерабочая.</b> В ней три отдельных фатальных ошибки PHP 8, и любой
/// осмысленный набор параметров приводит к одной из них — слить игры нельзя вообще.
/// Работает только вырожденный случай без параметров, который сразу упирается
/// в проверку прав.
/// </para>
/// <para>
/// Тесты фиксируют это как есть. Ничего не сливается, поэтому восстанавливать БД
/// не требуется — но именно в этом и суть: операция объявлена необратимой
/// («отменить слияние игр невозможно!»), а на деле не выполняется никогда.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class MergePageTests(PhpSiteUnderTest site)
{
    [Test]
    [Arguments("")]
    [Arguments("?id=910")]
    public async Task WithoutUsefulParametersRedirectsToHomePage(string query)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Единственная ветка, доходящая до проверки прав: анонимного разворачивает.
        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/edit/merge/{query}");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    [Arguments("?id=910&old_id=911", "Undefined constant \"id\" (строка 42: сравнение с id вместо $id)")]
    [Arguments("?id=910&action=merge", "Undefined constant \"merge\" (строка 75: сравнение с merge вместо 'merge')")]
    [Arguments("?name=zzz", "count(null) (строка 57: $old_ids не инициализирован, если поиск ничего не нашёл)")]
    public async Task AnyUsefulParameterCausesServerError(string query, string reason)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync($"/edit/merge/{query}");

        // Здесь, в отличие от архива, приходит честный 500: файл не начинается
        // с лишнего байта, вывод ещё не начался, и PHP успевает выставить статус.
        await Assert.That((int)response.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task MergeActionDoesNotTouchGamesEvenForAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Важное место. В коде слияние выполняется ДО проверки прав:
        //
        //   if (array_key_exists('action', $_GET) && $_GET['action'] == merge) { ... do_game_merge ... }
        //   if (!$id || !check_edit_priv()) { return_to_main(); }
        //
        // То есть порядок проверок неверный, и защищает сейчас только то, что
        // сравнение с неопределённой константой роняет скрипт раньше. Если
        // константы «починят», не тронув порядок, разрушительная операция станет
        // доступна анонимному посетителю по обычной ссылке.
        //
        // Тест сторожит фактическое положение дел: игры не меняются.
        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/edit/merge/?id=910&old_id=911&action=merge");

        var intact = await Database.CountAsync(
            site.ConnectionString, "ki_games", "id IN (910, 911) AND deleted_flag = 0");

        await Assert.That(intact).IsEqualTo(2);
    }
}
