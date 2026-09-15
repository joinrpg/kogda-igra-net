namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт справочника полигонов <c>/edit/polygons/</c>.
/// Закрыт третьим отдельным правом — <c>EDIT_POLYGONS</c>
/// (<c>edit/polygons/index.php</c>).
/// <para>Тесты меняют БД, поэтому состояние восстанавливается до и после каждого.</para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class PolygonAdminTests(PhpSiteUnderTest site)
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

    private const string PolygonsUrl = "/edit/polygons/";

    private async Task<long> PolygonCountAsync(string where)
        => await Database.CountAsync(site.ConnectionString, "ki_polygons", where);

    [Test]
    public async Task SectionIsClosedToAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync(PolygonsUrl);

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task CalendarModeratorCannotEditPolygons()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Третье независимое право: EDIT_GAMES сюда не пускает.
        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.GetAsync(PolygonsUrl);

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
    }

    [Test]
    public async Task PolygonEditorSeesList()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.PolygonEditorUserId);
        var body = await client.GetStringAsync(PolygonsUrl);
        var document = SiteResponse.Parse(body);

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();
        await Assert.That(document.Title).IsEqualTo("КогдаИгра: Панель управления :: Полигоны");

        // Названия полигонов лежат в атрибутах value полей ввода — список целиком
        // состоит из форм редактирования, поэтому в тексте страницы их нет.
        await Assert.That(body).Contains($"value=\"{TestData.EmptyPolygonName}\"");
    }

    [Test]
    public async Task ListHidesPlaceholderPolygons()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.PolygonEditorUserId);
        var body = await client.GetStringAsync(PolygonsUrl);

        // Запрос списка отбирает только meta_polygon = 0: полигоны-заглушки,
        // которыми помечают «место не указано», редактировать незачем.
        await Assert.That(body).DoesNotContain(TestData.MetaPolygonName);
    }

    [Test]
    public async Task DeleteButtonIsShownOnlyForPolygonWithoutGames()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.PolygonEditorUserId);
        var body = await client.GetStringAsync(PolygonsUrl);

        // Форма удаления рисуется, только когда на полигоне нет игр (COUNT(kg.id) = 0).
        await Assert.That(body).Contains($"id=\"delete{TestData.EmptyPolygonId}\"");
        await Assert.That(body).DoesNotContain($"id=\"delete{TestData.PolygonWithGames}\"");
    }

    [Test]
    public async Task PolygonEditorCanAddPolygon()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string name = "Новый полигон из теста";

        using var client = await site.CreateLoggedInClientAsync(TestData.PolygonEditorUserId);
        using var response = await client.PostAsync(PolygonsUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "save",
                ["id"] = "0",
                ["polygon_name"] = name,
                ["sub_region_id"] = "3",
            }));

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Полигон добавлен");

        await Assert.That(await PolygonCountAsync($"polygon_name = '{name}'")).IsEqualTo(1);
    }

    [Test]
    public async Task PolygonEditorCanRenamePolygon()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string name = "Переименованный полигон";

        using var client = await site.CreateLoggedInClientAsync(TestData.PolygonEditorUserId);
        using var response = await client.PostAsync(PolygonsUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "save",
                ["id"] = TestData.EmptyPolygonId.ToString(),
                ["polygon_name"] = name,
                ["sub_region_id"] = "3",
            }));

        await Assert.That(await PolygonCountAsync(
            $"polygon_id = {TestData.EmptyPolygonId} AND polygon_name = '{name}'")).IsEqualTo(1);
    }

    [Test]
    public async Task PolygonEditorCanDeleteEmptyPolygon()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.PolygonEditorUserId);
        using var response = await client.PostAsync(PolygonsUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["action"] = "delete",
                ["id"] = TestData.EmptyPolygonId.ToString(),
            }));

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Полигон удален");

        // Удаление здесь настоящее, а не мягкое: строка исчезает из таблицы,
        // в отличие от игр, у которых меняется только deleted_flag.
        await Assert.That(await PolygonCountAsync(
            $"polygon_id = {TestData.EmptyPolygonId}")).IsEqualTo(0);
    }
}
