using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using TravelApp.WebAPI.Controllers;

namespace TravelApp.WebAPI.Tests;

// UpdateTrip이 한때 [HttpPut("id")](리터럴 문자열 "id")로 선언돼 있어 PUT api/Trip/{id}가 아니라
// PUT api/Trip/id 라는 고정 경로로만 매칭되는 버그가 있었다. 라우트 파라미터 이름과 컨트롤러
// 액션 파라미터 이름이 실제로 일치하는지 리플렉션으로 검증해 같은 실수가 재발하면 바로 잡아낸다.
public class TripControllerRouteTests
{
    [Fact]
    public void UpdateTrip_RouteTemplate_UsesIdAsRouteParameter()
    {
        var method = typeof(TripController).GetMethod(nameof(TripController.UpdateTrip));
        Assert.NotNull(method);

        var httpPut = method!.GetCustomAttribute<HttpPutAttribute>();
        Assert.NotNull(httpPut);
        Assert.Equal("{id}", httpPut!.Template);
    }

    // TripController의 다른 {id} 기반 액션들도 같은 방식으로 선언돼 있는지 함께 확인한다.
    [Theory]
    [InlineData(nameof(TripController.GetTrip), typeof(HttpGetAttribute))]
    [InlineData(nameof(TripController.UpdateTrip), typeof(HttpPutAttribute))]
    [InlineData(nameof(TripController.DeleteTrip), typeof(HttpDeleteAttribute))]
    public void IdRouteActions_TemplateIsBraceWrappedId(string methodName, System.Type attributeType)
    {
        var method = typeof(TripController).GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttributes(attributeType, inherit: false).Single();
        var template = (string?)attributeType.GetProperty("Template")!.GetValue(attribute);

        Assert.Equal("{id}", template);
    }
}
