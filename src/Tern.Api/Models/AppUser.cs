using Microsoft.AspNetCore.Identity;

namespace Tern.Api.Models;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}

public static class Roles
{
    public const string Candidate = "Candidate";
    public const string Employer = "Employer";
}
