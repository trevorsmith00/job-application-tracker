using System.ComponentModel.DataAnnotations;

namespace JobApplicationTracker.Models;

public class CreateJobApplicationRequest
{
    [Required]
    [StringLength(200)]
    public string Company { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Role { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = ApplicationStatuses.Applied;

    [Required]
    public DateOnly AppliedOn { get; set; }

    public DateOnly? FollowUpDate { get; set; }

    [StringLength(500)]
    public string? JobUrl { get; set; }

    [StringLength(500)]
    public string? ApplicationLink { get; set; }

    [StringLength(150)]
    public string? Location { get; set; }

    [StringLength(80)]
    public string? JobLevel { get; set; }

    [StringLength(120)]
    public string? SalaryText { get; set; }

    public List<string>? KeySkills { get; set; }

    [StringLength(500)]
    public string? SourceUrl { get; set; }

    [StringLength(4000)]
    public string? Notes { get; set; }
}

public class UpdateJobApplicationRequest : CreateJobApplicationRequest;
