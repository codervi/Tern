using Tern.Api.Models;

namespace Tern.Api.Services;

// The only legal moves for an application. Keeping this in one place means the API and
// the tests agree, and the front end just asks the API what is allowed next.
//
//   Submitted -> InReview -> Interview -> Offer -> Hired
//   any step before Hired can also go to Rejected. Hired and Rejected are final.
public static class ApplicationFlow
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Moves = new()
    {
        [ApplicationStatus.Submitted] = new[] { ApplicationStatus.InReview, ApplicationStatus.Rejected },
        [ApplicationStatus.InReview] = new[] { ApplicationStatus.Interview, ApplicationStatus.Rejected },
        [ApplicationStatus.Interview] = new[] { ApplicationStatus.Offer, ApplicationStatus.Rejected },
        [ApplicationStatus.Offer] = new[] { ApplicationStatus.Hired, ApplicationStatus.Rejected },
        [ApplicationStatus.Hired] = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Rejected] = Array.Empty<ApplicationStatus>(),
    };

    public static IReadOnlyList<ApplicationStatus> NextOptions(ApplicationStatus from) => Moves[from];

    public static bool CanMove(ApplicationStatus from, ApplicationStatus to) => Moves[from].Contains(to);
}
