using JobApplicationTracker.Models;
using JobApplicationTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationTracker.Controllers;

[ApiController]
[Route("api/job-intake")]
public class JobIntakeController : ControllerBase
{
    private readonly JobPostingExtractionService _extractionService;
    private readonly JobPostingDraftRepository _draftRepository;
    private readonly JobApplicationRepository _applicationRepository;
    private readonly ILogger<JobIntakeController> _logger;

    public JobIntakeController(
        JobPostingExtractionService extractionService,
        JobPostingDraftRepository draftRepository,
        JobApplicationRepository applicationRepository,
        ILogger<JobIntakeController> logger)
    {
        _extractionService = extractionService;
        _draftRepository = draftRepository;
        _applicationRepository = applicationRepository;
        _logger = logger;
    }

    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromBody] ExtractJobPostingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Url) && string.IsNullOrWhiteSpace(request.PastedText))
        {
            return BadRequest(new { error = "Provide either a posting URL or pasted text." });
        }

        if (!string.IsNullOrWhiteSpace(request.Url) &&
            !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            return BadRequest(new { error = "URL must be absolute, including protocol (https://...)." });
        }

        if (!string.IsNullOrWhiteSpace(request.PastedText) && request.PastedText.Trim().Length < 40)
        {
            return BadRequest(new { error = "Pasted text is too short to extract fields. Provide at least 40 characters." });
        }

        try
        {
            var (rawText, extracted) = await _extractionService.ExtractAsync(request.Url, request.PastedText);
            var extractedJson = _extractionService.SerializeExtracted(extracted);
            var draft = await _draftRepository.CreateAsync(request.Url, rawText, extractedJson);

            return Ok(new
            {
                draftId = draft.Id,
                sourceUrl = draft.SourceUrl,
                rawText = draft.RawText,
                extracted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract job posting fields");
            return StatusCode(500, new { error = "Could not extract posting fields from the provided input." });
        }
    }

    [HttpPost("drafts/{draftId:int}/save")]
    public async Task<IActionResult> SaveDraft(int draftId, [FromBody] SaveDraftRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!ApplicationStatuses.All.Contains(request.Status))
        {
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", ApplicationStatuses.All)}" });
        }

        var review = request.Review;
        if (string.IsNullOrWhiteSpace(review.Company) || string.IsNullOrWhiteSpace(review.Title))
        {
            return BadRequest(new { error = "Company and title are required before saving." });
        }

        var createRequest = new CreateJobApplicationRequest
        {
            Company = review.Company.Trim(),
            Role = review.Title.Trim(),
            Status = request.Status,
            AppliedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            FollowUpDate = request.FollowUpDate,
            JobUrl = review.ApplicationLink,
            ApplicationLink = review.ApplicationLink,
            Location = string.IsNullOrWhiteSpace(review.Location) ? null : review.Location.Trim(),
            JobLevel = string.IsNullOrWhiteSpace(review.JobLevel) ? null : review.JobLevel.Trim(),
            SalaryText = string.IsNullOrWhiteSpace(review.SalaryText) ? null : review.SalaryText.Trim(),
            KeySkills = review.KeySkills,
            Notes = request.Notes,
            SourceUrl = review.SourceUrl
        };

        var created = await _applicationRepository.CreateAsync(createRequest);
        var extractedJson = _extractionService.SerializeExtracted(review);
        await _draftRepository.MarkSavedAsync(draftId, created.Id, extractedJson);

        _logger.LogInformation("Draft {DraftId} reviewed and saved as application {ApplicationId}", draftId, created.Id);
        return Ok(created);
    }
}
