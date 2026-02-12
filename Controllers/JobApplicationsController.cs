using JobApplicationTracker.Models;
using JobApplicationTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationTracker.Controllers;

[ApiController]
[Route("api/applications")]
public class JobApplicationsController : ControllerBase
{
    private readonly JobApplicationRepository _repository;
    private readonly GhostingService _ghostingService;

    public JobApplicationsController(JobApplicationRepository repository, GhostingService ghostingService)
    {
        _repository = repository;
        _ghostingService = ghostingService;
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

        var created = await _repository.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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

        var updated = await _repository.UpdateAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
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
