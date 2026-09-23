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

    public DbSet<Expense> Expenses { get; set; }

    public DbSet<ExpenseSplit> ExpenseSplits { get; set; }

    // 같은 여행에서 [start, end) 구간과 시간이 겹치는 기존 일정들을 시작 시간 순으로 돌려준다.
    // 호텔 체크인 일정은 체크인~체크아웃 전체를 감싸도록 설계되어 다른 활동과 겹치는 게 정상이므로,
    // 신규 쪽이 호텔 일정이면 검사하지 않고, 기존 쪽의 호텔 일정도 비교 대상에서 뺀다.
    // 수정 시에는 excludeScheduleId로 자기 자신을 제외한다.
    public async Task<List<SharedData.Models.Schedule>> FindOverlappingSchedulesAsync(
        int tripId,
        DateTime start,
        DateTime end,
        bool isHotelCheckIn,
        int? excludeScheduleId = null)
    {
        if (isHotelCheckIn)
        {
            return new List<SharedData.Models.Schedule>();
        }

        return await Schedules
            .Where(s =>
                s.TripId == tripId &&
                !s.IsHotelCheckIn &&
                (excludeScheduleId == null || s.Id != excludeScheduleId) &&
                s.StartTime < end &&
                start < s.EndTime)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trip>()
            .Property(t => t.BudgetAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Trip).WithMany(t => t.Expenses).HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PaidByUser).WithMany().HasForeignKey(x => x.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.TripId);
        });

        modelBuilder.Entity<ExpenseSplit>(s =>
        {
            s.HasKey(x => new { x.ExpenseId, x.UserId });
            s.Property(x => x.ShareAmount).HasPrecision(18, 2);
            s.HasOne(x => x.Expense).WithMany(x => x.Splits).HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Cascade);
            s.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

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