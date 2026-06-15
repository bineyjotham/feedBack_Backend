using Microsoft.EntityFrameworkCore;
using FeedBack.API.Models;

namespace FeedBack.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<EmailQueue> EmailQueues { get; set; }
    public DbSet<Personnel> Personnels { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Feedback indexes
        modelBuilder.Entity<Feedback>()
            .HasIndex(f => f.TicketNumber)
            .IsUnique();
            
        modelBuilder.Entity<Feedback>()
            .HasIndex(f => f.CustomerEmail);
            
        modelBuilder.Entity<Feedback>()
            .HasIndex(f => f.CreatedAt);
            
        modelBuilder.Entity<Feedback>()
            .HasIndex(f => f.Source);
            
        modelBuilder.Entity<Feedback>()
            .HasIndex(f => f.Status);
            
        // User indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
            
        // EmailQueue indexes
        modelBuilder.Entity<EmailQueue>()
            .HasIndex(e => e.Status);
            
        modelBuilder.Entity<EmailQueue>()
            .HasIndex(e => e.CreatedAt);
            
        modelBuilder.Entity<EmailQueue>()
            .HasIndex(e => new { e.Status, e.AttemptCount });
    }
}