using System.Text.Json;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Контракт входа через JoinRPG — <c>/login/joinrpg/</c>.
/// Authorization Code flow с PKCE; учётные записи сопоставляются по адресу почты.
/// <para>
/// Сервер входа подменён: его адрес берётся из <c>JOINRPG_ID_BASE</c>, поэтому
/// харнесс поднимает свой по обычному HTTP. Сертификат не нужен, PHP-код не меняется.
/// </para>
/// </summary>
[ClassDataSource<PhpSiteUnderTest>(Shared = SharedType.PerTestSession)]
public class OAuthLoginTests(PhpSiteUnderTest site)
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

    /// <summary>
    /// Проходит первый шаг входа и возвращает клиента с живой сессией и значение
    /// <c>state</c>, которое приложение положило в сессию. Без этого шага обратный
    /// вызов отвергается: сравнивать будет не с чем.
    /// </summary>
    private async Task<(HttpClient Client, string State)> StartLoginAsync()
    {
        var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/login/joinrpg/");

        var location = response.Headers.Location!.OriginalString;
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(location).Query);

        return (client, query["state"]!);
    }

    private async Task<long> UserCountAsync(string email)
        => await Database.CountAsync(site.ConnectionString, "users", $"email = '{email}'");

    [Test]
    public async Task LoginRedirectsToAuthorizationEndpointWithPkce()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/login/joinrpg/");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);

        var location = new Uri(response.Headers.Location!.OriginalString);
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);

        await Assert.That(location.AbsolutePath).IsEqualTo("/connect/authorize");
        await Assert.That(query["client_id"]).IsEqualTo(PhpSiteUnderTest.OAuthClientId);
        await Assert.That(query["response_type"]).IsEqualTo("code");
        await Assert.That(query["scope"]).IsEqualTo("openid email");
        await Assert.That(query["code_challenge_method"]).IsEqualTo("S256");

        // state и challenge генерируются из случайных байтов и кодируются
        // в base64url — без символов +, / и хвостовых =.
        foreach (var name in new[] { "state", "code_challenge" })
        {
            var value = query[name]!;
            await Assert.That(value).IsNotEmpty();
            await Assert.That(value).DoesNotContain("+");
            await Assert.That(value).DoesNotContain("/");
            await Assert.That(value).DoesNotContain("=");
        }
    }

    [Test]
    public async Task EachLoginAttemptGetsFreshState()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var (first, firstState) = await StartLoginAsync();
        first.Dispose();
        var (second, secondState) = await StartLoginAsync();
        second.Dispose();

        await Assert.That(firstState).IsNotEqualTo(secondState);
    }

    [Test]
    public async Task SuccessfulCallbackCreatesUserByEmail()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string email = "новичок@example.com";

        var (client, state) = await StartLoginAsync();
        using (client)
        {
            using var response = await client.GetAsync(
                $"/login/joinrpg/?code={Uri.EscapeDataString(email)}&state={Uri.EscapeDataString(state)}");

            // Успех или отказ — ответ одинаковый, редирект на главную.
            await Assert.That((int)response.StatusCode).IsEqualTo(302);
            await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
        }

        // Пользователя с таким адресом не было — он заводится.
        await Assert.That(await UserCountAsync(email)).IsEqualTo(1);
    }

    [Test]
    public async Task CallbackLinksExistingAccountByEmail()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Учётки сопоставляются по адресу почты — это и есть способ связать
        // аккаунт JoinRPG с уже существующим здесь.
        const string email = "moderator@example.com";

        var before = await Database.CountAsync(site.ConnectionString, "users", "1 = 1");

        var (client, state) = await StartLoginAsync();
        using (client)
        {
            using var response = await client.GetAsync(
                $"/login/joinrpg/?code={Uri.EscapeDataString(email)}&state={Uri.EscapeDataString(state)}");
        }

        var after = await Database.CountAsync(site.ConnectionString, "users", "1 = 1");

        // Новая запись не создаётся: адрес уже принадлежит модератору.
        await Assert.That(after).IsEqualTo(before);
        await Assert.That(await UserCountAsync(email)).IsEqualTo(1);
    }

    [Test]
    public async Task ApplicationSendsPkceVerifierAndClientSecret()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string email = "проверка-обмена@example.com";

        var (client, state) = await StartLoginAsync();
        using (client)
        {
            using var response = await client.GetAsync(
                $"/login/joinrpg/?code={Uri.EscapeDataString(email)}&state={Uri.EscapeDataString(state)}");
        }

        // Смотрим, что именно приложение прислало на сервер входа.
        var exchange = site.IdentityProviderRequests
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Last(r => r.GetProperty("endpoint").GetString() == "token");

        await Assert.That(exchange.GetProperty("grant_type").GetString())
            .IsEqualTo("authorization_code");
        await Assert.That(exchange.GetProperty("client_id").GetString())
            .IsEqualTo(PhpSiteUnderTest.OAuthClientId);
        await Assert.That(exchange.GetProperty("client_secret").GetString())
            .IsEqualTo(PhpSiteUnderTest.OAuthClientSecret);

        // PKCE: на обмене отправляется verifier, а не challenge.
        var verifier = exchange.GetProperty("code_verifier").GetString()!;
        await Assert.That(verifier).IsNotEmpty();
        await Assert.That(verifier).DoesNotContain("=");

        // redirect_uri должен совпадать с тем, что уходил на первом шаге.
        await Assert.That(exchange.GetProperty("redirect_uri").GetString())
            .IsEqualTo($"http://{site.BaseUrl.Authority}/login/joinrpg/");
    }

    [Test]
    public async Task CallbackWithWrongStateIsRejected()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string email = "подделка@example.com";

        var (client, _) = await StartLoginAsync();
        using (client)
        {
            // Чужой state — защита от CSRF. Сверка идёт через hash_equals.
            using var response = await client.GetAsync(
                $"/login/joinrpg/?code={Uri.EscapeDataString(email)}&state=не-тот-state");

            await Assert.That((int)response.StatusCode).IsEqualTo(302);
        }

        await Assert.That(await UserCountAsync(email)).IsEqualTo(0);
    }

    [Test]
    public async Task CallbackWithoutStartedFlowIsRejected()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string email = "без-начала@example.com";

        // Ни state, ни verifier в сессии нет — обмен кода даже не начинается.
        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync(
            $"/login/joinrpg/?code={Uri.EscapeDataString(email)}&state=что-угодно");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(await UserCountAsync(email)).IsEqualTo(0);
    }

    [Test]
    public async Task StateIsConsumedSoCodeCannotBeReplayed()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        const string first = "первый-вход@example.com";
        const string second = "повтор@example.com";

        var (client, state) = await StartLoginAsync();
        using (client)
        {
            using var ok = await client.GetAsync(
                $"/login/joinrpg/?code={Uri.EscapeDataString(first)}&state={Uri.EscapeDataString(state)}");

            // state и verifier вычищаются из сессии сразу, ещё до обмена кода,
            // поэтому повторный вызов с тем же state уже не проходит.
            using var replay = await client.GetAsync(
                $"/login/joinrpg/?code={Uri.EscapeDataString(second)}&state={Uri.EscapeDataString(state)}");
        }

        await Assert.That(await UserCountAsync(first)).IsEqualTo(1);
        await Assert.That(await UserCountAsync(second)).IsEqualTo(0);
    }

    [Test]
    public async Task ProviderErrorIsIgnoredQuietly()
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        // Пользователь отказал в доступе: приложение молча возвращает на главную.
        using var client = site.CreateClient(followRedirects: false);
        using var response = await client.GetAsync("/login/joinrpg/?error=access_denied");

        await Assert.That((int)response.StatusCode).IsEqualTo(302);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo("/");
    }

    [Test]
    [Arguments("deny-token")]
    [Arguments("deny-email")]
    public async Task FailedExchangeDoesNotCreateUser(string code)
    {
        Skip.When(site.StartupFailure is not null, $"PHP-сайт не поднялся: {site.StartupFailure}");

        var before = await Database.CountAsync(site.ConnectionString, "users", "1 = 1");

        var (client, state) = await StartLoginAsync();
        using (client)
        {
            // deny-token: сервер не отдал access_token.
            // deny-email: токен выдан, но в user_info нет адреса почты.
            using var response = await client.GetAsync(
                $"/login/joinrpg/?code={code}&state={Uri.EscapeDataString(state)}");

            await Assert.That((int)response.StatusCode).IsEqualTo(302);
        }

        await Assert.That(await Database.CountAsync(site.ConnectionString, "users", "1 = 1"))
            .IsEqualTo(before);
    }
}
