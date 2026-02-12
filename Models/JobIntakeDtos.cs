using System.ComponentModel.DataAnnotations;

namespace JobApplicationTracker.Models;

public class ExtractJobPostingRequest
{
    [StringLength(1000)]
    public string? Url { get; set; }

    [StringLength(50000)]
    public string? PastedText { get; set; }
}

public class ExtractedJobPosting
{
    public string? SourceUrl { get; set; }
    public string? Company { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
    public string? JobLevel { get; set; }
    public string? SalaryText { get; set; }
    public List<string> KeySkills { get; set; } = [];
    public string? ApplicationLink { get; set; }
}

public class JobPostingDraft
{
    public int Id { get; set; }
    public string? SourceUrl { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string ExtractedJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class SaveDraftRequest
{
    [Required]
    public ExtractedJobPosting Review { get; set; } = new();

    [StringLength(4000)]
    public string? Notes { get; set; }

    public DateOnly? FollowUpDate { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = ApplicationStatuses.Wishlist;
}
