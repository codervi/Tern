using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tern.Api.Data;
using Tern.Api.Dtos;
using Tern.Api.Models;
using Tern.Api.Security;

namespace Tern.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;

    public AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn, ITokenService tokens, AppDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _db = db;
    }

    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (request.AccountType == AccountType.Employer && string.IsNullOrWhiteSpace(request.CompanyName))
            return Problem(detail: "Company name is required for employer accounts.", statusCode: 400);

        var role = request.AccountType == AccountType.Employer ? Roles.Employer : Roles.Candidate;
        var email = request.Email.Trim();
        var user = new AppUser { UserName = email, Email = email, DisplayName = request.DisplayName.Trim() };

        // The user, the role and the company are created together or not at all.
        await using var tx = await _db.Database.BeginTransactionAsync();

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Problem(detail: created.Errors.First().Description, statusCode: 400);

        await _users.AddToRoleAsync(user, role);

        if (role == Roles.Employer)
        {
            _db.Companies.Add(new Company { Name = request.CompanyName!.Trim(), OwnerUserId = user.Id });
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        var (token, expires) = _tokens.Create(user, role);
        return new AuthResponse(token, expires, user.Email!, user.DisplayName, role);
    }

    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());

        // Same answer for "no such user" and "wrong password", so the API doesn't reveal which emails exist.
        const string wrong = "Wrong email or password.";
        if (user == null) return Problem(title: "Sign in failed", detail: wrong, statusCode: 401);

        var result = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Problem(title: "Sign in failed",
                detail: result.IsLockedOut ? "Too many failed attempts. Try again in 15 minutes." : wrong,
                statusCode: 401);

        var role = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Candidate;
        var (token, expires) = _tokens.Create(user, role);
        return new AuthResponse(token, expires, user.Email!, user.DisplayName, role);
    }
}
