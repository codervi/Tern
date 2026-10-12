namespace Tern.Api.Models;

public enum EmploymentType { FullTime, PartTime, Contract, Internship }

public class Job
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public bool IsRemote { get; set; }
    public EmploymentType EmploymentType { get; set; }

    // Yearly salary in whole currency units. Both are optional.
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public string Currency { get; set; } = "EUR";

    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    // The employer can close a posting early.
    public bool IsOpen { get; set; } = true;

    public List<Skill> Skills { get; set; } = new();
    public List<JobApplication> Applications { get; set; } = new();
}
