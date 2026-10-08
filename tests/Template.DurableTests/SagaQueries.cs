using Npgsql;

namespace Template.DurableTests;

/// <summary>Reads rows for failure diagnostics.</summary>
public sealed class SagaQueries(string connectionString)
{
    public async Task<List<string>> RowsAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(reader.GetValue(0).ToString()!);
        return rows;
    }
}
