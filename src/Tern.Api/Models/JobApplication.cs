namespace Tern.Api.Models;

public enum ApplicationStatus { Submitted, InReview, Interview, Offer, Hired, Rejected }

public class JobApplication
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job? Job { get; set; }

    // No foreign key to the user table on purpose, see AppDbContext.
    public string CandidateUserId { get; set; } = "";

    // Copied at the moment of applying, so the employer sees what the candidate sent
    // even if the profile changes later.
    public string ApplicantName { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";

    public string CoverLetter { get; set; } = "";
    public string? CvUrl { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
