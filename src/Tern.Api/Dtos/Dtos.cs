using System.ComponentModel.DataAnnotations;
using Tern.Api.Models;

namespace Tern.Api.Dtos;

public enum AccountType { Candidate, Employer }

public record RegisterRequest(
    [Required, EmailAddress, StringLength(120)] string Email,
    [Required, StringLength(100, MinimumLength = 10)] string Password,
    [Required, StringLength(100)] string DisplayName,
    AccountType AccountType,
    [StringLength(120)] string? CompanyName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string Email, string DisplayName, string Role);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalPages, int TotalCount);

public record SkillDto(int Id, string Name);

public record CompanyDto(int Id, string Name, string? Website);

public record JobListItemDto(
    int Id, string Title, string CompanyName, string Location, bool IsRemote,
    EmploymentType EmploymentType, int? SalaryMin, int? SalaryMax, string Currency,
    DateTime PostedAtUtc, List<string> Skills);

public record JobDetailDto(
    int Id, string Title, string Description, string Location, bool IsRemote,
    EmploymentType EmploymentType, int? SalaryMin, int? SalaryMax, string Currency,
    DateTime PostedAtUtc, DateTime ExpiresAtUtc, bool AcceptingApplications,
    CompanyDto Company, List<string> Skills, bool HasApplied);

public record JobInput(
    [Required, StringLength(150, MinimumLength = 5)] string Title,
    [Required, StringLength(4000, MinimumLength = 30)] string Description,
    [Required, StringLength(100)] string Location,
    bool IsRemote,
    EmploymentType EmploymentType,
    [Range(0, 10_000_000)] int? SalaryMin,
    [Range(0, 10_000_000)] int? SalaryMax,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Range(1, 120)] int? ExpiresInDays,
    [MaxLength(10)] int[]? SkillIds);

public record EmployerJobDto(
    int Id, string Title, string Location, bool IsOpen, bool IsExpired,
    DateTime PostedAtUtc, DateTime ExpiresAtUtc,
    int Total, int Submitted, int InReview, int Interview, int Offer, int Hired, int Rejected);

public record JobEditDto(
    int Id, string Title, string Description, string Location, bool IsRemote,
    EmploymentType EmploymentType, int? SalaryMin, int? SalaryMax, string Currency,
    DateTime ExpiresAtUtc, bool IsOpen, List<int> SkillIds);

public record ApplyInput(
    [Range(1, int.MaxValue)] int JobId,
    [Required, StringLength(3000, MinimumLength = 20)] string CoverLetter,
    [StringLength(300)] string? CvUrl);

public record MyApplicationDto(
    int Id, int JobId, string JobTitle, string CompanyName,
    ApplicationStatus Status, DateTime AppliedAtUtc, bool CanWithdraw);

public record ApplicantDto(
    int Id, string ApplicantName, string ApplicantEmail, string CoverLetter, string? CvUrl,
    ApplicationStatus Status, DateTime AppliedAtUtc, List<ApplicationStatus> AllowedNext);

public record StatusInput(ApplicationStatus Status);
