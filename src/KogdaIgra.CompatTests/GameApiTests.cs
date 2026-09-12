using System.Text.Json;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт JSON API <c>/api/game/*</c>. Его дёргают внешние потребители
/// (в первую очередь allrpg.info и joinrpg), поэтому набор полей и их имена —
/// самая жёсткая часть контракта: порт обязан отдавать ровно то же.
/// Список полей задан белым списком в <c>appcode/api.php</c>
/// (<c>strip_game_object_before_json</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class GameApiTests(PhpSiteUnderTest site)
{
    private async Task<JsonElement> GetJsonAsync(string url)
    {
        using var client = site.CreateClient();
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Test]
    public async Task GetByIdReturnsWhitelistedFields()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var game = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.Active}");

        await Assert.That(game.GetProperty("id").GetInt32()).IsEqualTo(TestData.Games.Active);
        await Assert.That(game.GetProperty("name").GetString()).IsEqualTo(TestData.ActiveGameName);
        await Assert.That(game.GetProperty("game_type_name").GetString()).IsEqualTo("Полевая");
        await Assert.That(game.GetProperty("status_name").GetString()).IsEqualTo("OK");
        await Assert.That(game.GetProperty("polygon_name").GetString()).IsEqualTo("Тестовый полигон");
    }

    [Test]
    public async Task GetByIdHidesNonWhitelistedFields()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var game = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.Active}");

        // Белый список в api.php не содержит внутренних счётчиков и флагов —
        // наружу они попадать не должны.
        foreach (var hidden in new[] { "vk_likes", "review_count", "photo_count", "show_flags", "hide_email" })
        {
            await Assert.That(game.TryGetProperty(hidden, out _)).IsFalse();
        }
    }

    [Test]
    public async Task GetByIdOmitsEmailWhenHidden()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var hidden = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.WithHiddenEmail}");
        var visible = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.Active}");

        // hide_email = 1 — поле email вырезается целиком (unset), а не пустеет.
        await Assert.That(hidden.TryGetProperty("email", out _)).IsFalse();
        await Assert.That(visible.GetProperty("email").GetString()).IsEqualTo("dragon@example.com");
    }

    [Test]
    public async Task GetByIdReturnsEmptyObjectForGameAwaitingModeration()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // get-by-id.php отдаёт "{}" при deleted_flag = -1.
        var game = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.AwaitingModeration}");

        await Assert.That(game.EnumerateObject().Any()).IsFalse();
    }

    [Test]
    public async Task DeletedGameIsStillReturnedByApi()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Зафиксировано как есть: API отсекает только deleted_flag = -1, а удалённую
        // игру (deleted_flag = 1) отдаёт вместе с самим флагом — потребители
        // ориентируются на него. На сайте такая игра при этом не показывается.
        var game = await GetJsonAsync($"/api/game/get-by-id.php?id={TestData.Games.Deleted}");

        await Assert.That(game.GetProperty("id").GetInt32()).IsEqualTo(TestData.Games.Deleted);
        await Assert.That(game.GetProperty("deleted_flag").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task UnknownGameReturnsEmptyObject()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var game = await GetJsonAsync("/api/game/get-by-id.php?id=987654");

        await Assert.That(game.EnumerateObject().Any()).IsFalse();
    }
}
