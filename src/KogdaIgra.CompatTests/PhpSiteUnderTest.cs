using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
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

        // Порт выбираем сами: PHP-приложению нужно знать свой публичный адрес (SITENAME_HOST)
        // ещё до старта, поэтому случайный порт от Testcontainers тут не подходит.
        var hostPort = FindFreePort();

        site = new ContainerBuilder(ImageName)
            .WithNetwork(network)
            .WithPortBinding(hostPort, 8080)
            .WithEnvironment("POSTGRES_SERVER", DatabaseNetworkAlias)
            .WithEnvironment("POSTGRES_PORT", "5432")
            .WithEnvironment("POSTGRES_DATABASE", DatabaseName)
            .WithEnvironment("POSTGRES_USER", DatabaseUser)
            .WithEnvironment("POSTGRES_PASSWORD", DatabasePassword)
            .WithEnvironment("SITENAME_HOST", $"localhost:{hostPort}")
            .WithEnvironment("SITENAME_SCHEME", "http")
            // MAILGUN_KEY и JOINRPG_CLIENT_ID/SECRET намеренно не заданы: без них PHP-код
            // сам становится no-op на внешних интеграциях (требование ADR 0001).
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8080))
            .Build();

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

        if (network is not null)
        {
            await network.DisposeAsync();
            network = null;
        }
    }
}
