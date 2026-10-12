using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tern.Api.Data;
using Tern.Api.Dtos;
using Tern.Api.Security;
using Tern.Api.Services;

namespace Tern.Api.Controllers;

// Anyone can search and read jobs, no account needed.
[ApiController, Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;
    public JobsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<JobListItemDto>>> Search([FromQuery] JobSearchQuery query)
    {
        var filtered = JobFilters.Apply(_db.Jobs.AsNoTracking(), query, DateTime.UtcNow);

        var total = await filtered.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)query.PageSize));
        var page = Math.Clamp(query.Page, 1, pages);

        var items = await JobFilters.Sort(filtered, query.Sort)
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(j => new JobListItemDto(
                j.Id, j.Title, j.Company!.Name, j.Location, j.IsRemote, j.EmploymentType,
                j.SalaryMin, j.SalaryMax, j.Currency, j.PostedAtUtc,
                j.Skills.OrderBy(s => s.Name).Select(s => s.Name).ToList()))
            .ToListAsync();

        return new PagedResult<JobListItemDto>(items, page, query.PageSize, pages, total);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<JobDetailDto>> Get(int id)
    {
        var job = await _db.Jobs.AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
            .FirstOrDefaultAsync(j => j.Id == id);
        if (job == null) return NotFound();

        // The token is optional here. A signed-in candidate also learns whether they already applied.
        var userId = User.UserId();
        var hasApplied = userId != null &&
            await _db.Applications.AnyAsync(a => a.JobId == id && a.CandidateUserId == userId);

        return new JobDetailDto(
            job.Id, job.Title, job.Description, job.Location, job.IsRemote, job.EmploymentType,
            job.SalaryMin, job.SalaryMax, job.Currency, job.PostedAtUtc, job.ExpiresAtUtc,
            job.IsOpen && job.ExpiresAtUtc > DateTime.UtcNow,
            new CompanyDto(job.Company!.Id, job.Company.Name, job.Company.Website),
            job.Skills.Select(s => s.Name).OrderBy(n => n).ToList(),
            hasApplied);
    }
}
