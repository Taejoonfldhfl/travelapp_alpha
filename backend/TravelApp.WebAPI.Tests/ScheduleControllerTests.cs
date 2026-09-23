using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Data;

namespace TravelApp.WebAPI.Tests;

// 일정 시간대 겹침 검증: [StartTime, EndTime) 반열린 구간 기준, 호텔 체크인 일정은 양쪽 모두 제외.
public class ScheduleControllerTests
{
    private const int TripId = 1;
    private static readonly DateTime Day = new(2026, 10, 1);

    private static (ScheduleController controller, ApplicationDbContext db) Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        db.Trips.Add(new Trip { Id = TripId, Title = "t", OwnerId = 1, StartDate = Day, EndDate = Day.AddDays(2) });
        db.TripMembers.Add(new TripMember { TripId = TripId, UserId = 1 });
        db.SaveChanges();

        var controller = new ScheduleController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
                }
            }
        };
        return (controller, db);
    }

    private static Schedule AddExisting(ApplicationDbContext db, string title, int startHour, int endHour, bool isHotel = false)
    {
        var schedule = new Schedule
        {
            TripId = TripId,
            Title = title,
            StartTime = Day.AddHours(startHour),
            EndTime = Day.AddHours(endHour),
            IsHotelCheckIn = isHotel
        };
        db.Schedules.Add(schedule);
        db.SaveChanges();
        return schedule;
    }

    private static ScheduleCreateDto NewSchedule(int startHour, int endHour, bool isHotel = false) => new()
    {
        Title = "새 일정",
        StartTime = Day.AddHours(startHour),
        EndTime = Day.AddHours(endHour),
        IsHotelCheckIn = isHotel
    };

    [Theory]
    [InlineData(9, 13)]  // 기존 일정을 완전히 감쌈
    [InlineData(10, 12)] // 기존 일정과 똑같은 구간
    [InlineData(10, 11)] // 기존 일정 안에 포함
    [InlineData(11, 13)] // 시작만 기존 일정 안에 걸침
    [InlineData(9, 11)]  // 끝만 기존 일정 안에 걸침
    public async Task Create_OverlappingTime_ReturnsBadRequestNamingTheSchedule(int startHour, int endHour)
    {
        var (controller, db) = Create();
        AddExisting(db, "점심", 10, 12);

        var result = await controller.CreateSchedule(TripId, NewSchedule(startHour, endHour));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequest.Value);
        Assert.Contains("'점심' (10-01 10:00~10-01 12:00)", message);
        Assert.Equal(1, db.Schedules.Count());
    }

    [Theory]
    [InlineData(12, 13)] // 기존 일정이 끝나는 시각에 바로 시작
    [InlineData(8, 10)]  // 기존 일정이 시작하는 시각에 바로 끝남
    [InlineData(14, 15)] // 떨어져 있음
    public async Task Create_AdjacentOrSeparateTime_IsAccepted(int startHour, int endHour)
    {
        var (controller, db) = Create();
        AddExisting(db, "점심", 10, 12);

        var result = await controller.CreateSchedule(TripId, NewSchedule(startHour, endHour));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(2, db.Schedules.Count());
    }

    [Fact]
    public async Task Create_OverlapInAnotherTrip_IsIgnored()
    {
        var (controller, db) = Create();
        db.Schedules.Add(new Schedule { TripId = 2, Title = "다른 여행", StartTime = Day.AddHours(10), EndTime = Day.AddHours(12) });
        db.SaveChanges();

        var result = await controller.CreateSchedule(TripId, NewSchedule(10, 12));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ManyOverlaps_ListsFirstThreeAndCountsTheRest()
    {
        var (controller, db) = Create();
        AddExisting(db, "A", 9, 10);
        AddExisting(db, "B", 10, 11);
        AddExisting(db, "C", 11, 12);
        AddExisting(db, "D", 12, 13);
        AddExisting(db, "E", 13, 14);

        var result = await controller.CreateSchedule(TripId, NewSchedule(9, 14));

        var message = (string)Assert.IsType<BadRequestObjectResult>(result.Result).Value!;
        Assert.Contains("'A'", message);
        Assert.Contains("'C'", message);
        Assert.DoesNotContain("'D'", message);
        Assert.EndsWith("외 2건", message);
    }

    [Fact]
    public async Task Update_OnlyOtherFieldsChanged_DoesNotOverlapWithItself()
    {
        var (controller, db) = Create();
        var existing = AddExisting(db, "점심", 10, 12);

        var result = await controller.UpdateSchedule(TripId, existing.Id, new ScheduleUpdateDto
        {
            Title = "점심 (예약 완료)",
            StartTime = existing.StartTime,
            EndTime = existing.EndTime
        });

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("점심 (예약 완료)", db.Schedules.Single().Title);
    }

    [Fact]
    public async Task Update_MovedOntoAnotherSchedule_ReturnsBadRequestAndKeepsOriginal()
    {
        var (controller, db) = Create();
        AddExisting(db, "점심", 10, 12);
        var moving = AddExisting(db, "카페", 13, 14);

        var result = await controller.UpdateSchedule(TripId, moving.Id, new ScheduleUpdateDto
        {
            Title = "카페",
            StartTime = Day.AddHours(11),
            EndTime = Day.AddHours(12).AddMinutes(30)
        });

        var message = (string)Assert.IsType<BadRequestObjectResult>(result.Result).Value!;
        Assert.Contains("'점심'", message);
        Assert.DoesNotContain("'카페'", message);
        Assert.Equal(Day.AddHours(13), db.Schedules.Single(s => s.Id == moving.Id).StartTime);
    }

    [Fact]
    public async Task Create_ActivityInsideExistingHotelStay_IsAccepted()
    {
        var (controller, db) = Create();
        AddExisting(db, "호텔 체크인", 15, 35, isHotel: true); // 1일 15시 ~ 2일 11시

        var result = await controller.CreateSchedule(TripId, NewSchedule(18, 20));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_HotelStayCoveringExistingActivities_IsAccepted()
    {
        var (controller, db) = Create();
        AddExisting(db, "저녁", 18, 20);

        var result = await controller.CreateSchedule(TripId, NewSchedule(15, 35, isHotel: true));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_ActivityMovedIntoHotelStay_IsAccepted()
    {
        var (controller, db) = Create();
        AddExisting(db, "호텔 체크인", 15, 35, isHotel: true);
        var activity = AddExisting(db, "저녁", 12, 13);

        var result = await controller.UpdateSchedule(TripId, activity.Id, new ScheduleUpdateDto
        {
            Title = "저녁",
            StartTime = Day.AddHours(18),
            EndTime = Day.AddHours(20)
        });

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
