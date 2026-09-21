﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharedData.Models;

namespace TravelApp.WebAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) 
    {
    }

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

        // Npgsql은 timestamp with time zone 컬럼에 Kind=Utc인 DateTime만 허용한다.
        // 요청 바디(JSON)에서 역직렬화된 DateTime은 기본적으로 Kind=Unspecified라
        // 저장/조회 시 이 값을 강제로 Utc로 맞춰준다. (모든 엔티티/모든 DateTime, DateTime? 컬럼에 일괄 적용)
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }
}