using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Helpers;

public class ErrorContextInfoTests
{
    [Fact(DisplayName = "ErrorContextInfo should return nothing if the HttpContext is null")]
    public void ErrorContextInfo_ReturnNothing_When_HttpContext_isNull()
    {
        var info = Should.NotThrow(() => new ErrorContextInfo(null!));
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "ErrorContextInfo should return defaults if we used default contstructor")]
    public void ErrorContextInfo_using_default_Contructor()
    {
        var info = new ErrorContextInfo();
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "ErrorContextInfo should get the RequestPath and the HttpMethod")]
    public void ErrorContextInfo_Get_RequestPath_and_HttpMethod()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/product/10";
        context.Request.Method = "POST";

        var info = new ErrorContextInfo(context);

        info.RequestPath.ShouldBe("/api/v1/product/10");
        info.HttpMethod.ShouldBe("POST");
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "ErrorContextInfo should get controller and action info if the project use controllers")]
    public void ErrorContextInfo_Get_ControllerName_Action()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "Product";
        context.Request.RouteValues["action"] = "GetById";

        var info = new ErrorContextInfo(context);

        info.Controller.ShouldBe("Product");
        info.Action.ShouldBe("GetById");
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.EndpointName.ShouldBeNull();
    }

    [Fact(DisplayName = "ErrorContextInfo should has EndpointName if the applicaiton use miminal APIss")]
    public void ErrorContextInfo_Has_EndpointName_In_MinimalAPIs()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EndpointNameMetadata("CreateOrderEndpoint")),
            "CreateOrderEndpoint"));

        var info = new ErrorContextInfo(context);
        info.EndpointName.ShouldBe("CreateOrderEndpoint");
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
    }

    [Fact(DisplayName = "ErrorContextInfo should has EndpointName from DispalyName if Enpoint Meta Data is null")]
    public void ErrorContextInfo_Has_EndpointName_from_DisplayName_EndpointMetaData_IsNull()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, null, "CreateOrderEndpoint"));

        var info = new ErrorContextInfo(context);
        info.EndpointName.ShouldBe("CreateOrderEndpoint");
        info.RequestPath.ShouldBeEmpty();
        info.HttpMethod.ShouldBeEmpty();
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
    }

    [Fact(DisplayName = "ErrorContextInfo should null controller and null action when the routeValues is null")]
    public void ErrorContextInfo_NullController_when_RouteValues_Null()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = null!;

        var info = new ErrorContextInfo(context);
        info.Controller.ShouldBeNull();
        info.Action.ShouldBeNull();
    }
}
