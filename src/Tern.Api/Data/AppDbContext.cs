using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tern.Api.Models;

namespace Tern.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<JobStats> JobStats => Set<JobStats>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>().Property(u => u.DisplayName).HasMaxLength(100);

        b.Entity<Company>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(120);
            e.Property(c => c.Website).HasMaxLength(200);
            e.Property(c => c.OwnerUserId).HasMaxLength(450);
            e.HasIndex(c => c.OwnerUserId).IsUnique();   // one company per employer account
            e.HasOne<AppUser>().WithMany()
                .HasForeignKey(c => c.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Skill>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(50);
            e.HasIndex(s => s.Name).IsUnique();
        });

        b.Entity<Job>(e =>
        {
            e.Property(j => j.Title).HasMaxLength(150);
            e.Property(j => j.Description).HasMaxLength(4000);
            e.Property(j => j.Location).HasMaxLength(100);
            e.Property(j => j.Currency).HasMaxLength(3);
            e.Property(j => j.EmploymentType).HasConversion<string>().HasMaxLength(20);

            e.HasOne(j => j.Company).WithMany(c => c.Jobs)
                .HasForeignKey(j => j.CompanyId).OnDelete(DeleteBehavior.Cascade);

            // The public search always asks for "open and not expired, newest first".
            // This index serves exactly that query, so the database reads the newest
            // rows in order instead of sorting the whole table.
            e.HasIndex(j => new { j.IsOpen, j.PostedAtUtc, j.ExpiresAtUtc })
                .IsDescending(false, true, false);

            e.HasIndex(j => j.CompanyId);
            e.HasIndex(j => j.Location);

            // Many-to-many: EF creates the JobSkill join table.
            e.HasMany(j => j.Skills).WithMany(s => s.Jobs);
        });

        b.Entity<JobApplication>(e =>
        {
            e.ToTable("Applications");
            e.Property(a => a.CandidateUserId).HasMaxLength(450);
            e.Property(a => a.ApplicantName).HasMaxLength(100);
            e.Property(a => a.ApplicantEmail).HasMaxLength(256);
            e.Property(a => a.CoverLetter).HasMaxLength(3000);
            e.Property(a => a.CvUrl).HasMaxLength(300);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(a => a.Job).WithMany(j => j.Applications)
                .HasForeignKey(a => a.JobId).OnDelete(DeleteBehavior.Cascade);

            // One application per person per job, enforced by the database.
            e.HasIndex(a => new { a.JobId, a.CandidateUserId }).IsUnique();
            e.HasIndex(a => a.CandidateUserId);
        });

        // Not a table: a SQL view created in SeedData.CreateViewAsync.
        b.Entity<JobStats>(e =>
        {
            e.HasNoKey();
            e.ToView("vw_JobApplicationStats");
        });
    }
}
