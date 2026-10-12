using Tern.Api.Dtos;
using Tern.Api.Models;
using Tern.Api.Services;
using Xunit;

namespace Tern.Tests;

// The filters are plain LINQ, so they are tested here against a list in memory.
// (String matching in memory is case sensitive, so the tests use the same case.)
public class JobFiltersTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Skill Csharp = new() { Id = 1, Name = "C#" };
    private static readonly Skill Sql = new() { Id = 2, Name = "SQL Server" };
    private static readonly Skill Js = new() { Id = 3, Name = "JavaScript" };

    private static Job Make(int id, string title, string company = "Acme", string location = "Berlin",
        bool remote = false, EmploymentType type = EmploymentType.FullTime, int? min = null, int? max = null,
        int postedDaysAgo = 1, int expiresInDays = 10, bool open = true, string description = "A job description that is long enough.",
        params Skill[] skills) => new()
    {
        Id = id, Title = title, Company = new Company { Name = company }, Location = location,
        IsRemote = remote, EmploymentType = type, SalaryMin = min, SalaryMax = max,
        PostedAtUtc = Now.AddDays(-postedDaysAgo), ExpiresAtUtc = Now.AddDays(expiresInDays),
        IsOpen = open, Description = description, Skills = skills.ToList()
    };

    private static List<int> Ids(IEnumerable<Job> jobs) => jobs.Select(j => j.Id).ToList();

    private static List<int> Search(List<Job> jobs, JobSearchQuery query)
        => Ids(JobFilters.Sort(JobFilters.Apply(jobs.AsQueryable(), query, Now), query.Sort));

    [Fact]
    public void Closed_and_expired_jobs_are_hidden()
    {
        var jobs = new List<Job>
        {
            Make(1, "Open job"),
            Make(2, "Closed job", open: false),
            Make(3, "Expired job", expiresInDays: -1),
        };

        Assert.Equal(new List<int> { 1 }, Search(jobs, new JobSearchQuery()));
    }

    [Fact]
    public void Keyword_matches_title_company_or_description()
    {
        var jobs = new List<Job>
        {
            Make(1, "Backend Developer"),
            Make(2, "Designer", company: "Developer Tools Inc"),
            Make(3, "Analyst", description: "You will work with our Developer team every day."),
            Make(4, "Nurse"),
        };

        var result = Search(jobs, new JobSearchQuery { Q = "Developer" });

        Assert.Equal(new List<int> { 1, 2, 3 }, result.OrderBy(i => i).ToList());
        Assert.DoesNotContain(4, result);
    }

    [Fact]
    public void All_words_in_the_keyword_must_match()
    {
        var jobs = new List<Job>
        {
            Make(1, "Senior Backend Developer"),
            Make(2, "Senior Designer"),
            Make(3, "Backend Engineer"),
        };

        Assert.Equal(new List<int> { 1 }, Search(jobs, new JobSearchQuery { Q = "Senior Backend" }));
    }

    [Fact]
    public void Location_filter_matches_part_of_the_location()
    {
        var jobs = new List<Job> { Make(1, "A job", location: "Berlin, Germany"), Make(2, "B job", location: "Dublin, Ireland") };

        Assert.Equal(new List<int> { 1 }, Search(jobs, new JobSearchQuery { Location = "Berlin" }));
    }

    [Fact]
    public void Employment_type_filter_works()
    {
        var jobs = new List<Job>
        {
            Make(1, "Full time job", type: EmploymentType.FullTime),
            Make(2, "Contract job", type: EmploymentType.Contract),
        };

        Assert.Equal(new List<int> { 2 }, Search(jobs, new JobSearchQuery { Type = EmploymentType.Contract }));
    }

    [Fact]
    public void Remote_filter_only_applies_when_true()
    {
        var jobs = new List<Job> { Make(1, "Office job"), Make(2, "Remote job", remote: true) };

        Assert.Equal(new List<int> { 2 }, Search(jobs, new JobSearchQuery { Remote = true }));
        Assert.Equal(2, Search(jobs, new JobSearchQuery { Remote = false }).Count);
        Assert.Equal(2, Search(jobs, new JobSearchQuery()).Count);
    }

    [Fact]
    public void Minimum_salary_looks_at_the_top_of_the_range_and_skips_jobs_without_salary()
    {
        var jobs = new List<Job>
        {
            Make(1, "Low pay", min: 30000, max: 40000),
            Make(2, "Wide range", min: 40000, max: 70000),
            Make(3, "No salary"),
        };

        Assert.Equal(new List<int> { 2 }, Search(jobs, new JobSearchQuery { MinSalary = 60000 }));
    }

    [Fact]
    public void Skill_filter_requires_all_selected_skills()
    {
        var jobs = new List<Job>
        {
            Make(1, "Only C#", skills: new[] { Csharp }),
            Make(2, "C# and SQL", skills: new[] { Csharp, Sql }),
            Make(3, "Everything", skills: new[] { Csharp, Sql, Js }),
        };

        Assert.Equal(3, Search(jobs, new JobSearchQuery { Skill = new[] { 1 } }).Count);
        Assert.Equal(new List<int> { 3, 2 }, Search(jobs, new JobSearchQuery { Skill = new[] { 1, 2 } }));
        Assert.Equal(new List<int> { 3 }, Search(jobs, new JobSearchQuery { Skill = new[] { 1, 2, 3 } }));
    }

    [Fact]
    public void Repeated_skill_ids_do_not_change_the_result()
    {
        var jobs = new List<Job> { Make(1, "C# job", skills: new[] { Csharp }) };

        Assert.Equal(new List<int> { 1 }, Search(jobs, new JobSearchQuery { Skill = new[] { 1, 1, 1 } }));
    }

    [Fact]
    public void Newest_first_is_the_default_and_id_breaks_ties()
    {
        var jobs = new List<Job>
        {
            Make(1, "Old job", postedDaysAgo: 5),
            Make(2, "New job", postedDaysAgo: 1),
            Make(3, "Also new job", postedDaysAgo: 1),
        };

        Assert.Equal(new List<int> { 3, 2, 1 }, Search(jobs, new JobSearchQuery()));
    }

    [Fact]
    public void Salary_sort_puts_the_best_paid_first_and_jobs_without_salary_last()
    {
        var jobs = new List<Job>
        {
            Make(1, "Medium", max: 60000),
            Make(2, "No salary"),
            Make(3, "High", max: 90000),
            Make(4, "Only a minimum", min: 70000),
        };

        Assert.Equal(new List<int> { 3, 4, 1, 2 }, Search(jobs, new JobSearchQuery { Sort = "salary" }));
    }

    [Fact]
    public void Filters_can_be_combined()
    {
        var jobs = new List<Job>
        {
            Make(1, "Backend Developer", location: "Berlin", remote: true, max: 80000, skills: new[] { Csharp, Sql }),
            Make(2, "Backend Developer", location: "Berlin", remote: false, max: 80000, skills: new[] { Csharp, Sql }),
            Make(3, "Backend Developer", location: "Dublin", remote: true, max: 80000, skills: new[] { Csharp, Sql }),
            Make(4, "Backend Developer", location: "Berlin", remote: true, max: 50000, skills: new[] { Csharp, Sql }),
        };

        var query = new JobSearchQuery { Q = "Backend", Location = "Berlin", Remote = true, MinSalary = 70000, Skill = new[] { 1, 2 } };

        Assert.Equal(new List<int> { 1 }, Search(jobs, query));
    }
}

public class JobSearchQueryTests
{
    [Fact]
    public void Terms_are_trimmed_unique_and_at_least_two_letters_long()
    {
        var query = new JobSearchQuery { Q = "  senior   .NET  a  senior developer " };

        Assert.Equal(new List<string> { "senior", ".NET", "developer" }, query.Terms());
    }

    [Fact]
    public void Terms_are_limited_to_five()
    {
        var query = new JobSearchQuery { Q = "aa bb cc dd ee ff gg" };

        Assert.Equal(5, query.Terms().Count);
    }

    [Fact]
    public void Empty_search_has_no_terms()
    {
        Assert.Empty(new JobSearchQuery { Q = null }.Terms());
        Assert.Empty(new JobSearchQuery { Q = "   " }.Terms());
    }
}
