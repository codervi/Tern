using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tern.Api.Data;
using Tern.Api.Dtos;
using Tern.Api.Models;
using Tern.Api.Security;

namespace Tern.Api.Controllers;

[ApiController, Route("api/applications"), Authorize(Roles = Roles.Candidate)]
public class ApplicationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public ApplicationsController(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<List<MyApplicationDto>> Mine()
    {
        var userId = User.UserId();

        return await _db.Applications.AsNoTracking()
            .Where(a => a.CandidateUserId == userId)
            .OrderByDescending(a => a.AppliedAtUtc)
            .Select(a => new MyApplicationDto(
                a.Id, a.JobId, a.Job!.Title, a.Job.Company!.Name,
                a.Status, a.AppliedAtUtc, a.Status == ApplicationStatus.Submitted))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Apply(ApplyInput input)
    {
        var userId = User.UserId()!;

        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == input.JobId);
        if (job == null) return Problem(detail: "Job not found.", statusCode: 404);

        if (!job.IsOpen || job.ExpiresAtUtc <= DateTime.UtcNow)
            return Problem(detail: "This position is no longer accepting applications.", statusCode: 409);

        if (await _db.Applications.AnyAsync(a => a.JobId == job.Id && a.CandidateUserId == userId))
            return Problem(detail: "You have already applied to this job.", statusCode: 409);

        string? cvUrl = null;
        if (!string.IsNullOrWhiteSpace(input.CvUrl))
        {
            var ok = Uri.TryCreate(input.CvUrl.Trim(), UriKind.Absolute, out var uri)
                     && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
            if (!ok) return Problem(detail: "The CV link must start with http:// or https://.", statusCode: 400);
            cvUrl = uri!.ToString();
        }

        var user = await _users.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var application = new JobApplication
        {
            JobId = job.Id,
            CandidateUserId = userId,
            ApplicantName = user.DisplayName,
            ApplicantEmail = user.Email ?? "",
            CoverLetter = input.CoverLetter.Trim(),
            CvUrl = cvUrl
        };

        _db.Applications.Add(application);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // two clicks at the same moment: the unique index (JobId, CandidateUserId) stopped the second one
            return Problem(detail: "You have already applied to this job.", statusCode: 409);
        }

        return Created($"/api/applications/{application.Id}", new { application.Id });
    }

    // Only possible while the employer hasn't started looking at it.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Withdraw(int id)
    {
        var userId = User.UserId();
        var application = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id && a.CandidateUserId == userId);
        if (application == null) return NotFound();

        if (application.Status != ApplicationStatus.Submitted)
            return Problem(detail: "The employer has already started reviewing this application.", statusCode: 409);

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
