using Microsoft.EntityFrameworkCore;
using SharedData.Models;

namespace TravelApp.WebAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) 
    {
    }

    // User 클래스를 DbSet으로 등록하여 데이터베이스에서 Users 테이블로 매핑
    public DbSet<User> Users { get; set; }

    public DbSet<Trip> Trips { get; set; }

    public DbSet<TripMember> TripMembers { get; set; }

    public DbSet<SharedData.Models.Schedule> Schedules { get; set; }

    public DbSet<ChatSession> ChatSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TripMember>()
            .HasIndex(tm => new { tm.TripId, tm.UserId })
            .IsUnique();
    }
}
