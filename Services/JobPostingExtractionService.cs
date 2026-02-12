using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Services;

public class JobPostingExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobPostingExtractionService> _logger;

    public JobPostingExtractionService(HttpClient httpClient, ILogger<JobPostingExtractionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(string RawText, ExtractedJobPosting Extracted)> ExtractAsync(string? url, string? pastedText)
    {
        string rawText;
        if (!string.IsNullOrWhiteSpace(pastedText))
        {
            rawText = pastedText.Trim();
        }
        else
        {
            rawText = await LoadUrlTextAsync(url!);
        }

        var normalized = NormalizeWhitespace(rawText);
        var extracted = new ExtractedJobPosting
        {
            SourceUrl = string.IsNullOrWhiteSpace(url) ? null : url,
            Company = FindField(normalized, "company", "organization", "employer"),
            Title = FindField(normalized, "title", "job title", "role", "position"),
            Location = FindField(normalized, "location", "work location"),
            JobLevel = ExtractJobLevel(normalized),
            SalaryText = ExtractSalary(normalized),
            KeySkills = ExtractSkills(normalized),
            ApplicationLink = ExtractApplicationLink(normalized, url)
        };

        extracted.Title ??= GuessTitle(normalized);
        extracted.Company ??= GuessCompany(normalized);

        _logger.LogInformation(
            "Extracted posting fields: company={CompanyPresent}, title={TitlePresent}, skills={SkillCount}",
            !string.IsNullOrWhiteSpace(extracted.Company),
            !string.IsNullOrWhiteSpace(extracted.Title),
            extracted.KeySkills.Count);

        return (normalized, extracted);
    }

    public string SerializeExtracted(ExtractedJobPosting extracted)
    {
        return JsonSerializer.Serialize(extracted);
    }

    private async Task<string> LoadUrlTextAsync(string url)
    {
        _logger.LogInformation("Fetching job posting URL for extraction");
        var html = await _httpClient.GetStringAsync(url);
        var withoutScript = Regex.Replace(html, "<script.*?</script>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var withoutStyle = Regex.Replace(withoutScript, "<style.*?</style>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var plain = Regex.Replace(withoutStyle, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(plain);
    }

    private static string NormalizeWhitespace(string input)
    {
        return Regex.Replace(input, @"\s+", " ").Trim();
    }

    private static string? FindField(string input, params string[] labels)
    {
        foreach (var label in labels)
        {
            var pattern = @"\b" + Regex.Escape(label) + @"\s*[:\-]\s*(.{2,120}?)(?:\s{2,}|\b(?:location|salary|skills?|apply|application)\b[:\-])";
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return TrimPunctuation(match.Groups[1].Value);
            }
        }

        return null;
    }

    private static string? ExtractSalary(string input)
    {
        var match = Regex.Match(input, @"(\$[\d,]{2,}(?:\.\d+)?(?:\s*-\s*\$[\d,]{2,}(?:\.\d+)?)?(?:\s*(?:per|/)\s*(?:year|yr|hour|hr))?)", RegexOptions.IgnoreCase);
        return match.Success ? TrimPunctuation(match.Groups[1].Value) : null;
    }

    private static string? ExtractJobLevel(string input)
    {
        var levels = new[] { "intern", "junior", "mid", "senior", "staff", "principal", "lead", "manager", "director" };
        foreach (var level in levels)
        {
            if (Regex.IsMatch(input, $@"\b{level}\b", RegexOptions.IgnoreCase))
            {
                return char.ToUpper(level[0]) + level[1..];
            }
        }

        return null;
    }

    private static List<string> ExtractSkills(string input)
    {
        var catalog = new[]
        {
            "C#", ".NET", "ASP.NET", "Java", "JavaScript", "TypeScript", "React", "Node.js",
            "Python", "SQL", "PostgreSQL", "SQLite", "Docker", "Kubernetes", "AWS", "Azure",
            "GCP", "REST", "GraphQL", "Redis", "CI/CD", "Git", "Terraform", "Microservices"
        };

        return catalog
            .Where(skill => Regex.IsMatch(input, $@"\b{Regex.Escape(skill)}\b", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static string? ExtractApplicationLink(string input, string? sourceUrl)
    {
        var match = Regex.Match(input, @"(https?://[^\s)]+)");
        if (match.Success)
        {
            var url = match.Groups[1].Value.TrimEnd('.', ',', ';');
            if (url.Contains("apply", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("jobs", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("careers", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }

        return sourceUrl;
    }

    private static string? GuessTitle(string input)
    {
        var match = Regex.Match(input, @"\b(Software Engineer|Backend Engineer|Frontend Engineer|Full Stack Engineer|Data Engineer|DevOps Engineer|Product Manager|Designer)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static string? GuessCompany(string input)
    {
        var match = Regex.Match(input, @"\bat\s+([A-Z][\w&\-. ]{1,50})\b");
        return match.Success ? TrimPunctuation(match.Groups[1].Value) : null;
    }

    private static string TrimPunctuation(string value)
    {
        return value.Trim().Trim(',', '.', ';', ':');
    }
}
