namespace Tern.Api.Models;

public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Job> Jobs { get; set; } = new();
}
