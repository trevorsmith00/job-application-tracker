using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace JobApplicationTracker.Services;

public class MigrationService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(
        SqliteConnectionFactory connectionFactory,
        IWebHostEnvironment environment,
        ILogger<MigrationService> logger)
    {
        _connectionFactory = connectionFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task ApplyMigrationsAsync()
    {
        var migrationFolder = Path.Combine(_environment.ContentRootPath, "Database", "Migrations");
        Directory.CreateDirectory(migrationFolder);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var createTrackingTable = connection.CreateCommand();
        createTrackingTable.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                filename TEXT NOT NULL UNIQUE,
                applied_at_utc TEXT NOT NULL
            );
            """;
        await createTrackingTable.ExecuteNonQueryAsync();

        var files = Directory.GetFiles(migrationFolder, "*.sql")
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            var filename = Path.GetFileName(file);
            if (await AlreadyAppliedAsync(connection, filename))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(file);
            await ExecuteScriptAsync(connection, sql);

            var trackCommand = connection.CreateCommand();
            trackCommand.CommandText = """
                INSERT INTO schema_migrations (filename, applied_at_utc)
                VALUES ($filename, $appliedAtUtc);
                """;
            trackCommand.Parameters.AddWithValue("$filename", filename);
            trackCommand.Parameters.AddWithValue("$appliedAtUtc", DateTime.UtcNow.ToString("O"));
            await trackCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("Applied migration {MigrationFilename}", filename);
        }
    }

    private static async Task<bool> AlreadyAppliedAsync(SqliteConnection connection, string filename)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM schema_migrations WHERE filename = $filename;";
        command.Parameters.AddWithValue("$filename", filename);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync());
        return result > 0;
    }

    private static async Task ExecuteScriptAsync(SqliteConnection connection, string sqlScript)
    {
        var chunks = Regex.Split(sqlScript, @"^\s*--\s*GO\s*$", RegexOptions.Multiline);
        foreach (var chunk in chunks)
        {
            var sql = chunk.Trim();
            if (string.IsNullOrWhiteSpace(sql))
            {
                continue;
            }

            var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}
