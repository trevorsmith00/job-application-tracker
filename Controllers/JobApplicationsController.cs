using JobApplicationTracker.Models;
using JobApplicationTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationTracker.Controllers;

[ApiController]
[Route("api/applications")]
public class JobApplicationsController : ControllerBase
{
    private readonly JobApplicationRepository _repository;
    private readonly GhostingService _ghostingService;
    private readonly ILogger<JobApplicationsController> _logger;

    public JobApplicationsController(
        JobApplicationRepository repository,
        GhostingService ghostingService,
        ILogger<JobApplicationsController> logger)
    {
        _repository = repository;
        _ghostingService = ghostingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobApplication>>> GetAll([FromQuery] ApplicationQuery query)
    {
        var apps = await _repository.GetAllAsync(query);
        return Ok(apps);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<JobApplication>> GetById(int id)
    {
        var app = await _repository.GetByIdAsync(id);
        return app is null ? NotFound() : Ok(app);
    }

    [HttpPost]
    public async Task<ActionResult<JobApplication>> Create([FromBody] CreateJobApplicationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!IsValidStatus(request.Status))
        {
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", ApplicationStatuses.All)}" });
        }

        var urlError = ValidateOptionalUrlFields(request.JobUrl, request.ApplicationLink, request.SourceUrl);
        if (urlError is not null)
        {
            return BadRequest(new { error = urlError });
        }

        try
        {
            var created = await _repository.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Create application failed due to SQLite error.");
            return StatusCode(500, new { error = $"Database write failed: {ex.SqliteErrorCode}. {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create application failed.");
            return StatusCode(500, new { error = $"Create failed: {ex.Message}" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<JobApplication>> Update(int id, [FromBody] UpdateJobApplicationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!IsValidStatus(request.Status))
        {
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", ApplicationStatuses.All)}" });
        }

        var urlError = ValidateOptionalUrlFields(request.JobUrl, request.ApplicationLink, request.SourceUrl);
        if (urlError is not null)
        {
            return BadRequest(new { error = urlError });
        }

        try
        {
            var updated = await _repository.UpdateAsync(id, request);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Update application {ApplicationId} failed due to SQLite error.", id);
            return StatusCode(500, new { error = $"Database update failed: {ex.SqliteErrorCode}. {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update application {ApplicationId} failed.", id);
            return StatusCode(500, new { error = $"Update failed: {ex.Message}" });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _repository.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("ghosting-sweep")]
    public async Task<ActionResult<IEnumerable<GhostingSweepResult>>> SweepGhosted([FromQuery] int inactivityDays = 14)
    {
        if (inactivityDays < 1)
        {
            return BadRequest(new { error = "inactivityDays must be at least 1." });
        }

        var result = await _ghostingService.SweepAndMarkGhostedAsync(inactivityDays);
        return Ok(result);
    }

    private static bool IsValidStatus(string status) => ApplicationStatuses.All.Contains(status);

    private static string? ValidateOptionalUrlFields(params string?[] urls)
    {
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return $"Invalid URL value: {url}";
            }
        }

        return null;
    }
}
