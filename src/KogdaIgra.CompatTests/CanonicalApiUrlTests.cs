using System.Text.Json;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Короткие адреса API из <c>.htaccess</c> — именно они описаны на странице
/// <c>/api/</c> и именно ими пользуются внешние потребители.
/// Остальные тесты API бьют по <c>*.php?id=</c>, то есть проверяют обработчик;
/// здесь проверяется маршрутизация к нему.
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class CanonicalApiUrlTests(PhpSiteUnderTest site)
{
    private async Task<JsonElement> GetJsonAsync(string url)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(url);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Test]
    public async Task ShortGameUrlReturnsSameDataAsQueryForm()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var short_ = await GetJsonAsync($"/api/game/{TestData.Games.Active}");
        var long_ = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.Active}");

        await Assert.That(short_.GetProperty("id").GetInt32()).IsEqualTo(TestData.Games.Active);
        await Assert.That(short_.GetProperty("name").GetString())
            .IsEqualTo(long_.GetProperty("name").GetString());
    }

    [Test]
    public async Task ShortGameUrlWorksWithAndWithoutTrailingSlash()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // В отличие от /find/, здесь слэш на конце необязателен и не ломает адрес:
        // правило написано как ([0-9]+)/?$ — группа не жадная до слэша.
        var withSlash = await GetJsonAsync($"/api/game/{TestData.Games.Active}/");

        await Assert.That(withSlash.GetProperty("id").GetInt32()).IsEqualTo(TestData.Games.Active);
    }

    [Test]
    public async Task ShortChangedUrlTakesTimestampFromPath()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var games = await GetJsonAsync("/api/changed/0");

        await Assert.That(games.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(games.GetArrayLength()).IsGreaterThan(0);
    }

    [Test]
    public async Task ShortAllrpgUrlHitsTheBrokenEndpoint()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Маршрут исправен, а обработчик за ним — нет: отдаёт 200 и пустое тело
        // (joinrpg/kogda-igra#74).
        using var client = site.CreateClient();
        using var response = await client.GetAsync($"/api/allrpg-info/{TestData.KnownAllrpgId}");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body.Trim()).IsEmpty();
    }

    [Test]
    public async Task ApiDocumentationPageIsPublic()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync("/api/");
        var document = SiteResponse.Parse(body);

        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Автоматизированные запросы");

        // Страница документирует именно короткие адреса — те, что проверены выше.
        var text = document.Body!.TextContent;
        await Assert.That(text).Contains("/api/game/");
        await Assert.That(text).Contains("allrpg.info");
    }
}
