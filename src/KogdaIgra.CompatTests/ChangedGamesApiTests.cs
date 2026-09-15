using System.Text.Json;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт <c>/api/game/changed.php</c> — эндпоинта инкрементальной синхронизации.
/// Внешняя система запоминает момент последнего обхода и спрашивает, что поменялось
/// с тех пор (<c>appcode/logic/gamelist.php</c>, <c>get_games_by_timestamp</c>).
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class ChangedGamesApiTests(PhpSiteUnderTest site)
{
    private async Task<JsonElement> GetChangedAsync(DateTimeOffset since)
    {
        using var client = site.CreateClient();
        var body = await client.GetStringAsync(
            $"/api/game/changed.php?timestamp={since.ToUnixTimeSeconds()}");

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static int[] Ids(JsonElement games)
        => [.. games.EnumerateArray().Select(g => g.GetProperty("id").GetInt32())];

    [Test]
    public async Task ReturnsGamesChangedAfterTimestamp()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Свежие правки засеяны сутки и двое суток назад.
        var games = await GetChangedAsync(DateTimeOffset.UtcNow.AddDays(-3));
        var ids = Ids(games);

        await Assert.That(ids).Contains(TestData.Games.Active);
        await Assert.That(ids).Contains(TestData.Games.InSaintPetersburg);
    }

    [Test]
    public async Task RespectsTheTimestampBoundary()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Между правками: та, что суточной давности, попадает, двухсуточная — нет.
        var ids = Ids(await GetChangedAsync(DateTimeOffset.UtcNow.AddHours(-36)));

        await Assert.That(ids).Contains(TestData.Games.Active);
        await Assert.That(ids).DoesNotContain(TestData.Games.InSaintPetersburg);
    }

    [Test]
    public async Task ReturnsEmptyArrayWhenNothingChanged()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var games = await GetChangedAsync(DateTimeOffset.UtcNow.AddDays(1));

        await Assert.That(games.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(games.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task HidesGamesAwaitingModeration()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // deleted_flag = -1 отфильтровывается явно: наружу не должно попадать то,
        // что ещё не одобрено модератором.
        var ids = Ids(await GetChangedAsync(DateTimeOffset.UnixEpoch));

        await Assert.That(ids).DoesNotContain(TestData.Games.AwaitingModeration);
    }

    [Test]
    public async Task ReportsDeletedGamesSoConsumersCanDropThem()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // А вот удалённые (deleted_flag = 1) отдаются — иначе потребитель никогда
        // не узнал бы, что игру убрали из календаря, и хранил бы её вечно.
        var games = await GetChangedAsync(DateTimeOffset.UnixEpoch);

        var deleted = games.EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("id").GetInt32() == TestData.Games.Deleted);

        await Assert.That(deleted.ValueKind).IsEqualTo(JsonValueKind.Object);

        // Внимание на тип: deleted_flag приезжает СТРОКОЙ "1", а не числом.
        // Драйвер pgsql отдаёт значения строками, а json_encode так их и сериализует.
        // Порт с типизированной моделью по умолчанию отдал бы число и сломал бы
        // потребителей со строгим разбором.
        await Assert.That(deleted.GetProperty("deleted_flag").GetString()).IsEqualTo("1");
    }

    [Test]
    public async Task EntryCarriesUpdateDateInIsoFormat()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var games = await GetChangedAsync(DateTimeOffset.UtcNow.AddDays(-3));
        var entry = games.EnumerateArray()
            .First(g => g.GetProperty("id").GetInt32() == TestData.Games.Active);

        // api.php форматирует update_date через DateTime::ATOM — это ISO 8601
        // со смещением, а не unix-время и не строка Postgres.
        var raw = entry.GetProperty("update_date").GetString();

        await Assert.That(DateTimeOffset.TryParse(raw, out _)).IsTrue();
        await Assert.That(raw).Contains("T");
    }
}
