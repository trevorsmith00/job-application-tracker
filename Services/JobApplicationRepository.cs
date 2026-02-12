using System.Text;
using System.Text.Json;
using JobApplicationTracker.Models;
using Microsoft.Data.Sqlite;

namespace JobApplicationTracker.Services;

public class JobApplicationRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<JobApplicationRepository> _logger;

    public JobApplicationRepository(SqliteConnectionFactory connectionFactory, ILogger<JobApplicationRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<List<JobApplication>> GetAllAsync(ApplicationQuery query)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var sql = new StringBuilder("""
            SELECT
                id,
                company,
                role,
                status,
                applied_on,
                follow_up_date,
                job_url,
                application_link,
                location,
                job_level,
                salary_text,
                key_skills_json,
                source_url,
                notes,
                created_at_utc,
                updated_at_utc
            FROM job_applications
            WHERE 1=1
            """);

        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            sql.Append(" AND status = $status");
            parameters.Add(new SqliteParameter("$status", query.Status));
        }

        if (query.FollowUpBefore is not null)
        {
            sql.Append(" AND follow_up_date IS NOT NULL AND date(follow_up_date) <= date($followUpBefore)");
            parameters.Add(new SqliteParameter("$followUpBefore", query.FollowUpBefore.Value.ToString("yyyy-MM-dd")));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sql.Append("""
                 AND (
                     lower(company) LIKE $search OR
                     lower(role) LIKE $search OR
                     lower(COALESCE(location, '')) LIKE $search OR
                     lower(COALESCE(notes, '')) LIKE $search
                 )
                """);
            parameters.Add(new SqliteParameter("$search", $"%{query.Search.Trim().ToLowerInvariant()}%"));
        }

        sql.Append("""
             ORDER BY
                CASE status
                    WHEN 'Wishlist' THEN 1
                    WHEN 'Applied' THEN 2
                    WHEN 'Interviewing' THEN 3
                    WHEN 'Offer' THEN 4
                    WHEN 'Rejected' THEN 5
                    WHEN 'Ghosted' THEN 6
                    WHEN 'Closed' THEN 7
                    ELSE 99
                END,
                date(applied_on) DESC;
            """);

        var command = connection.CreateCommand();
        command.CommandText = sql.ToString();
        command.Parameters.AddRange(parameters);

        var output = new List<JobApplication>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            output.Add(Map(reader));
        }

        _logger.LogInformation("Fetched {Count} job applications with filters", output.Count);
        return output;
    }

    public async Task<JobApplication?> GetByIdAsync(int id)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                id,
                company,
                role,
                status,
                applied_on,
                follow_up_date,
                job_url,
                application_link,
                location,
                job_level,
                salary_text,
                key_skills_json,
                source_url,
                notes,
                created_at_utc,
                updated_at_utc
            FROM job_applications
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<JobApplication> CreateAsync(CreateJobApplicationRequest request)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO job_applications (
                company, role, status, applied_on, follow_up_date, job_url, application_link, location,
                job_level, salary_text, key_skills_json, source_url, notes
            ) VALUES (
                $company, $role, $status, $appliedOn, $followUpDate, $jobUrl, $applicationLink, $location,
                $jobLevel, $salaryText, $keySkillsJson, $sourceUrl, $notes
            );
            SELECT
                id,
                company,
                role,
                status,
                applied_on,
                follow_up_date,
                job_url,
                application_link,
                location,
                job_level,
                salary_text,
                key_skills_json,
                source_url,
                notes,
                created_at_utc,
                updated_at_utc
            FROM job_applications
            WHERE id = last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$company", request.Company.Trim());
        command.Parameters.AddWithValue("$role", request.Role.Trim());
        command.Parameters.AddWithValue("$status", request.Status);
        command.Parameters.AddWithValue("$appliedOn", request.AppliedOn.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$followUpDate", (object?)request.FollowUpDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$jobUrl", (object?)request.JobUrl?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$applicationLink", (object?)request.ApplicationLink?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$location", (object?)request.Location?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$jobLevel", (object?)request.JobLevel?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$salaryText", (object?)request.SalaryText?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$keySkillsJson", SerializeSkills(request.KeySkills));
        command.Parameters.AddWithValue("$sourceUrl", (object?)request.SourceUrl?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)request.Notes?.Trim() ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var created = Map(reader);
        _logger.LogInformation("Created job application {ApplicationId} for {Company}", created.Id, created.Company);
        return created;
    }

    public async Task<JobApplication?> UpdateAsync(int id, UpdateJobApplicationRequest request)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE job_applications
            SET
                company = $company,
                role = $role,
                status = $status,
                applied_on = $appliedOn,
                follow_up_date = $followUpDate,
                job_url = $jobUrl,
                application_link = $applicationLink,
                location = $location,
                job_level = $jobLevel,
                salary_text = $salaryText,
                key_skills_json = $keySkillsJson,
                source_url = $sourceUrl,
                notes = $notes,
                updated_at_utc = CURRENT_TIMESTAMP
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$company", request.Company.Trim());
        command.Parameters.AddWithValue("$role", request.Role.Trim());
        command.Parameters.AddWithValue("$status", request.Status);
        command.Parameters.AddWithValue("$appliedOn", request.AppliedOn.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$followUpDate", (object?)request.FollowUpDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$jobUrl", (object?)request.JobUrl?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$applicationLink", (object?)request.ApplicationLink?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$location", (object?)request.Location?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$jobLevel", (object?)request.JobLevel?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$salaryText", (object?)request.SalaryText?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$keySkillsJson", SerializeSkills(request.KeySkills));
        command.Parameters.AddWithValue("$sourceUrl", (object?)request.SourceUrl?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)request.Notes?.Trim() ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync();
        if (affected == 0)
        {
            return null;
        }

        _logger.LogInformation("Updated job application {ApplicationId}", id);
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM job_applications WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var deleted = await command.ExecuteNonQueryAsync() > 0;
        if (deleted)
        {
            _logger.LogInformation("Deleted job application {ApplicationId}", id);
        }

        return deleted;
    }

    private static JobApplication Map(SqliteDataReader reader)
    {
        return new JobApplication
        {
            Id = reader.GetInt32(0),
            Company = reader.GetString(1),
            Role = reader.GetString(2),
            Status = reader.GetString(3),
            AppliedOn = DateOnly.Parse(reader.GetString(4)),
            FollowUpDate = reader.IsDBNull(5) ? null : DateOnly.Parse(reader.GetString(5)),
            JobUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            ApplicationLink = reader.IsDBNull(7) ? null : reader.GetString(7),
            Location = reader.IsDBNull(8) ? null : reader.GetString(8),
            JobLevel = reader.IsDBNull(9) ? null : reader.GetString(9),
            SalaryText = reader.IsDBNull(10) ? null : reader.GetString(10),
            KeySkills = ParseSkills(reader.IsDBNull(11) ? null : reader.GetString(11)),
            SourceUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
            Notes = reader.IsDBNull(13) ? null : reader.GetString(13),
            CreatedAtUtc = ParseUtcOrNow(reader, 14),
            UpdatedAtUtc = ParseUtcOrNow(reader, 15)
        };
    }

    private static string SerializeSkills(List<string>? skills)
    {
        var list = skills?
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(static s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        return JsonSerializer.Serialize(list);
    }

    private static List<string> ParseSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static DateTime ParseUtcOrNow(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return DateTime.UtcNow;
        }

        var raw = reader.GetString(ordinal);
        return DateTime.TryParse(raw, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;
    }
}
