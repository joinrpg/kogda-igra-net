using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace KogdaIgra.CompatTests;

/// <summary>
/// PHP-оригинал как чёрный ящик: образ из ghcr.io плюс Postgres рядом, ровно в той
/// конфигурации, что описана в docker-compose.yml PHP-репозитория (ADR 0001).
/// Поднимается один раз на прогон.
/// </summary>
public sealed class PhpSiteUnderTest : ISiteUnderTest, IAsyncInitializer, IAsyncDisposable
{
    /// <summary>Образ, который публикует CI PHP-репозитория.</summary>
    public const string DefaultImageName = "ghcr.io/joinrpg/kogda-igra:latest";

    /// <summary>Переменная окружения для подмены образа.</summary>
    public const string ImageEnvironmentVariable = "KOGDA_IGRA_IMAGE";

    /// <summary>
    /// Переменная окружения с каталогом, куда складывать сырые данные покрытия PHP.
    /// Имеет смысл только вместе с образом, собранным из <c>coverage/Dockerfile</c>.
    /// </summary>
    public const string CoverageEnvironmentVariable = "COMPAT_COVERAGE_DIR";

    /// <summary>Имя, под которым PHP-контейнер видит поддельный сервер входа.</summary>
    private const string IdentityNetworkAlias = "joinrpg-id";

    /// <summary>Идентификатор клиента, который приложение шлёт при обмене кода.</summary>
    public const string OAuthClientId = "compat-test-client";

    /// <summary>Секрет клиента — проверяется тестами в журнале запросов.</summary>
    public const string OAuthClientSecret = "compat-test-secret";

    /// <summary>
    /// Хосты, обращения к которым не должны уходить наружу во время тестов.
    /// </summary>
    private static readonly string[] UnreachableExternalHosts =
    [
        "api.vkontakte.ru",
        "api.eu.mailgun.net",
        "id.joinrpg.ru",
        "devid.joinrpg.ru",
    ];

    /// <summary>
    /// Образ PHP-версии. По умолчанию — опубликованный в ghcr.io: пакет публичный,
    /// анонимный pull работает. Подменить (например, на локально собранный из соседнего
    /// чекаута PHP-репозитория) можно через <see cref="ImageEnvironmentVariable"/> —
    /// см. docs/compat-tests.md.
    /// </summary>
    public static string ImageName =>
        Environment.GetEnvironmentVariable(ImageEnvironmentVariable) is { Length: > 0 } custom
            ? custom
            : DefaultImageName;

    private const string DatabaseName = "kogdaigra";
    private const string DatabaseUser = "kogdauser";
    private const string DatabasePassword = "kogdapass";

    /// <summary>Имя, под которым PHP-контейнер видит БД (POSTGRES_SERVER в compose).</summary>
    private const string DatabaseNetworkAlias = "kogda-db";

    private INetwork? network;
    private PostgreSqlContainer? database;
    private IContainer? site;
    private IContainer? identityProvider;
    private string? identityRequestLog;

    /// <summary>
    /// Причина, по которой сайт не поднялся (нет docker, нет доступа к приватному образу и т.п.).
    /// Тесты используют её, чтобы пропуститься с внятным сообщением, а не упасть.
    /// </summary>
    public string? StartupFailure { get; private set; }

    public Uri BaseUrl
    {
        get => field ?? throw new InvalidOperationException(
            $"Сайт не поднят: {StartupFailure ?? "InitializeAsync не вызывался"}");
        private set;
    }

    public HttpClient CreateClient(bool followRedirects = true)
        => new(new HttpClientHandler { AllowAutoRedirect = followRedirects })
        {
            BaseAddress = BaseUrl,
        };

    public async Task InitializeAsync()
    {
        try
        {
            await StartAsync();
        }
        catch (Exception e)
        {
            StartupFailure = e.Message;
            await DisposeAsync();
        }
    }

    private async Task StartAsync()
    {
        network = new NetworkBuilder().Build();

        // Версия прода PHP-версии: dev и prod — одна и та же СУБД в Yandex Cloud
        // (видно по utils/migrate-db в PHP-репозитории), pg_dump отчитался о 15.18.
        // docker-compose.yml PHP-репозитория указывает 14.2 — он отстал от реальности.
        database = new PostgreSqlBuilder("postgres:15.18")
            .WithDatabase(DatabaseName)
            .WithUsername(DatabaseUser)
            .WithPassword(DatabasePassword)
            .WithNetwork(network)
            .WithNetworkAliases(DatabaseNetworkAlias)
            .Build();

        await database.StartAsync();
        await DatabaseSchema.ApplyAsync(database, DatabaseUser, DatabaseName);
        await TestData.SeedAsync(database, DatabaseUser, DatabaseName);

        // Поддельный сервер входа поднимается до сайта: приложению нужен его адрес.
        // Работает по обычному HTTP — адрес берётся из JOINRPG_ID_BASE, так что
        // сертификат не нужен и PHP-код не меняется. Подробности — в docs/compat-tests.md.
        identityRequestLog = Path.Combine(Path.GetTempPath(), $"kogda-oidc-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(identityRequestLog);

        // Журнал пишет Apache в контейнере от www-data, а каталог на хосте принадлежит
        // пользователю, запустившему тесты. На Linux (CI) с правами 755 запись молча
        // не проходит — мок пишет через @file_put_contents, — и журнал остаётся пустым.
        // Docker Desktop на Windows права не проверяет, поэтому локально это не видно.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                identityRequestLog,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }

        identityProvider = new ContainerBuilder(ImageName)
            .WithNetwork(network)
            .WithNetworkAliases(IdentityNetworkAlias)
            .WithBindMount(Path.Combine(RepositoryLayout.Root.FullName, "oidc-mock"), "/var/www/html")
            .WithBindMount(identityRequestLog, "/requests")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8080))
            .Build();

        await identityProvider.StartAsync();

        // Порт выбираем сами: PHP-приложению нужно знать свой публичный адрес (SITENAME_HOST)
        // ещё до старта, поэтому случайный порт от Testcontainers тут не подходит.
        var hostPort = FindFreePort();

        var builder = new ContainerBuilder(ImageName)
            .WithNetwork(network)
            .WithPortBinding(hostPort, 8080)
            .WithEnvironment("POSTGRES_SERVER", DatabaseNetworkAlias)
            .WithEnvironment("POSTGRES_PORT", "5432")
            .WithEnvironment("POSTGRES_DATABASE", DatabaseName)
            .WithEnvironment("POSTGRES_USER", DatabaseUser)
            .WithEnvironment("POSTGRES_PASSWORD", DatabasePassword)
            .WithEnvironment("SITENAME_HOST", $"localhost:{hostPort}")
            .WithEnvironment("SITENAME_SCHEME", "http")
            .WithEnvironment("JOINRPG_ID_BASE", $"http://{IdentityNetworkAlias}:8080")
            .WithEnvironment("JOINRPG_CLIENT_ID", OAuthClientId)
            .WithEnvironment("JOINRPG_CLIENT_SECRET", OAuthClientSecret)
            // MAILGUN_KEY намеренно не задан: без него Email::mg_send выходит первой
            // же строкой, и почта наружу не уходит (требование ADR 0001).
            // Ключи OAuth, наоборот, заданы — вход проверяется против поддельного
            // сервера, поднятого выше.
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8080));

        // Внешние хосты уводятся в никуда. Ключей для почты и OAuth в окружении нет,
        // поэтому туда обращений и так не будет, а вот VK приложение дёргает без
        // всяких ключей: без этой подмены тест эндпоинта лайков стучался бы
        // в настоящий api.vkontakte.ru с машины разработчика и из CI.
        //
        // 127.0.0.1 внутри контейнера — это сам контейнер, где на 443 никто
        // не слушает, так что соединение отвергается сразу, без ожидания таймаута.
        foreach (var host in UnreachableExternalHosts)
        {
            builder = builder.WithExtraHost(host, "127.0.0.1");
        }

        // Замер покрытия PHP: каталог с хоста подключается внутрь, и образ с Xdebug
        // складывает туда по файлу на запрос. Включается только переменной окружения,
        // на обычный прогон не влияет — см. coverage/README.md.
        var coverageDirectory = Environment.GetEnvironmentVariable(CoverageEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(coverageDirectory))
        {
            _ = Directory.CreateDirectory(coverageDirectory);
            builder = builder.WithBindMount(Path.GetFullPath(coverageDirectory), "/coverage-data");
        }

        site = builder.Build();

        await site.StartAsync();

        BaseUrl = new Uri($"http://localhost:{hostPort}/");
        await WaitUntilRespondsAsync();
    }

    /// <summary>
    /// Ждём не статус-код, а осмысленный ответ: на сломанной БД этот PHP отдаёт 200
    /// с текстом SQL-ошибки в теле, так что по коду ответа готовность не определить.
    /// </summary>
    private async Task WaitUntilRespondsAsync()
    {
        using var client = CreateClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        string lastBody;

        do
        {
            lastBody = await client.GetStringAsync("/");
            if (SiteResponse.LooksLikeHtmlPage(lastBody))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        while (DateTimeOffset.UtcNow < deadline);

        throw new InvalidOperationException(
            $"Сайт поднялся, но не отдаёт страницу. Последний ответ: {SiteResponse.Excerpt(lastBody)}");
    }

    /// <summary>
    /// Строка подключения к БД сайта — для ассертов по состоянию таблиц.
    /// Write-тесты обязаны проверять и ответ, и то, что реально легло в базу (ADR 0001).
    /// </summary>
    public string ConnectionString => database?.GetConnectionString()
        ?? throw new InvalidOperationException($"БД не поднята: {StartupFailure}");

    /// <summary>
    /// Клиент, входящий на сайт как засеянный пользователь.
    /// <para>
    /// Вход в PHP-версии идёт только через OAuth JoinRPG, а он в тестах заглушён
    /// отсутствием ключей. Но авторизация опирается лишь на <c>$_SESSION['user_id']</c>
    /// (<c>appcode/user_funcs.php</c>), а сессии PHP в этом образе хранит файлами
    /// в <c>/tmp</c>. Поэтому харнесс кладёт готовый файл сессии в контейнер и
    /// присылает соответствующую cookie — PHP-код при этом не меняется и не
    /// подменяется, что и требует ADR 0001.
    /// </para>
    /// </summary>
    public async Task<HttpClient> CreateLoggedInClientAsync(
        int userId,
        bool followRedirects = true,
        CancellationToken cancellationToken = default)
    {
        if (site is null)
        {
            throw new InvalidOperationException($"Сайт не поднят: {StartupFailure}");
        }

        // Идентификатор сессии произвольный: PHP берёт его из cookie как есть.
        var sessionId = $"compattest{userId:D6}";

        // Формат сериализации сессий PHP по умолчанию: <ключ>|<значение в serialize()>.
        var payload = $"user_id|i:{userId};";

        await site.CopyAsync(
            System.Text.Encoding.UTF8.GetBytes(payload),
            $"/tmp/sess_{sessionId}",
            // Файл создаётся от root, а Apache работает под www-data — без права
            // на запись PHP не сможет обновить сессию и молча начнёт новую.
            fileMode: UnixFileModes.UserRead | UnixFileModes.UserWrite
                | UnixFileModes.GroupRead | UnixFileModes.GroupWrite
                | UnixFileModes.OtherRead | UnixFileModes.OtherWrite,
            ct: cancellationToken);

        var client = CreateClient(followRedirects);
        client.DefaultRequestHeaders.Add("Cookie", $"PHPSESSID={sessionId}");
        return client;
    }

    /// <summary>
    /// Запросы, которые приложение отправило на сервер входа, в порядке поступления.
    /// Каждая строка — JSON с полями запроса.
    /// </summary>
    public IReadOnlyList<string> IdentityProviderRequests
    {
        get
        {
            var log = Path.Combine(identityRequestLog ?? string.Empty, "requests.log");
            return File.Exists(log) ? File.ReadAllLines(log) : [];
        }
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (database is null)
        {
            throw new InvalidOperationException("БД не поднята.");
        }

        await DatabaseSchema.ResetAsync(database, DatabaseUser, DatabaseName, cancellationToken);
        await TestData.SeedAsync(database, DatabaseUser, DatabaseName, cancellationToken);
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (site is not null)
        {
            await site.DisposeAsync();
            site = null;
        }

        if (database is not null)
        {
            await database.DisposeAsync();
            database = null;
        }

        if (identityProvider is not null)
        {
            await identityProvider.DisposeAsync();
            identityProvider = null;
        }

        if (network is not null)
        {
            await network.DisposeAsync();
            network = null;
        }

        if (identityRequestLog is not null && Directory.Exists(identityRequestLog))
        {
            try
            {
                Directory.Delete(identityRequestLog, recursive: true);
            }
            catch (IOException)
            {
                // Каталог мог остаться занятым docker-демоном; для теста это неважно.
            }

            identityRequestLog = null;
        }
    }
}
