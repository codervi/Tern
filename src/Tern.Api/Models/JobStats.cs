namespace Tern.Api.Models;

public class JobStats
{
    public int JobId { get; set; }
    public int Total { get; set; }
    public int Submitted { get; set; }
    public int InReview { get; set; }
    public int Interview { get; set; }
    public int Offer { get; set; }
    public int Hired { get; set; }
    public int Rejected { get; set; }
}
