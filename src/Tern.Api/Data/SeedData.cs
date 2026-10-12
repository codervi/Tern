using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tern.Api.Models;

namespace Tern.Api.Data;

public static class SeedData
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await CreateViewAsync(db);

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Candidate, Roles.Employer })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        await EnsureSkillsAsync(db);

        if (isDevelopment && !await db.Companies.AnyAsync())
            await AddDemoDataAsync(sp.GetRequiredService<UserManager<AppUser>>(), db, config["SeedUsers:DemoPassword"]);
    }

    // The view counts applications per job and status in one pass. The employer's job list
    // reads it instead of running a COUNT per row. CREATE OR ALTER makes it safe to run on every start.
    private static Task<int> CreateViewAsync(AppDbContext db) => db.Database.ExecuteSqlRawAsync(@"
CREATE OR ALTER VIEW dbo.vw_JobApplicationStats AS
SELECT
    j.Id AS JobId,
    COUNT(a.Id) AS Total,
    SUM(CASE WHEN a.Status = 'Submitted' THEN 1 ELSE 0 END) AS Submitted,
    SUM(CASE WHEN a.Status = 'InReview'  THEN 1 ELSE 0 END) AS InReview,
    SUM(CASE WHEN a.Status = 'Interview' THEN 1 ELSE 0 END) AS Interview,
    SUM(CASE WHEN a.Status = 'Offer'     THEN 1 ELSE 0 END) AS Offer,
    SUM(CASE WHEN a.Status = 'Hired'     THEN 1 ELSE 0 END) AS Hired,
    SUM(CASE WHEN a.Status = 'Rejected'  THEN 1 ELSE 0 END) AS Rejected
FROM dbo.Jobs AS j
LEFT JOIN dbo.Applications AS a ON a.JobId = j.Id
GROUP BY j.Id;");

    private static readonly string[] SkillNames =
    {
        "C#", "ASP.NET Core", "Entity Framework", "SQL Server", "JavaScript", "TypeScript", "HTML and CSS",
        "REST APIs", "Azure", "Docker", "Git", "Unit testing", "React", "Python", "Java", "Agile"
    };

    private static async Task EnsureSkillsAsync(AppDbContext db)
    {
        if (await db.Skills.AnyAsync()) return;
        db.Skills.AddRange(SkillNames.Select(n => new Skill { Name = n }));
        await db.SaveChangesAsync();
    }

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> users, string email, string name, string role, string password)
    {
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Demo user could not be created: " + result.Errors.First().Description);
        await users.AddToRoleAsync(user, role);
        return user;
    }

    // Invented companies, people and postings, for local development only.
    private static async Task AddDemoDataAsync(UserManager<AppUser> users, AppDbContext db, string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) return;

        var brightwaveOwner = await CreateUserAsync(users, "hiring@brightwave.test", "Brightwave Hiring", Roles.Employer, password);
        var harborOwner = await CreateUserAsync(users, "talent@harborworks.test", "Harborworks Talent", Roles.Employer, password);
        var alex = await CreateUserAsync(users, "alex@candidate.test", "Alex Morgan", Roles.Candidate, password);
        var maya = await CreateUserAsync(users, "maya@candidate.test", "Maya Chen", Roles.Candidate, password);
        var jonas = await CreateUserAsync(users, "jonas@candidate.test", "Jonas Weber", Roles.Candidate, password);

        var brightwave = new Company { Name = "Brightwave Software", Website = "https://brightwave.example", OwnerUserId = brightwaveOwner.Id };
        var harbor = new Company { Name = "Harborworks Logistics", Website = "https://harborworks.example", OwnerUserId = harborOwner.Id };
        db.Companies.AddRange(brightwave, harbor);
        await db.SaveChangesAsync();

        var skills = await db.Skills.ToDictionaryAsync(s => s.Name);
        var now = DateTime.UtcNow;

        Job Make(Company c, string title, string location, bool remote, EmploymentType type, int? min, int? max,
                 int daysAgo, int daysLeft, string description, params string[] skillNames) => new()
        {
            Company = c, Title = title, Location = location, IsRemote = remote, EmploymentType = type,
            SalaryMin = min, SalaryMax = max, Currency = "EUR", Description = description,
            PostedAtUtc = now.AddDays(-daysAgo), ExpiresAtUtc = now.AddDays(daysLeft),
            Skills = skillNames.Select(n => skills[n]).ToList()
        };

        var jobs = new List<Job>
        {
            Make(brightwave, "Junior .NET Developer", "Berlin, Germany", false, EmploymentType.FullTime, 42000, 52000, 2, 28,
                "Join a small product team building a booking platform. You will write ASP.NET Core APIs, fix bugs and learn code review from senior colleagues.",
                "C#", "ASP.NET Core", "SQL Server", "Git"),
            Make(brightwave, "Mid-level Full Stack Developer", "Remote, Europe", true, EmploymentType.FullTime, 58000, 72000, 4, 26,
                "Own features from database to browser. Our stack is ASP.NET Core, SQL Server and plain JavaScript. You will also mentor interns.",
                "C#", "ASP.NET Core", "Entity Framework", "SQL Server", "JavaScript", "HTML and CSS"),
            Make(brightwave, "Frontend Developer", "Amsterdam, Netherlands", false, EmploymentType.FullTime, 50000, 64000, 6, 24,
                "Build accessible interfaces for our customer portal. Strong HTML, CSS and JavaScript fundamentals matter more to us than any framework.",
                "JavaScript", "TypeScript", "HTML and CSS", "React"),
            Make(brightwave, "Backend Developer (Contract)", "Remote, Europe", true, EmploymentType.Contract, 65000, 85000, 8, 22,
                "Six month contract to move our reporting service from stored procedures to a REST API. Experience with SQL Server tuning is a plus.",
                "C#", "ASP.NET Core", "SQL Server", "REST APIs", "Unit testing"),
            Make(brightwave, "Software Engineering Intern", "Berlin, Germany", false, EmploymentType.Internship, 18000, 22000, 10, 20,
                "Three months working next to the platform team. You will pick up real tickets, write tests and present your work at the end.",
                "C#", "Git", "Agile"),
            Make(brightwave, "DevOps Engineer", "Remote, Europe", true, EmploymentType.FullTime, 70000, 90000, 12, 18,
                "Keep our pipelines and cloud environments boring and reliable. You will work with Azure, containers and the developers who ship to them.",
                "Azure", "Docker", "Git"),
            Make(brightwave, "QA Automation Engineer", "Dublin, Ireland", false, EmploymentType.PartTime, 30000, 38000, 15, 15,
                "Three days a week. Write automated tests for our APIs and web app and help developers think about edge cases before release.",
                "Unit testing", "C#", "JavaScript"),
            Make(brightwave, "Senior Python Engineer", "Remote, Europe", true, EmploymentType.FullTime, 80000, 100000, 20, 10,
                "Lead the data import service. Design the architecture, review code and keep the team honest about technical debt.",
                "Python", "SQL Server", "Docker", "REST APIs"),
            Make(harbor, "ASP.NET Developer", "Rotterdam, Netherlands", false, EmploymentType.FullTime, 48000, 60000, 1, 29,
                "Maintain and extend the internal shipment tracking system used by 300 dispatchers. Mostly ASP.NET Core MVC and SQL Server.",
                "C#", "ASP.NET Core", "SQL Server", "HTML and CSS"),
            Make(harbor, "Database Developer", "Hamburg, Germany", false, EmploymentType.FullTime, 55000, 70000, 3, 27,
                "Own our SQL Server databases: indexes, views, stored procedures and the slow queries nobody dares to touch.",
                "SQL Server", "C#", "Entity Framework"),
            Make(harbor, "Web Developer", "Remote, Europe", true, EmploymentType.PartTime, 28000, 36000, 5, 25,
                "Part-time role improving our customer tracking pages. HTML, CSS and JavaScript, no frameworks required.",
                "JavaScript", "HTML and CSS"),
            Make(harbor, "Integration Engineer", "Antwerp, Belgium", false, EmploymentType.Contract, 60000, 78000, 9, 21,
                "Connect our warehouse systems to customer APIs. You will read other people's documentation and write your own.",
                "REST APIs", "C#", "ASP.NET Core", "Azure"),
            Make(harbor, "Java Developer", "Hamburg, Germany", false, EmploymentType.FullTime, 52000, 66000, 14, 16,
                "Work on the routing engine that plans our delivery tours. Java, SQL and a lot of interesting maths.",
                "Java", "SQL Server", "Unit testing"),
            // An expired posting and a closed one, so the empty and closed states are easy to see.
            Make(harbor, "Warehouse Systems Analyst", "Rotterdam, Netherlands", false, EmploymentType.FullTime, 45000, 55000, 50, -5,
                "This position was filled last month. It stays in the list for the employer, but candidates can no longer see or apply to it.",
                "SQL Server", "Agile"),
        };
        var closed = Make(brightwave, "Technical Writer", "Remote, Europe", true, EmploymentType.PartTime, 30000, 40000, 18, 12,
            "We closed this posting early because we found someone. The employer can reopen it.", "Git", "Agile");
        closed.IsOpen = false;
        jobs.Add(closed);

        db.Jobs.AddRange(jobs);
        await db.SaveChangesAsync();

        JobApplication Apply(Job job, AppUser u, ApplicationStatus status, int daysAgo, string letter) => new()
        {
            JobId = job.Id, CandidateUserId = u.Id, ApplicantName = u.DisplayName, ApplicantEmail = u.Email!,
            Status = status, AppliedAtUtc = now.AddDays(-daysAgo), CoverLetter = letter,
            CvUrl = "https://cv.example/" + u.DisplayName.Split(' ')[0].ToLowerInvariant()
        };

        var junior = jobs[0];
        var fullStack = jobs[1];
        var aspDev = jobs[8];

        db.Applications.AddRange(
            Apply(junior, alex, ApplicationStatus.Submitted, 1, "I finished a bootcamp last spring and have built three ASP.NET Core projects since. I would love to learn from your team."),
            Apply(junior, maya, ApplicationStatus.Interview, 2, "Two years of experience with C# and SQL Server in a support role. I am ready to move into development."),
            Apply(junior, jonas, ApplicationStatus.Rejected, 2, "I enjoy backend work and recently built a small booking API with Entity Framework and tests."),
            Apply(fullStack, alex, ApplicationStatus.InReview, 3, "I like working across the whole stack and have shipped small apps with plain JavaScript on the front end."),
            Apply(fullStack, jonas, ApplicationStatus.Submitted, 1, "I have five years of web experience and want to move toward a role with more ownership of the product."),
            Apply(aspDev, maya, ApplicationStatus.Submitted, 1, "Logistics software interests me. At my current job I maintain an internal ASP.NET MVC application."));
        await db.SaveChangesAsync();
    }
}
