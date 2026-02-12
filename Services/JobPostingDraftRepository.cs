using JobApplicationTracker.Models;
using Microsoft.Data.Sqlite;

namespace JobApplicationTracker.Services;

public class JobPostingDraftRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<JobPostingDraftRepository> _logger;

    public JobPostingDraftRepository(SqliteConnectionFactory connectionFactory, ILogger<JobPostingDraftRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<JobPostingDraft> CreateAsync(string? sourceUrl, string rawText, string extractedJson)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO job_posting_drafts (source_url, raw_text, extracted_json)
            VALUES ($sourceUrl, $rawText, $extractedJson);
            SELECT id, source_url, raw_text, extracted_json, created_at_utc
            FROM job_posting_drafts
            WHERE id = last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$sourceUrl", (object?)sourceUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$rawText", rawText);
        command.Parameters.AddWithValue("$extractedJson", extractedJson);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var draft = new JobPostingDraft
        {
            Id = reader.GetInt32(0),
            SourceUrl = reader.IsDBNull(1) ? null : reader.GetString(1),
            RawText = reader.GetString(2),
            ExtractedJson = reader.GetString(3),
            CreatedAtUtc = DateTime.Parse(reader.GetString(4)).ToUniversalTime()
        };

        _logger.LogInformation("Created job posting draft {DraftId}", draft.Id);
        return draft;
    }

    public async Task<bool> MarkSavedAsync(int draftId, int applicationId, string extractedJson)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE job_posting_drafts
            SET saved_application_id = $applicationId,
                extracted_json = $extractedJson,
                saved_at_utc = CURRENT_TIMESTAMP
            WHERE id = $draftId;
            """;
        command.Parameters.AddWithValue("$draftId", draftId);
        command.Parameters.AddWithValue("$applicationId", applicationId);
        command.Parameters.AddWithValue("$extractedJson", extractedJson);
        var updated = await command.ExecuteNonQueryAsync() > 0;

        if (updated)
        {
            _logger.LogInformation("Marked draft {DraftId} as saved to application {ApplicationId}", draftId, applicationId);
        }

        return updated;
    }
}
