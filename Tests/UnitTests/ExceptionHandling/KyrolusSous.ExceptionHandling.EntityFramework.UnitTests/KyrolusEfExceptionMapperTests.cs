using System.Data.Common;
using System.Net;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.ExceptionHandling.EntityFramework.UnitTests;

public class KyrolusEfExceptionMapperTests
{
    private readonly KyrolusEfExceptionMapper mapper = new();
    private readonly KyrolusErrorContext context = new(
        TraceId: "trace-ef-123",
        CorrelationId: "corr-ef-456",
        UserId: "user-ef",
        TenantId: "tenant-ef",
        Path: "/api/entities",
        Method: "POST",
        Culture: null);

    [Fact(DisplayName = "Order should return -50")]
    public void Order_Should_Be_Minus50()
    {
        mapper.Order.ShouldBe(-50);
    }

    [Fact(DisplayName = "TryMap with DbUpdateConcurrencyException should map to ConcurrencyConflict")]
    public void TryMap_DbUpdateConcurrencyException_Should_Map_To_ConcurrencyConflict()
    {
        var ex = new DbUpdateConcurrencyException("Optimistic concurrency error occurred");
        ex.Data["EntityName"] = "Product";

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        mapping.IsTransient.ShouldBeTrue();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.ConcurrencyConflict);
        mapping.Error.Title.ShouldBe("Concurrency conflict");
        mapping.Error.Detail.ShouldBe("Optimistic concurrency error occurred");
        mapping.Error.TraceId.ShouldBe("trace-ef-123");
        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata["EntityName"]!.ToString().ShouldBe("Product");
    }

    [Fact(DisplayName = "TryMap with DbUpdateException and transient inner exception should map to transient DatabaseError")]
    public void TryMap_DbUpdateException_With_Transient_Inner_Should_Be_Transient()
    {
        var inner = new TimeoutException("Database connection timed out");
        var ex = new DbUpdateException("Could not update database", inner);

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        mapping.IsTransient.ShouldBeTrue();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.DatabaseError);
        mapping.Error.Title.ShouldBe("Database error");
        mapping.Error.Detail.ShouldBe("Could not update database");
    }

    [Fact(DisplayName = "TryMap with DbUpdateException and non-transient inner exception should map to non-transient DatabaseError")]
    public void TryMap_DbUpdateException_With_NonTransient_Inner_Should_Not_Be_Transient()
    {
        var inner = new InvalidOperationException("Non-transient DB validation error");
        var ex = new DbUpdateException("Could not save changes", inner);

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        mapping.IsTransient.ShouldBeFalse();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.DatabaseError);
    }

    [Fact(DisplayName = "TryMap with unrelated exception should return false and null mapping")]
    public void TryMap_UnrelatedException_Should_Return_False()
    {
        var ex = new ArgumentException("Invalid argument");

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeFalse();
        mapping.ShouldBeNull();
    }

    [Fact(DisplayName = "AddKyrolusEntityFrameworkExceptionHandling should register mapper in DI")]
    public void AddKyrolusEntityFrameworkExceptionHandling_Should_Register_Mapper()
    {
        var services = new ServiceCollection();
        services.AddKyrolusEntityFrameworkExceptionHandling();

        var provider = services.BuildServiceProvider();
        var mappers = provider.GetServices<IKyrolusExceptionMapper>().ToList();

        mappers.ShouldNotBeEmpty();
        mappers.ShouldContain(m => m is KyrolusEfExceptionMapper);
    }
}
