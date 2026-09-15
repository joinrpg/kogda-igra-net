namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт управления учётными записями <c>/edit/users/</c>.
/// Раздел закрыт правом <c>USERS_CONTROL</c> — не тем же, что остальная модерация,
/// так что редактор календаря сюда не попадает (<c>edit/users/index.php</c>).
/// <para>Тесты меняют БД, поэтому состояние восстанавливается до и после каждого.</para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class UserAdminTests(PhpSiteUnderTest site)
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

    private async Task<long> PrivilegeCountAsync(int userId, int privilegeId)
        => await Database.CountAsync(
            site.ConnectionString, "user_privs", $"uid = {userId} AND pid = {privilegeId}");

    private async Task<string?> EmailAsync(int userId)
        => await Database.ScalarAsync<string>(
            site.ConnectionString, $"SELECT email FROM users WHERE user_id = {userId}");

    [Test]
    public async Task SectionIsClosedToAnonymousVisitor()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/edit/users/");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task CalendarModeratorCannotManageUsers()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Права разделены: EDIT_GAMES пускает в очереди модерации и ленты,
        // но не даёт трогать учётные записи.
        using var client = await site.CreateLoggedInClientAsync(
            TestData.ModeratorUserId, followRedirects: false);
        using var response = await client.GetAsync("/edit/users/");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task AdminSeesUserPage()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Идентификатор передаётся путём, а не строкой запроса — см. соседний тест.
        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        var body = await client.GetStringAsync($"/edit/users/{TestData.PlainUserId}");
        var document = SiteResponse.Parse(body);

        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsFalse();

        // Заголовок снова без префикса «КогдаИгра: » — write_header() напрямую.
        // Тире здесь длинное (—), как в исходнике.
        await Assert.That(document.Title).IsEqualTo($"Пользователь — {TestData.PlainUserName}");
    }

    [Test]
    public async Task QueryStringFormLeaksDatabaseError()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        using var response = await client.GetAsync($"/edit/users/?id={TestData.PlainUserId}");
        var body = await response.Content.ReadAsStringAsync();

        // Зафиксировано как есть. Правило .htaccess ^edit/users/([0-9]*)/?$ совпадает
        // и с пустым идентификатором, а подставляет свою строку запроса (флага QSA нет),
        // поэтому переданный ?id= затирается пустым. Дальше get_user_by_id('') уходит
        // в SQL с пустой строкой вместо числа.
        //
        // Наружу при этом отдаётся HTTP 200, а телом ответа становится сырой текст
        // ошибки Postgres — вся страница целиком. Ещё один довод, почему ассерт
        // по одному статус-коду в этих тестах бессмыслен.
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(SiteResponse.ContainsDatabaseError(body)).IsTrue();
        await Assert.That(SiteResponse.LooksLikeHtmlPage(body)).IsFalse();
    }

    [Test]
    public async Task AdminCanGrantPrivilege()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        await Assert.That(await PrivilegeCountAsync(
            TestData.PlainUserId, TestData.EditGamesPrivilegeId)).IsEqualTo(0);

        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        using var response = await client.PostAsync("/edit/users/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = TestData.PlainUserId.ToString(),
                ["grant"] = TestData.EditGamesPrivilegeId.ToString(),
            }));

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Привилегия предоставлена");

        await Assert.That(await PrivilegeCountAsync(
            TestData.PlainUserId, TestData.EditGamesPrivilegeId)).IsEqualTo(1);
    }

    [Test]
    public async Task AdminCanRevokePrivilege()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        using var response = await client.PostAsync("/edit/users/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = TestData.ModeratorUserId.ToString(),
                ["revoke"] = TestData.EditGamesPrivilegeId.ToString(),
            }));

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Привилегия отозвана");

        await Assert.That(await PrivilegeCountAsync(
            TestData.ModeratorUserId, TestData.EditGamesPrivilegeId)).IsEqualTo(0);
    }

    [Test]
    public async Task GrantingTwiceIsStoppedByPrimaryKey()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // grant_priv делает голый INSERT без проверки на дубль, но схема страхует:
        // у user_privs первичный ключ (uid, pid). Поэтому повторная выдача строку
        // не добавляет — она падает на уровне БД, и в страницу утекает текст ошибки.
        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        var form = new Dictionary<string, string>
        {
            ["id"] = TestData.PlainUserId.ToString(),
            ["grant"] = TestData.EditGamesPrivilegeId.ToString(),
        };

        using var first = await client.PostAsync("/edit/users/", new FormUrlEncodedContent(form));
        using var second = await client.PostAsync("/edit/users/", new FormUrlEncodedContent(form));

        await Assert.That(await PrivilegeCountAsync(
            TestData.PlainUserId, TestData.EditGamesPrivilegeId)).IsEqualTo(1);

        var secondBody = await second.Content.ReadAsStringAsync();
        await Assert.That(SiteResponse.ContainsDatabaseError(secondBody)).IsTrue();
    }

    [Test]
    public async Task AdminCanChangeEmail()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string newEmail = "renamed@example.com";

        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        using var response = await client.PostAsync("/edit/users/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = TestData.PlainUserId.ToString(),
                ["new_email"] = newEmail,
            }));

        await Assert.That(await EmailAsync(TestData.PlainUserId)).IsEqualTo(newEmail);
    }

    [Test]
    public async Task EmailAlreadyTakenIsRejected()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // force_set_email обновляет строку только если такого адреса нет у другого
        // пользователя, причём сравнение регистронезависимое (ILIKE) — поэтому
        // адрес в другом регистре тоже считается занятым.
        using var client = await site.CreateLoggedInClientAsync(TestData.AdminUserId);
        using var response = await client.PostAsync("/edit/users/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = TestData.PlainUserId.ToString(),
                ["new_email"] = "MODERATOR@example.com",
            }));

        await Assert.That(await EmailAsync(TestData.PlainUserId)).IsEqualTo("plain@example.com");
    }
}
