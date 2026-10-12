using Tern.Api.Dtos;
using Tern.Api.Models;

namespace Tern.Api.Services;

// The search is built from small pieces that are added only when the user asked for them,
// so EF Core produces one SQL statement with just the WHERE parts that are needed.
// Only plain LINQ is used (no EF-specific functions), so the same code is unit-tested in memory.
public static class JobFilters
{
    public static IQueryable<Job> Apply(IQueryable<Job> jobs, JobSearchQuery s, DateTime nowUtc)
    {
        // Public search only shows open, not expired jobs.
        jobs = jobs.Where(j => j.IsOpen && j.ExpiresAtUtc > nowUtc);

        // Every word has to appear in the title, the company name or the description.
        foreach (var term in s.Terms())
        {
            var t = term;
            jobs = jobs.Where(j => j.Title.Contains(t) || j.Company!.Name.Contains(t) || j.Description.Contains(t));
        }

        if (!string.IsNullOrWhiteSpace(s.Location))
        {
            var location = s.Location.Trim();
            jobs = jobs.Where(j => j.Location.Contains(location));
        }

        if (s.Type.HasValue)
        {
            var type = s.Type.Value;
            jobs = jobs.Where(j => j.EmploymentType == type);
        }

        if (s.Remote == true)
            jobs = jobs.Where(j => j.IsRemote);

        // "At least X": the top of the salary range must reach X. Jobs without a salary are left out.
        if (s.MinSalary.HasValue)
        {
            var min = s.MinSalary.Value;
            jobs = jobs.Where(j => j.SalaryMax != null && j.SalaryMax >= min);
        }

        // Counting the matching skills and comparing with the number asked for means
        // "has all of them", and it becomes one correlated COUNT in SQL.
        var skillIds = s.Skill?.Distinct().ToArray();
        if (skillIds is { Length: > 0 })
            jobs = jobs.Where(j => j.Skills.Count(sk => skillIds.Contains(sk.Id)) == skillIds.Length);

        return jobs;
    }

    // Id is the tie-breaker, so two jobs posted at the same moment never swap places between pages.
    public static IOrderedQueryable<Job> Sort(IQueryable<Job> jobs, string? sort) =>
        sort == "salary"
            ? jobs.OrderByDescending(j => j.SalaryMax ?? j.SalaryMin ?? 0)
                  .ThenByDescending(j => j.PostedAtUtc).ThenByDescending(j => j.Id)
            : jobs.OrderByDescending(j => j.PostedAtUtc).ThenByDescending(j => j.Id);
}
