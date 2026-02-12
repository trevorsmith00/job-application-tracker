namespace JobApplicationTracker.Models;

public class ApplicationQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public DateOnly? FollowUpBefore { get; set; }
}
