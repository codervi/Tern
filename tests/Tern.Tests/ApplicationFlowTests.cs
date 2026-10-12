using Tern.Api.Models;
using Tern.Api.Services;
using Xunit;

namespace Tern.Tests;

public class ApplicationFlowTests
{
    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.InReview)]
    [InlineData(ApplicationStatus.InReview, ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Offer, ApplicationStatus.Hired)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.InReview, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Offer, ApplicationStatus.Rejected)]
    public void Allowed_moves_are_accepted(ApplicationStatus from, ApplicationStatus to)
    {
        Assert.True(ApplicationFlow.CanMove(from, to));
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Interview)]  // can't skip a step
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Hired)]
    [InlineData(ApplicationStatus.InReview, ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.InReview)]   // can't go back
    [InlineData(ApplicationStatus.Offer, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Submitted)]  // can't stay
    public void Other_moves_are_refused(ApplicationStatus from, ApplicationStatus to)
    {
        Assert.False(ApplicationFlow.CanMove(from, to));
    }

    [Theory]
    [InlineData(ApplicationStatus.Hired)]
    [InlineData(ApplicationStatus.Rejected)]
    public void Final_statuses_have_no_next_step(ApplicationStatus status)
    {
        Assert.Empty(ApplicationFlow.NextOptions(status));
    }

    [Fact]
    public void Every_status_is_covered()
    {
        foreach (var status in Enum.GetValues<ApplicationStatus>())
            Assert.NotNull(ApplicationFlow.NextOptions(status));
    }
}
