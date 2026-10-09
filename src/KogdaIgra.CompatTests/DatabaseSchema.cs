using DotNet.Testcontainers.Containers;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Схема kogda-igra, взятая из образа сайта.
/// <para>
/// PHP-репозиторий кладёт в образ свой <c>db/init/</c> (снимок dev-базы: структура и
/// справочники) каталогом <see cref="DirectoryInImage"/>. Схема читается из того же образа,
/// против которого идут тесты, поэтому версии схемы и кода совпадают всегда — в том числе
/// при подмене образа через <c>KOGDA_IGRA_IMAGE</c> (см. db/README.md).
/// </para>
/// </summary>
public sealed class DatabaseSchema
{
    /// <summary>Каталог со скриптами схемы внутри образа PHP-версии.</summary>
    public const string DirectoryInImage = "/db-init";

    private readonly IReadOnlyList<(string Name, byte[] Content)> scripts;

    private DatabaseSchema(IReadOnlyList<(string Name, byte[] Content)> scripts) => this.scripts = scripts;

    /// <summary>
    /// Читает скрипты из запущенного контейнера сайта. Порядок — по имени файла,
    /// как их выполнил бы <c>docker-entrypoint-initdb.d</c> в Postgres.
    /// </summary>
    public static async Task<DatabaseSchema> ReadFromSiteAsync(
        IContainer site,
        CancellationToken cancellationToken = default)
    {
        var listing = await site.ExecAsync(["ls", "-1", DirectoryInImage], cancellationToken);
        if (listing.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"В образе {site.Image.FullName} нет {DirectoryInImage}: образ собран раньше, " +
                $"чем схема переехала в PHP-репозиторий. {listing.Stderr}");
        }

        var names = listing.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
        {
            throw new InvalidOperationException($"В {DirectoryInImage} образа {site.Image.FullName} нет ни одного .sql.");
        }

        var scripts = new List<(string Name, byte[] Content)>();
        foreach (var name in names)
        {
            scripts.Add((name, await site.ReadFileAsync($"{DirectoryInImage}/{name}", cancellationToken)));
        }

        return new DatabaseSchema(scripts);
    }

    /// <summary>
    /// Применяет схему и справочники.
    /// Выполняется <c>psql</c> внутри контейнера, а не через Npgsql: вывод <c>pg_dump</c> —
    /// это скрипт для psql, а не чистый SQL. В нём есть мета-команды (<c>\restrict</c>,
    /// <c>\unrestrict</c>) и блоки <c>COPY ... FROM stdin</c>, которые обычное соединение
    /// выполнить не может.
    /// </summary>
    public async Task ApplyAsync(
        IContainer database,
        string user,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        foreach (var (name, content) in scripts)
        {
            var pathInContainer = $"/tmp/{name}";
            await database.CopyAsync(content, pathInContainer, ct: cancellationToken);

            var result = await database.ExecAsync(
                [
                    "psql",
                    "--username", user,
                    "--dbname", databaseName,
                    // Без этого psql проглотит ошибку и молча оставит схему недоделанной.
                    "--set", "ON_ERROR_STOP=1",
                    "--file", pathInContainer,
                ],
                cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Не применился {name} (код {result.ExitCode}): {result.Stderr}");
            }
        }
    }

    /// <summary>
    /// Возвращает БД в исходное состояние: для write-тестов, которым нужна чистая база
    /// перед каждой проверкой (ADR 0001).
    /// </summary>
    public async Task ResetAsync(
        IContainer database,
        string user,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        var result = await database.ExecAsync(
            [
                "psql",
                "--username", user,
                "--dbname", databaseName,
                "--set", "ON_ERROR_STOP=1",
                "--command", "DROP SCHEMA public CASCADE; CREATE SCHEMA public;",
            ],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Не удалось очистить схему: {result.Stderr}");
        }

        await ApplyAsync(database, user, databaseName, cancellationToken);
    }
}
