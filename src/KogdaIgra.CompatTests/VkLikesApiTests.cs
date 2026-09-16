namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт <c>/api/game/update-likes.php</c> — единственного места, где приложение
/// само ходит наружу (<c>appcode/vk.php</c>, <c>update_vk_likes_for_game</c>).
/// <para>
/// В тестах <c>api.vkontakte.ru</c> уведён на 127.0.0.1 внутри контейнера, см.
/// <see cref="PhpSiteUnderTest"/>: запросы к настоящему VK из тестов недопустимы.
/// Поэтому здесь закрепляется именно поведение при недоступном VK — а оно
/// оказывается разрушительным, см. ниже.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class VkLikesApiTests(PhpSiteUnderTest site)
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

    private async Task<long> LikesAsync(int gameId)
        => await Database.ScalarAsync<long>(
            site.ConnectionString, $"SELECT vk_likes FROM ki_games WHERE id = {gameId}");

    [Test]
    public async Task UnreachableVkZeroesStoredLikes()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // У игры засеяно 50 лайков.
        await Assert.That(await LikesAsync(TestData.MostLikedGameId)).IsEqualTo(50);

        using var client = site.CreateClient();
        using var response = await client.GetAsync(
            $"/api/game/update-likes.php?id={TestData.MostLikedGameId}");

        // Ответ успешный, и по нему ничего не заподозрить.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);

        // А счётчик обнулён. Цепочка: file_get_contents() на недоступном хосте
        // возвращает false, json_decode(false) даёт null, intval(null) — ноль,
        // и ноль записывается в базу поверх настоящего значения.
        //
        // То есть любой сбой связи с VK стирает накопленные лайки, и восстановить
        // их можно только следующим удачным вызовом. Заведено как joinrpg/kogda-igra#77.
        await Assert.That(await LikesAsync(TestData.MostLikedGameId)).IsEqualTo(0);
    }

    [Test]
    public async Task EndpointIsOpenToAnyone()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Проверки прав здесь нет вовсе: update-likes.php состоит из четырёх строк
        // и сразу зовёт update_vk_likes_for_game(). Вместе с обнулением выше это
        // значит, что стереть лайки любой игре может кто угодно.
        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync(
            $"/api/game/update-likes.php?id={TestData.MostLikedInMoscowId}");

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(await LikesAsync(TestData.MostLikedInMoscowId)).IsEqualTo(0);
    }

    [Test]
    public async Task ResponseBodyLeaksDebugOutput()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        var body = await client.GetStringAsync(
            $"/api/game/update-likes.php?id={TestData.MostLikedGameId}");

        // В vk.php остался отладочный var_dump($result) — он и составляет весь ответ.
        // При недоступном VK это «NULL».
        await Assert.That(body).Contains("NULL");
    }

    [Test]
    public async Task MissingIdStillRunsTheUpdate()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient();
        using var response = await client.GetAsync("/api/game/update-likes.php");

        // Без параметра id получается intval('') = 0, и запрос обновляет игру с id = 0,
        // которой нет. Проверки на это в коде нет, но и вреда нет: строк не затронуто.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(await LikesAsync(TestData.MostLikedGameId)).IsEqualTo(50);
    }
}
