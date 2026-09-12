using DotNet.Testcontainers.Containers;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Накатывает схему kogda-igra на пустую БД.
/// Скрипты — это вывод <c>pg_dump</c> с живой dev-базы, снятый
/// <c>utils/fetch-dev-db.ps1</c> (см. db/README.md).
/// </summary>
public static class DatabaseSchema
{
    /// <summary>
    /// Применяет схему и справочники.
    /// Выполняется <c>psql</c> внутри контейнера, а не через Npgsql: вывод <c>pg_dump</c> —
    /// это скрипт для psql, а не чистый SQL. В нём есть мета-команды (<c>\restrict</c>,
    /// <c>\unrestrict</c>) и блоки <c>COPY ... FROM stdin</c>, которые обычное соединение
    /// выполнить не может.
    /// </summary>
    public static async Task ApplyAsync(
        IContainer database,
        string user,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        foreach (var script in EnumerateScripts())
        {
            // CopyAsync принимает каталог назначения, имя файла берётся из источника.
            const string directoryInContainer = "/tmp";
            var pathInContainer = $"{directoryInContainer}/{script.Name}";
            await database.CopyAsync(script, directoryInContainer, ct: cancellationToken);

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
                    $"Не применился {script.Name} (код {result.ExitCode}): {result.Stderr}");
            }
        }
    }

    /// <summary>
    /// Возвращает БД в исходное состояние: для write-тестов, которым нужна чистая база
    /// перед каждой проверкой (ADR 0001).
    /// </summary>
    public static async Task ResetAsync(
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

    private static IEnumerable<FileInfo> EnumerateScripts()
    {
        var schemaDirectory = new DirectoryInfo(Path.Combine(RepositoryLayout.Database.FullName, "schema"));

        var schema = new FileInfo(Path.Combine(schemaDirectory.FullName, "schema.sql"));
        if (!schema.Exists)
        {
            throw new FileNotFoundException(
                "Нет db/schema/schema.sql — схема БД не снята, см. db/README.md.", schema.FullName);
        }

        yield return schema;

        // Справочники (регионы, типы игр, статусы) лежат отдельно: pg_dump --schema-only
        // данных не содержит, а без справочников сайт не отрисует ни одной страницы.
        var referenceData = new FileInfo(Path.Combine(schemaDirectory.FullName, "reference-data.sql"));
        if (!referenceData.Exists)
        {
            throw new FileNotFoundException(
                "Нет db/schema/reference-data.sql — справочники не сняты, см. db/README.md.",
                referenceData.FullName);
        }

        yield return referenceData;
    }
}
