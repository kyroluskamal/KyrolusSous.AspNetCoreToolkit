using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Validation.Abstractions;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusExceptionMapperBaseTests
{
    public sealed record TestMappedResponse(string Category, string Message);

    /// <summary>
    /// Test double proving the base class dispatches to the right factory method per recognized
    /// exception type, and records which one (if any) actually ran.
    /// </summary>
    private sealed class RecordingTestMapper : KyrolusExceptionMapperBase<TestMappedResponse>
    {
        public int SecurityCalls { get; private set; }
        public int IdempotencyCalls { get; private set; }
        public int ValidationCalls { get; private set; }

        protected override TestMappedResponse OnSecurityException(KyrolusSecurityException exception)
        {
            SecurityCalls++;
            return new TestMappedResponse("Security", exception.Message);
        }

        protected override TestMappedResponse OnIdempotencyConflict(KyrolusIdempotencyConflictException exception)
        {
            IdempotencyCalls++;
            return new TestMappedResponse("Idempotency", exception.Message);
        }

        protected override TestMappedResponse OnValidationFailed(KyrolusValidationException exception)
        {
            ValidationCalls++;
            return new TestMappedResponse("Validation", exception.Message);
        }
    }

    public sealed record ProtectedCommand(string Name) : IKyrolusCommand<TestMappedResponse>;

    [Fact(DisplayName = "KyrolusExceptionMapperBase: KyrolusSecurityException routes to OnSecurityException")]
    public void TryMap_SecurityException_RoutesToOnSecurityException()
    {
        var mapper = new RecordingTestMapper();
        var exception = new KyrolusSecurityException("Missing role 'Admin'.", "Admin");

        var mapped = mapper.TryMap(exception, out var response);

        mapped.ShouldBeTrue();
        mapper.SecurityCalls.ShouldBe(1);
        mapper.IdempotencyCalls.ShouldBe(0);
        mapper.ValidationCalls.ShouldBe(0);
        response.ShouldBe(new TestMappedResponse("Security", exception.Message));
    }

    [Fact(DisplayName = "KyrolusExceptionMapperBase: KyrolusIdempotencyConflictException routes to OnIdempotencyConflict")]
    public void TryMap_IdempotencyConflictException_RoutesToOnIdempotencyConflict()
    {
        var mapper = new RecordingTestMapper();
        var exception = new KyrolusIdempotencyConflictException("key-123");

        var mapped = mapper.TryMap(exception, out var response);

        mapped.ShouldBeTrue();
        mapper.IdempotencyCalls.ShouldBe(1);
        mapper.SecurityCalls.ShouldBe(0);
        mapper.ValidationCalls.ShouldBe(0);
        response.ShouldBe(new TestMappedResponse("Idempotency", exception.Message));
    }

    [Fact(DisplayName = "KyrolusExceptionMapperBase: KyrolusValidationException routes to OnValidationFailed")]
    public void TryMap_ValidationException_RoutesToOnValidationFailed()
    {
        var mapper = new RecordingTestMapper();
        IReadOnlyList<KyrolusValidationFailure> failures = [new("Name", "Name is required", null, KyrolusValidationSeverity.Error, "ERR_EMPTY")];
        var exception = new KyrolusValidationException(failures);

        var mapped = mapper.TryMap(exception, out var response);

        mapped.ShouldBeTrue();
        mapper.ValidationCalls.ShouldBe(1);
        mapper.SecurityCalls.ShouldBe(0);
        mapper.IdempotencyCalls.ShouldBe(0);
        response.ShouldBe(new TestMappedResponse("Validation", exception.Message));
    }

    [Fact(DisplayName = "KyrolusExceptionMapperBase: Unrecognized exception type returns false and calls no factory method")]
    public void TryMap_UnrecognizedException_ReturnsFalse_CallsNoFactoryMethod()
    {
        var mapper = new RecordingTestMapper();
        var exception = new InvalidOperationException("boom");

        var mapped = mapper.TryMap(exception, out _);

        mapped.ShouldBeFalse();
        mapper.SecurityCalls.ShouldBe(0);
        mapper.IdempotencyCalls.ShouldBe(0);
        mapper.ValidationCalls.ShouldBe(0);
    }

    [Fact(DisplayName = "KyrolusExceptionMapperBase: Null exception throws instead of silently mapping")]
    public void TryMap_NullException_Throws()
    {
        var mapper = new RecordingTestMapper();

        Should.Throw<ArgumentNullException>(() => mapper.TryMap(null!, out _));
    }

    [Fact(DisplayName = "KyrolusExceptionMapperBase + KyrolusExceptionMappingBehavior: Handler throwing KyrolusSecurityException returns the app-supplied response")]
    public async Task ExceptionMappingBehavior_WithBaseClassMapper_HandlerThrowsSecurityException_ReturnsAppSuppliedResponse()
    {
        var mapper = new RecordingTestMapper();
        var behavior = new KyrolusExceptionMappingBehavior<ProtectedCommand, TestMappedResponse>([mapper]);
        var request = new ProtectedCommand("test");

        var result = await behavior.Handle(
            request,
            _ => throw new KyrolusSecurityException("User is not authorized.", "Admin"),
            CancellationToken.None);

        mapper.SecurityCalls.ShouldBe(1);
        result.ShouldBe(new TestMappedResponse("Security", "User is not authorized."));
    }
}
