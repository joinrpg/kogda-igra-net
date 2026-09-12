namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт подсказок поиска <c>/api/search-suggest/</c> — эндпоинт из
/// <c>opensearch.xml</c>, его дёргает адресная строка браузера.
/// Формат OpenSearch: <c>["запрос", ["вариант", ...]]</c>.
/// Ответ собирается конкатенацией строк, а не <c>json_encode</c>
/// (<c>api/search-suggest/index.php</c>), поэтому проверяются точные байты.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class SearchSuggestApiTests(PhpSiteUnderTest site)
{
    private async Task<string> SuggestAsync(string term)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync($"/api/search-suggest/?search={Uri.EscapeDataString(term)}");
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return body;
    }

    [Test]
    public async Task SuggestsGameNameForMatchingTerm()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await SuggestAsync("дракон");

        // Файл начинается с перевода строки до <?php, поэтому он есть и в теле ответа.
        await Assert.That(body.Trim()).IsEqualTo($"[\"дракон\", [\"{TestData.ActiveGameName}\"]]");
    }

    [Test]
    public async Task SuggestsBothGameNamesAndMasterGroups()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Запрос — UNION по названиям игр, мастерским группам и полигонам.
        var body = await SuggestAsync("Тестовая");

        await Assert.That(body).Contains("\"МГ Тестовая\"");
        await Assert.That(body).Contains($"\"{TestData.ActiveGameName}\"");
        // Разделитель — запятая с пробелом.
        await Assert.That(body).Contains("\", \"");
    }

    [Test]
    public async Task ExcludesDeletedAndUnmoderatedGames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await SuggestAsync("Тестовая");

        await Assert.That(body).DoesNotContain("Удалённая");
    }

    [Test]
    public async Task ReturnsEmptyListWhenNothingMatches()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var body = await SuggestAsync("заведомо-ничего-не-найдётся");

        await Assert.That(body.Trim()).IsEqualTo("[\"заведомо-ничего-не-найдётся\", []]");
    }

    [Test]
    public async Task EchoesTermBackWithoutEscaping()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Зафиксировано как есть: запрос вставляется в ответ без экранирования,
        // поэтому кавычка в запросе ломает JSON. Порт должен воспроизвести это
        // поведение — или сознательно исправить, но тогда с изменением теста.
        var body = await SuggestAsync("а\"б");

        await Assert.That(body.Trim()).StartsWith("[\"а\"б\", ");
    }
}
