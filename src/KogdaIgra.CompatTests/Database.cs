using Npgsql;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Чтение состояния БД для ассертов write-тестов: по ADR 0001 такой тест проверяет
/// и HTTP-ответ, и то, что реально легло в таблицы.
/// </summary>
public static class Database
{
    public static async Task<T?> ScalarAsync<T>(
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>Количество строк, удовлетворяющих условию.</summary>
    public static async Task<long> CountAsync(
        string connectionString,
        string table,
        string where,
        CancellationToken cancellationToken = default)
        => await ScalarAsync<long>(connectionString, $"SELECT count(*) FROM {table} WHERE {where}", cancellationToken);
}
