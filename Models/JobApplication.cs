namespace JobApplicationTracker.Models;

public static class ApplicationStatuses
{
    public const string Wishlist = "Wishlist";
    public const string Applied = "Applied";
    public const string Interviewing = "Interviewing";
    public const string Offer = "Offer";
    public const string Rejected = "Rejected";
    public const string Ghosted = "Ghosted";
    public const string Closed = "Closed";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Wishlist,
        Applied,
        Interviewing,
        Offer,
        Rejected,
        Ghosted,
        Closed
    };
}

public class JobApplication
{
    public int Id { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = ApplicationStatuses.Applied;
    public DateOnly AppliedOn { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public string? JobUrl { get; set; }
    public string? ApplicationLink { get; set; }
    public string? Location { get; set; }
    public string? JobLevel { get; set; }
    public string? SalaryText { get; set; }
    public List<string> KeySkills { get; set; } = [];
    public string? SourceUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
