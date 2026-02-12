using JobApplicationTracker.Models;
using Microsoft.Data.Sqlite;

namespace JobApplicationTracker.Services;

public class GhostingService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<GhostingService> _logger;

    public GhostingService(SqliteConnectionFactory connectionFactory, ILogger<GhostingService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<List<GhostingSweepResult>> SweepAndMarkGhostedAsync(int inactivityDays)
    {
        if (inactivityDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(inactivityDays), "inactivityDays must be at least 1.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        var candidates = new List<(int Id, string Company, string Role, string Status, int DaysInactive)>();

        var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = """
            SELECT
                id,
                company,
                role,
                status,
                CAST(julianday('now') - julianday(COALESCE(updated_at_utc, created_at_utc)) AS INTEGER) AS days_inactive
            FROM job_applications
            WHERE lower(status) IN ('applied', 'interviewing', 'interviewed')
              AND CAST(julianday('now') - julianday(COALESCE(updated_at_utc, created_at_utc)) AS INTEGER) >= $inactivityDays;
            """;
        select.Parameters.AddWithValue("$inactivityDays", inactivityDays);

        await using (var reader = await select.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                candidates.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4)));
            }
        }

        var results = new List<GhostingSweepResult>();
        foreach (var candidate in candidates)
        {
            var recommendation = GetRecommendation(candidate.Status, candidate.DaysInactive);
            var draft = BuildFollowUpDraft(candidate.Company, candidate.Role);

            var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE job_applications
                SET status = $ghostedStatus,
                    updated_at_utc = CURRENT_TIMESTAMP
                WHERE id = $id;
                """;
            update.Parameters.AddWithValue("$ghostedStatus", ApplicationStatuses.Ghosted);
            update.Parameters.AddWithValue("$id", candidate.Id);
            await update.ExecuteNonQueryAsync();

            var insertDraft = connection.CreateCommand();
            insertDraft.Transaction = transaction;
            insertDraft.CommandText = """
                INSERT INTO application_follow_up_drafts (
                    application_id,
                    days_inactive,
                    draft_text,
                    recommendation
                ) VALUES (
                    $applicationId,
                    $daysInactive,
                    $draftText,
                    $recommendation
                );
                """;
            insertDraft.Parameters.AddWithValue("$applicationId", candidate.Id);
            insertDraft.Parameters.AddWithValue("$daysInactive", candidate.DaysInactive);
            insertDraft.Parameters.AddWithValue("$draftText", draft);
            insertDraft.Parameters.AddWithValue("$recommendation", recommendation);
            await insertDraft.ExecuteNonQueryAsync();

            results.Add(new GhostingSweepResult
            {
                ApplicationId = candidate.Id,
                Company = candidate.Company,
                Role = candidate.Role,
                PreviousStatus = candidate.Status,
                DaysInactive = candidate.DaysInactive,
                FollowUpDraft = draft,
                Recommendation = recommendation
            });
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Ghosting sweep completed: {Count} applications moved to Ghosted.", results.Count);
        return results;
    }

    private static string BuildFollowUpDraft(string company, string role)
    {
        return
            $"Hi {company} hiring team,\n\n" +
            $"I hope you are doing well. I am following up on my application for the {role} role. " +
            "I remain very interested in the position and would appreciate any update on timeline or next steps.\n\n" +
            "Thank you for your time and consideration.\n\n" +
            "Best regards,";
    }

    private static string GetRecommendation(string previousStatus, int daysInactive)
    {
        if (previousStatus.Equals("Interviewing", StringComparison.OrdinalIgnoreCase) ||
            previousStatus.Equals("Interviewed", StringComparison.OrdinalIgnoreCase))
        {
            return "Find referrals inside the company before re-applying.";
        }

        if (daysInactive >= 30)
        {
            return "Consider re-applying if the role was re-posted, and try to add a referral.";
        }

        return "Prioritize finding a referral first; re-apply only if the posting is refreshed.";
    }
}
