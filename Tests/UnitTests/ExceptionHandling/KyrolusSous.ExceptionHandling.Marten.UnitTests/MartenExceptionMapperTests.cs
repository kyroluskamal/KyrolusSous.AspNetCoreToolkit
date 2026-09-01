using System.Net;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Marten;
using Marten.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.ExceptionHandling.Marten.UnitTests;

public class MartenExceptionMapperTests
{
    private readonly KyrolusMartenExceptionMapper mapper = new();
    private readonly KyrolusErrorContext context = new(
        TraceId: "trace-marten-123",
        CorrelationId: "corr-marten-456",
        UserId: "user-marten",
        TenantId: "tenant-marten",
        Path: "/api/documents",
        Method: "POST",
        Culture: null);

    [Fact(DisplayName = "Order Should Be Minus50")]
    public void Order_ShouldBe_Minus50()
    {
        mapper.Order.ShouldBe(-50);
    }

    [Fact(DisplayName = "Concurrent Update Exception Should Map To Concurrency Conflict")]
    public void ConcurrentUpdateException_ShouldMapTo_ConcurrencyConflict()
    {
        var ex = new ConcurrentUpdateException(new Exception("optimistic concurrency failure"));
        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        mapping.IsTransient.ShouldBeTrue();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.ConcurrencyConflict);
        mapping.Error.Title.ShouldBe("Concurrency conflict");
    }

    [Fact(DisplayName = "Existing Stream Id Collision Exception Should Map To Conflict")]
    public void ExistingStreamIdCollisionException_ShouldMapTo_Conflict()
    {
        var ex = new ExistingStreamIdCollisionException("stream-123", typeof(object));
        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.Conflict);
    }

    [Fact(DisplayName = "Non Existent Stream Exception Should Map To Not Found")]
    public void NonExistentStreamException_ShouldMapTo_NotFound()
    {
        var ex = new NonExistentStreamException(Guid.NewGuid());
        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.NotFound);
    }

    [Fact(DisplayName = "Bad Linq Expression Exception Should Map To Bad Request")]
    public void BadLinqExpressionException_ShouldMapTo_BadRequest()
    {
        var ex = new BadLinqExpressionException("invalid linq query");
        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.BadRequest);
    }

    [Fact(DisplayName = "MartenCommandException should not leak the raw provider message by default")]
    public void MartenCommandException_Should_Not_LeakRawMessage_ByDefault()
    {
        var inner = new InvalidOperationException("relation \"public.mt_doc_user\" column \"email\" violates constraint");
        var ex = new MartenCommandException(null, inner);

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.DatabaseError);
        mapping.Error.Detail?.ShouldNotContain("mt_doc_user");
        mapping.Error.Detail.ShouldBe("A database error occurred.");
    }

    [Fact(DisplayName = "MartenCommandException should expose the raw provider message when explicitly opted in")]
    public void MartenCommandException_Should_ExposeRawMessage_WhenOptedIn()
    {
        var optedInOptions = Options.Create(new KyrolusExceptionHandlingOptions { IncludeRawDatabaseErrorDetails = true });
        var optedInMapper = new KyrolusMartenExceptionMapper(optedInOptions);
        var inner = new InvalidOperationException("boom");
        var ex = new MartenCommandException(null, inner);

        optedInMapper.TryMap(ex, context, out var mapping);

        mapping.Error.Detail.ShouldBe(ex.Message);
    }

    [Fact(DisplayName = "Add Kyrolus Marten Exception Mapping applies configured options")]
    public void AddKyrolusMartenExceptionMapping_AppliesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMartenExceptionMapping(o => o.IncludeRawDatabaseErrorDetails = true);

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetServices<IKyrolusExceptionMapper>().OfType<KyrolusMartenExceptionMapper>().Single();

        var ex = new MartenCommandException(null, new InvalidOperationException("boom"));
        mapper.TryMap(ex, context, out var mapping);

        mapping.Error.Detail.ShouldBe(ex.Message);
    }

    [Fact(DisplayName = "Unrelated Exception Should Return False")]
    public void UnrelatedException_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("Something bad");
        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeFalse();
        mapping.ShouldBeNull();
    }

    [Fact(DisplayName = "Add Kyrolus Marten Exception Mapping Registers Mapper")]
    public void AddKyrolusMartenExceptionMapping_RegistersMapper()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMartenExceptionMapping();

        var provider = services.BuildServiceProvider();
        var mappers = provider.GetServices<IKyrolusExceptionMapper>();

        mappers.ShouldContain(m => m is KyrolusMartenExceptionMapper);
    }
}
