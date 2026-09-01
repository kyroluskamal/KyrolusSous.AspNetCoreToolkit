namespace KyrolusSous.Validation.Runtime.UnitTests;

public class TestValidator : IKyrolusRequestValidator<TestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TestRequest request, CancellationToken cancellationToken = default)
    {
        var failures = new List<KyrolusValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Name))
            failures.Add(new KyrolusValidationFailure(nameof(request.Name), "Name cannot be null or whitespace."));

        if (request.Age < 0)
            failures.Add(new KyrolusValidationFailure(nameof(request.Age), "Age cannot be negative."));

        return ValueTask.FromResult((IReadOnlyList<KyrolusValidationFailure>)failures);
    }
}

public class TestRequest
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class ThrowingTestValidator : IKyrolusRequestValidator<TestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TestRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated validator failure.");
}

public class ProfileTestRequest
{
    public string Name { get; set; } = string.Empty;
}

public class ProfileTestValidator : IKyrolusRequestValidator<ProfileTestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(ProfileTestRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures =
        [
            new("Prop1", "Warning in A", Severity: KyrolusValidationSeverity.Warning, RuleSet: "RuleSetA"),
            new("Prop2", "Error in A", Severity: KyrolusValidationSeverity.Error, RuleSet: "RuleSetA"),
            new("Prop3", "Error in B", Severity: KyrolusValidationSeverity.Error, RuleSet: "RuleSetB"),
            new("Prop4", "Error in Other", Severity: KyrolusValidationSeverity.Error, RuleSet: "OtherRuleSet"),
            new("Prop5", "Error in Other", Severity: KyrolusValidationSeverity.Error, RuleSet: "Context1")
        ];

        return ValueTask.FromResult(failures);
    }
}

public class HookTestRequest
{
    public int Id { get; set; }
}

public class TestGlobalValidationHook : IKyrolusValidationHook
{
    public bool OnBeforeCalled { get; private set; }
    public bool OnAfterCalled { get; private set; }
    public object? PassedRequest { get; private set; }
    public KyrolusValidationContext? PassedContext { get; private set; }

    public ValueTask OnBeforeAsync(object? request, KyrolusValidationContext context, CancellationToken cancellationToken = default)
    {
        OnBeforeCalled = true;
        PassedRequest = request;
        PassedContext = context;
        return ValueTask.CompletedTask;
    }

    public ValueTask OnAfterAsync(object? request, KyrolusValidationContext context, IReadOnlyList<KyrolusValidationFailure> failures, CancellationToken cancellationToken = default)
    {
        OnAfterCalled = true;
        return ValueTask.CompletedTask;
    }
}

public class TestRequestSpecificValidationHook : IKyrolusValidationHook<HookTestRequest>
{
    public bool OnBeforeCalled { get; private set; }
    public bool OnAfterCalled { get; private set; }
    public HookTestRequest? PassedRequest { get; private set; }
    public KyrolusValidationContext? PassedContext { get; private set; }

    public ValueTask OnBeforeAsync(HookTestRequest request, KyrolusValidationContext context, CancellationToken cancellationToken = default)
    {
        OnBeforeCalled = true;
        PassedRequest = request;
        PassedContext = context;
        return ValueTask.CompletedTask;
    }

    public ValueTask OnAfterAsync(HookTestRequest request, KyrolusValidationContext context, IReadOnlyList<KyrolusValidationFailure> failures, CancellationToken cancellationToken = default)
    {
        OnAfterCalled = true;
        return ValueTask.CompletedTask;
    }
}

public class CacheableTestRequest : IKyrolusValidationCacheable
{
    public string CacheKey { get; set; } = "test-cache-key-1";
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.All;
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(5);
}

public class CacheableTestValidator : IKyrolusRequestValidator<CacheableTestRequest>
{
    public int ExecutionCount { get; private set; }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(CacheableTestRequest request, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        IReadOnlyList<KyrolusValidationFailure> failures = [new("Prop", "Cached failure message")];
        return ValueTask.FromResult(failures);
    }
}

public class TestLocalizer : IKyrolusLocalizer
{
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null) =>
        new($"Localized: {key}", ResourceNotFound: false);

    public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null) =>
        GetString(key, culture);

    public string Format(string template, object? arguments) => template;
}

public class ContextValidatorTestRequest
{
    public string Title { get; set; } = string.Empty;
}

public class ContextValidatorTestValidator : IKyrolusRequestValidatorWithContext<ContextValidatorTestRequest>
{
    public KyrolusValidationContext? ReceivedContext { get; private set; }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(ContextValidatorTestRequest request, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
    }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(ContextValidatorTestRequest request, KyrolusValidationContext context, CancellationToken cancellationToken = default)
    {
        ReceivedContext = context;
        IReadOnlyList<KyrolusValidationFailure> failures = [new("Title", "Context Validator Failure", RuleSet: "RuleSetA")];
        return ValueTask.FromResult(failures);
    }
}

public class CompositeTestValidator : IKyrolusRequestValidator<KyrolusValidationComposite<TestRequest, ProfileTestRequest>>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(KyrolusValidationComposite<TestRequest, ProfileTestRequest> composite, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures = [new("CompositeProp", "Composite validation failed")];
        return ValueTask.FromResult(failures);
    }
}

public class GroupTestRequest
{
    public string Name { get; set; } = string.Empty;
}

public class GroupTestValidator : IKyrolusRequestValidator<GroupTestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(GroupTestRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures =
        [
            new("Prop1", "Error in GroupA", Groups: ["GroupA"]),
            new("Prop2", "Error in GroupB", Groups: ["GroupB"]),
            new("Prop3", "Error in ContextGroup", Groups: ["ContextGroup"]),
            new("Prop4", "Error in OtherGroup", Groups: ["OtherGroup"])
        ];

        return ValueTask.FromResult(failures);
    }
}

public class MappingTestRequest
{
    public int Age { get; set; }
}

public class MappingTestValidator : IKyrolusRequestValidator<MappingTestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(MappingTestRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures = [new("Age", "Invalid age", ErrorCode: "ERR_AGE")];
        return ValueTask.FromResult(failures);
    }
}

public class NegativeCacheableTestRequest : IKyrolusValidationCacheable, IKyrolusValidationNegativeCacheable
{
    public string CacheKey { get; set; } = "negative-cache-key";
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.All;
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(10);
    public TimeSpan? NegativeCacheTtl => TimeSpan.FromMinutes(2);
}

public class NegativeCacheableFailingTestValidator : IKyrolusRequestValidator<NegativeCacheableTestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(NegativeCacheableTestRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures = [new("Prop", "Always fails")];
        return ValueTask.FromResult(failures);
    }
}

/// <summary>
/// Records the arguments of the last <see cref="SetAsync"/> call, so a test can assert exactly which TTL the
/// engine chose for a given outcome (positive vs. negative caching) without depending on real elapsed time.
/// </summary>
public sealed class SpyValidationCacheStore : IKyrolusValidationCacheStore
{
    public TimeSpan? LastTtl { get; private set; }
    public IReadOnlyList<KyrolusValidationFailure>? LastFailures { get; private set; }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>?> TryGetAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);

    public ValueTask SetAsync(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        LastTtl = ttl;
        LastFailures = failures;
        return ValueTask.CompletedTask;
    }
}

public class CancellationTokenTestRequest
{
    public string Data { get; set; } = string.Empty;
}

public class CancellationTokenTestValidator : IKyrolusRequestValidator<CancellationTokenTestRequest>
{
    public CancellationToken ReceivedToken { get; private set; }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(CancellationTokenTestRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
    }
}

public class SuccessOnlyCacheableTestRequest : IKyrolusValidationCacheable
{
    public string CacheKey { get; set; } = "success-only-key";
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.SuccessOnly;
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(5);
}

public class FailuresOnlyCacheableTestRequest : IKyrolusValidationCacheable
{
    public string CacheKey { get; set; } = "failures-only-key";
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.FailuresOnly;
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(5);
}

public class FailuresOnlyCacheableTestValidator : IKyrolusRequestValidator<FailuresOnlyCacheableTestRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(FailuresOnlyCacheableTestRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures = [new("Prop", "Failure message")];
        return ValueTask.FromResult(failures);
    }
}

public class NullRuleSetAndGroupRequest
{
    public string Name { get; set; } = string.Empty;
}

public class NullRuleSetAndGroupValidator : IKyrolusRequestValidator<NullRuleSetAndGroupRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(NullRuleSetAndGroupRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusValidationFailure> failures =
        [
            new("Prop", "Failure with null RuleSet and null Group", RuleSet: null, Groups: null)
        ];
        return ValueTask.FromResult(failures);
    }
}

public class CacheableNullKeyRequest : IKyrolusValidationCacheable
{
    public string? CacheKey => null;
    public TimeSpan? CacheTtl => throw new NotImplementedException();
    public KyrolusValidationCacheMode CacheMode => throw new NotImplementedException();

}
public class CacheableEmptyStringKeyRequest : IKyrolusValidationCacheable
{
    public string? CacheKey => "";
    public TimeSpan? CacheTtl => throw new NotImplementedException();
    public KyrolusValidationCacheMode CacheMode => throw new NotImplementedException();

}

public class CacheableCacheModeIsNoneRequest : IKyrolusValidationCacheable
{
    public string? CacheKey => "NONEMODECACHEKEY";
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(5);
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.None;
}

public class ZeroTtlCacheableTestRequest : IKyrolusValidationCacheable
{
    public string? CacheKey => "ZEROTTLKEY";
    public TimeSpan? CacheTtl => TimeSpan.Zero;
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.All;
}

public class ValidCacheableTestRequest : IKyrolusValidationCacheable
{
    public string? CacheKey => "ValidCache";
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(1);
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.FailuresOnly;
}

public class ValidCacheableWithNullTtlRequest : IKyrolusValidationCacheable
{
    public string? CacheKey => "ValidCacheNullTtl";
    public TimeSpan? CacheTtl => null;
    public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.All;
}


public enum InvalidRequestKind
{
    NullRequest,
    NotCacheableObject,
    NullCacheKey,
    EmptyCacheKey,
    CacheModeIsNone,
    ZeroTtl
}