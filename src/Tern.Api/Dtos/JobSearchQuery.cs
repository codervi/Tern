using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Tern.Api.Models;

namespace Tern.Api.Dtos;

// Query string of GET /api/jobs, for example:
//   /api/jobs?q=developer&location=berlin&type=FullTime&remote=true&minSalary=50000&skill=1&skill=4&sort=salary&page=2
public class JobSearchQuery
{
    [FromQuery(Name = "q"), StringLength(100)]
    public string? Q { get; set; }

    [FromQuery(Name = "location"), StringLength(100)]
    public string? Location { get; set; }

    [FromQuery(Name = "type")]
    public EmploymentType? Type { get; set; }

    // Only "true" filters. Leaving it out means remote and on-site jobs.
    [FromQuery(Name = "remote")]
    public bool? Remote { get; set; }

    [FromQuery(Name = "minSalary"), Range(0, 10_000_000)]
    public int? MinSalary { get; set; }

    // A job must have ALL of the given skills.
    [FromQuery(Name = "skill"), MaxLength(10)]
    public int[]? Skill { get; set; }

    // "newest" (default) or "salary"
    [FromQuery(Name = "sort")]
    public string? Sort { get; set; }

    [FromQuery(Name = "page"), Range(1, 10_000)]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize"), Range(1, 50)]
    public int PageSize { get; set; } = 10;

    // Words typed in the search box, at most five, each at least two letters.
    public IReadOnlyList<string> Terms() =>
        (Q ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2)
            .Distinct()
            .Take(5)
            .ToList();
}
