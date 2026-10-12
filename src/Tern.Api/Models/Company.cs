namespace Tern.Api.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Website { get; set; }

    // Id of the employer account that owns this company.
    public string OwnerUserId { get; set; } = "";

    public List<Job> Jobs { get; set; } = new();
}
