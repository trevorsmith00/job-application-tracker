namespace JobApplicationTracker.Models;

public class GhostingSweepResult
{
    public int ApplicationId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public int DaysInactive { get; set; }
    public string FollowUpDraft { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
