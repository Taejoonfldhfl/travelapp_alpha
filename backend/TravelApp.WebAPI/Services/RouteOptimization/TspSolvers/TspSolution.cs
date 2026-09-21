namespace TravelApp.WebAPI.Services.RouteOptimization.TspSolvers
{
    // matrix의 인덱스 기준으로 표현한 방문 순서(경로)와 총 이동시간(초).
    // 방문 전부를 전제로 하므로 Order.Length == matrix의 지점 개수와 항상 같다.
    public record TspSolution(int[] Order, double TotalTravelTimeSeconds);
}
