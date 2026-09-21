namespace TravelApp.WebAPI.Services.RouteOptimization
{
    // 경로 최적화 대상 지점 하나. ScheduleId로 나중에 원래 일정에 다시 연결한다.
    public readonly record struct GeoPoint(int ScheduleId, double Latitude, double Longitude);
}
