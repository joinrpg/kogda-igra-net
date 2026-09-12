using Npgsql;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Накатывает схему kogda-igra на пустую БД: сначала db/schema/schema.sql,
/// затем db/migrations/*.sql в алфавитном порядке — ровно так же, как это
/// происходит на живой базе PHP-версии.
/// </summary>
public static class DatabaseSchema
{
    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var script in EnumerateScripts())
        {
            var sql = await File.ReadAllTextAsync(script.FullName, cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection);
            try
            {
                _ = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException e)
            {
                throw new InvalidOperationException($"Не применился {script.Name}: {e.MessageText}", e);
            }
        }
    }

    /// <summary>
    /// Возвращает БД в исходное засеянное состояние: для write-тестов, которым нужна
    /// чистая база перед каждой проверкой (ADR 0001).
    /// </summary>
    public static async Task ResetAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var drop = new NpgsqlCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", connection);
            _ = await drop.ExecuteNonQueryAsync(cancellationToken);
        }

        await ApplyAsync(connectionString, cancellationToken);
    }

    private static IEnumerable<FileInfo> EnumerateScripts()
    {
        var schema = new FileInfo(Path.Combine(RepositoryLayout.Database.FullName, "schema", "schema.sql"));
        if (!schema.Exists)
        {
            throw new FileNotFoundException(
                "Нет db/schema/schema.sql — схема БД не заведена, см. db/README.md.", schema.FullName);
        }

        yield return schema;

        // Справочники (регионы, типы игр, статусы) лежат отдельно, когда схема снята с живой
        // базы: pg_dump --schema-only данных не содержит, а без справочников сайт не отрисуется.
        var referenceData = new FileInfo(
            Path.Combine(RepositoryLayout.Database.FullName, "schema", "reference-data.sql"));
        if (referenceData.Exists)
        {
            yield return referenceData;
        }

        // Каталог миграций существует, только пока схема собрана из исторических дампов.
        // Снимок живой базы уже содержит их все — тогда каталога просто нет, см. db/README.md.
        var migrations = new DirectoryInfo(Path.Combine(RepositoryLayout.Database.FullName, "migrations"));
        if (!migrations.Exists)
        {
            yield break;
        }

        foreach (var migration in migrations.GetFiles("*.sql").OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            yield return migration;
        }
    }
}
