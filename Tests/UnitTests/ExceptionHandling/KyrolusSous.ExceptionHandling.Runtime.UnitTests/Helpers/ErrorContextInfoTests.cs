using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Helpers;

public class KyrolusErrorContextInfoTests
{
    [Fact(DisplayName = "KyrolusErrorContextInfo should return nothing if the HttpContext is null")]
    public void KyrolusErrorContextInfo_ReturnNothing_When_HttpContext_isNull()
    {
        var info = Should.NotThrow(() => new KyrolusErrorContextInfo(null!));
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusErrorContextInfo should return defaults if we used default contstructor")]
    public void KyrolusErrorContextInfo_using_default_Contructor()
    {
        var info = new KyrolusErrorContextInfo();
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusErrorContextInfo should get the RequestPath and the HttpMethod")]
    public void KyrolusErrorContextInfo_Get_RequestPath_and_HttpMethod()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/product/10";
        context.Request.Method = "POST";

        var info = new KyrolusErrorContextInfo(context);

        info.RequestPath.ShouldBe("/api/v1/product/10");
        info.HttpMethod.ShouldBe("POST");
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusErrorContextInfo should get controller and action info if the project use controllers")]
    public void KyrolusErrorContextInfo_Get_ControllerName_Action()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "Product";
        context.Request.RouteValues["action"] = "GetById";

        var info = new KyrolusErrorContextInfo(context);

        info.Controller.ShouldBe("Product");
        info.Action.ShouldBe("GetById");
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusErrorContextInfo should has EndpointName if the applicaiton use miminal APIss")]
    public void KyrolusErrorContextInfo_Has_EndpointName_In_MinimalAPIs()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EndpointNameMetadata("CreateOrderEndpoint")),
            "CreateOrderEndpoint"));

        var info = new KyrolusErrorContextInfo(context);
        info.EndpointName.ShouldBe("CreateOrderEndpoint");
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusErrorContextInfo should has EndpointName from DispalyName if Enpoint Meta Data is null")]
    public void KyrolusErrorContextInfo_Has_EndpointName_from_DisplayName_EndpointMetaData_IsNull()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, null, "CreateOrderEndpoint"));

        var info = new KyrolusErrorContextInfo(context);
        info.EndpointName.ShouldBe("CreateOrderEndpoint");
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusErrorContextInfo should null controller and null action when the routeValues is null")]
    public void KyrolusErrorContextInfo_NullController_when_RouteValues_Null()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = null!;

        var info = new KyrolusErrorContextInfo(context);
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
    }
}
