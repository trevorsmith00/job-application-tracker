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

        await EnsureJobApplicationsTableShapeAsync(connection);
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

    private async Task EnsureJobApplicationsTableShapeAsync(SqliteConnection connection)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(job_applications);";

        await using (var reader = await pragma.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (existingColumns.Count == 0)
        {
            return;
        }

        var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application_link"] = "TEXT NULL",
            ["job_level"] = "TEXT NULL",
            ["salary_text"] = "TEXT NULL",
            ["key_skills_json"] = "TEXT NULL",
            ["source_url"] = "TEXT NULL",
            ["follow_up_date"] = "TEXT NULL",
            ["job_url"] = "TEXT NULL",
            ["location"] = "TEXT NULL",
            ["notes"] = "TEXT NULL",
            ["created_at_utc"] = "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP",
            ["updated_at_utc"] = "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP"
        };

        foreach (var kvp in requiredColumns)
        {
            if (existingColumns.Contains(kvp.Key))
            {
                continue;
            }

            var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE job_applications ADD COLUMN {kvp.Key} {kvp.Value};";
            await alter.ExecuteNonQueryAsync();
            _logger.LogWarning("Schema drift repaired: added missing column {ColumnName} to job_applications.", kvp.Key);
        }
    }
}
