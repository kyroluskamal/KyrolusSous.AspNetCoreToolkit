using KyrolusSous.EndpointKit.Core.Filters;
using KyrolusSous.Validation.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusValidationEndpointFilterTests
{
    public sealed class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [Fact(DisplayName = "ValidationFilter: Proceeds to next delegate when model is valid")]
    public async Task ValidationFilter_Should_Proceed_When_Valid()
    {
        var validationEngine = Substitute.For<IKyrolusValidationEngine>();
        validationEngine.ValidateAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(new List<KyrolusValidationFailure>()));

        var services = new ServiceCollection();
        services.AddSingleton(validationEngine);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);
        filterContext.Arguments.Returns(new List<object?> { new CreateProductDto { Name = "Laptop", Price = 999 } });

        var filter = new KyrolusValidationEndpointFilter();
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ResultOK");
        };

        var result = await filter.InvokeAsync(filterContext, next);
        nextCalled.ShouldBeTrue();
        result.ShouldBe("ResultOK");
    }

    [Fact(DisplayName = "ValidationFilter: Short-circuits with ValidationProblem when validation fails")]
    public async Task ValidationFilter_Should_ShortCircuit_On_Validation_Error()
    {
        var validationEngine = Substitute.For<IKyrolusValidationEngine>();
        var failures = new List<KyrolusValidationFailure>
        {
            new("Name", "Name is required", "ERR_REQUIRED")
        };

        validationEngine.ValidateAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures));

        var services = new ServiceCollection();
        services.AddSingleton(validationEngine);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);
        filterContext.Arguments.Returns(new List<object?> { new CreateProductDto { Name = "", Price = 999 } });

        var filter = new KyrolusValidationEndpointFilter();
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ResultOK");
        };

        var result = await filter.InvokeAsync(filterContext, next);
        nextCalled.ShouldBeFalse();
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ProblemHttpResult>();
    }
}
