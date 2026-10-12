using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tern.Api.Data;
using Tern.Api.Dtos;
using Tern.Api.Models;
using Tern.Api.Security;
using Tern.Api.Services;

namespace Tern.Api.Controllers;

// Everything here is limited to the employer's own company. Every query filters by it,
// so an employer can never see or change another company's jobs or applicants.
[ApiController, Route("api/employer"), Authorize(Roles = Roles.Employer)]
public class EmployerController : ControllerBase
{
    private readonly AppDbContext _db;
    public EmployerController(AppDbContext db) => _db = db;

    private async Task<Company?> MyCompanyAsync()
    {
        var userId = User.UserId();
        return await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.OwnerUserId == userId);
    }

    // ----- jobs -----

    [HttpGet("jobs")]
    public async Task<ActionResult<List<EmployerJobDto>>> MyJobs()
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var now = DateTime.UtcNow;

        // Jobs joined with the SQL view that already counted the applications per status.
        var rows = await (
            from j in _db.Jobs.AsNoTracking()
            where j.CompanyId == company.Id
            join s in _db.JobStats on j.Id equals s.JobId
            orderby j.PostedAtUtc descending
            select new EmployerJobDto(
                j.Id, j.Title, j.Location, j.IsOpen, j.ExpiresAtUtc <= now,
                j.PostedAtUtc, j.ExpiresAtUtc,
                s.Total, s.Submitted, s.InReview, s.Interview, s.Offer, s.Hired, s.Rejected))
            .ToListAsync();

        return rows;
    }

    [HttpGet("jobs/{id:int}")]
    public async Task<ActionResult<JobEditDto>> GetJob(int id)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var job = await _db.Jobs.AsNoTracking().Include(j => j.Skills)
            .FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == company.Id);
        return job == null ? NotFound() : ToEdit(job);
    }

    [HttpPost("jobs")]
    public async Task<ActionResult<JobEditDto>> Create(JobInput input)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var error = Validate(input, creating: true);
        if (error != null) return Problem(detail: error, statusCode: 400);

        var skills = await LoadSkillsAsync(input.SkillIds);
        if (skills == null) return Problem(detail: "One of the selected skills does not exist.", statusCode: 400);

        var now = DateTime.UtcNow;
        var job = new Job
        {
            CompanyId = company.Id,
            PostedAtUtc = now,
            ExpiresAtUtc = now.AddDays(input.ExpiresInDays!.Value),
            IsOpen = true,
            Skills = skills
        };
        Apply(job, input);

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, ToEdit(job));
    }

    [HttpPut("jobs/{id:int}")]
    public async Task<ActionResult<JobEditDto>> Update(int id, JobInput input)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var error = Validate(input, creating: false);
        if (error != null) return Problem(detail: error, statusCode: 400);

        var job = await _db.Jobs.Include(j => j.Skills)
            .FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == company.Id);
        if (job == null) return NotFound();

        var skills = await LoadSkillsAsync(input.SkillIds);
        if (skills == null) return Problem(detail: "One of the selected skills does not exist.", statusCode: 400);

        Apply(job, input);
        if (input.ExpiresInDays.HasValue)
            job.ExpiresAtUtc = DateTime.UtcNow.AddDays(input.ExpiresInDays.Value);

        job.Skills.Clear();
        foreach (var s in skills) job.Skills.Add(s);

        await _db.SaveChangesAsync();
        return ToEdit(job);
    }

    [HttpPost("jobs/{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == company.Id);
        if (job == null) return NotFound();

        job.IsOpen = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("jobs/{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == company.Id);
        if (job == null) return NotFound();

        if (job.ExpiresAtUtc <= DateTime.UtcNow)
            return Problem(detail: "This posting has expired. Edit it and choose a new closing date first.", statusCode: 409);

        job.IsOpen = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // A job that already has applicants can only be closed, so their history is kept.
    [HttpDelete("jobs/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == company.Id);
        if (job == null) return NotFound();

        if (await _db.Applications.AnyAsync(a => a.JobId == id))
            return Problem(detail: "This job has applications, so it can't be deleted. Close it instead.", statusCode: 409);

        _db.Jobs.Remove(job);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----- applicants -----

    [HttpGet("jobs/{id:int}/applications")]
    public async Task<ActionResult<List<ApplicantDto>>> Applicants(int id, [FromQuery] ApplicationStatus? status)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        if (!await _db.Jobs.AnyAsync(j => j.Id == id && j.CompanyId == company.Id)) return NotFound();

        var query = _db.Applications.AsNoTracking().Where(a => a.JobId == id);
        if (status.HasValue) query = query.Where(a => a.Status == status);

        var rows = await query.OrderByDescending(a => a.AppliedAtUtc).ToListAsync();
        return rows.Select(ToApplicant).ToList();
    }

    [HttpPatch("applications/{id:int}/status")]
    public async Task<ActionResult<ApplicantDto>> SetStatus(int id, StatusInput input)
    {
        var company = await MyCompanyAsync();
        if (company == null) return Forbid();

        if (!Enum.IsDefined(input.Status)) return Problem(detail: "Unknown status.", statusCode: 400);

        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == id && a.Job!.CompanyId == company.Id);
        if (application == null) return NotFound();

        if (!ApplicationFlow.CanMove(application.Status, input.Status))
            return Problem(detail: $"An application that is {application.Status} can't move to {input.Status}.", statusCode: 409);

        application.Status = input.Status;
        application.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToApplicant(application);
    }

    // ----- helpers -----

    private static string? Validate(JobInput input, bool creating)
    {
        if (creating && !input.ExpiresInDays.HasValue) return "Choose how many days the posting stays open.";
        if (input.SalaryMin.HasValue && input.SalaryMax.HasValue && input.SalaryMin > input.SalaryMax)
            return "The minimum salary can't be higher than the maximum.";
        if (!input.Currency.All(char.IsAsciiLetter)) return "Currency must be a 3 letter code such as EUR.";
        return null;
    }

    private static void Apply(Job job, JobInput input)
    {
        job.Title = input.Title.Trim();
        job.Description = input.Description.Trim();
        job.Location = input.Location.Trim();
        job.IsRemote = input.IsRemote;
        job.EmploymentType = input.EmploymentType;
        job.SalaryMin = input.SalaryMin;
        job.SalaryMax = input.SalaryMax;
        job.Currency = input.Currency.ToUpperInvariant();
    }

    // Returns null when an id doesn't exist.
    private async Task<List<Skill>?> LoadSkillsAsync(int[]? ids)
    {
        var wanted = ids?.Distinct().ToArray() ?? Array.Empty<int>();
        if (wanted.Length == 0) return new List<Skill>();

        var found = await _db.Skills.Where(s => wanted.Contains(s.Id)).ToListAsync();
        return found.Count == wanted.Length ? found : null;
    }

    private static JobEditDto ToEdit(Job j) => new(
        j.Id, j.Title, j.Description, j.Location, j.IsRemote, j.EmploymentType,
        j.SalaryMin, j.SalaryMax, j.Currency, j.ExpiresAtUtc, j.IsOpen,
        j.Skills.Select(s => s.Id).ToList());

    private static ApplicantDto ToApplicant(JobApplication a) => new(
        a.Id, a.ApplicantName, a.ApplicantEmail, a.CoverLetter, a.CvUrl,
        a.Status, a.AppliedAtUtc, ApplicationFlow.NextOptions(a.Status).ToList());
}
