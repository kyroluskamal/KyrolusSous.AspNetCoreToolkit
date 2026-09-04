using KyrolusSous.CQRS.Abstractions.Audit;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Outbox;
using KyrolusSous.CQRS.Validation;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Validation.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>Regression tests for the fourth CQRS-only review round (all-library review, then a fix pass).</summary>
public sealed class KyrolusReviewRound4Tests
{
    #region Audit: dictionary payloads must be redacted by key, not by the KeyValuePair's own property names

    public sealed record PatchLikeCommand(Dictionary<string, object> Updates)
        : IKyrolusCommand<string>, IKyrolusAuditableCommand;

    [Fact(DisplayName = "Audit: a Dictionary payload redacts entries whose KEY is sensitive, not the literal words 'Key'/'Value'")]
    public async Task Audit_DictionaryPayload_RedactsBySensitiveKey()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var behavior = new KyrolusAuditBehavior<PatchLikeCommand, string>(sink);

        await behavior.Handle(
            new PatchLikeCommand(new Dictionary<string, object> { ["Password"] = "hunter2", ["Name"] = "Alice" }),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        var payload = sink.Entries.Single().Payload.ShouldBeOfType<Dictionary<string, object?>>();
        var updates = payload["Updates"].ShouldBeOfType<Dictionary<string, object?>>();
        updates["Password"].ShouldBe("***REDACTED***");
        updates["Name"].ShouldBe("Alice");
    }

    #endregion

    #region Audit: a single property throwing during sanitization must not leak the rest of the object raw

    public sealed record ThrowingComputedPropertyCommand(string CardNumber) : IKyrolusCommand<string>, IKyrolusAuditableCommand
    {
        public string Computed => throw new InvalidOperationException("navigation not loaded");
    }

    [Fact(DisplayName = "Audit: a throwing property is isolated instead of causing the whole payload to be logged raw")]
    public async Task Audit_ThrowingProperty_DoesNotLeakRestOfObjectRaw()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var behavior = new KyrolusAuditBehavior<ThrowingComputedPropertyCommand, string>(sink);

        await behavior.Handle(
            new ThrowingComputedPropertyCommand("4111111111111111"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        var payload = sink.Entries.Single().Payload.ShouldBeOfType<Dictionary<string, object?>>();
        payload["CardNumber"].ShouldBe("***REDACTED***"); // must still be redacted, not exposed raw
        payload["Computed"].ShouldBe("***UNAVAILABLE***");
    }

    #endregion

    #region Audit: sensitive-keyword list covers ApiKey by default and can be extended per application

    public sealed record ApiKeyCommand(string ApiKey, string InternalCode) : IKyrolusCommand<string>, IKyrolusAuditableCommand;

    [Fact(DisplayName = "Audit: ApiKey is redacted by the built-in keyword list")]
    public async Task Audit_ApiKey_RedactedByDefault()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var behavior = new KyrolusAuditBehavior<ApiKeyCommand, string>(sink);

        await behavior.Handle(new ApiKeyCommand("abc123", "n/a"), _ => Task.FromResult("ok"), CancellationToken.None);

        var payload = sink.Entries.Single().Payload.ShouldBeOfType<Dictionary<string, object?>>();
        payload["ApiKey"].ShouldBe("***REDACTED***");
    }

    [Fact(DisplayName = "Audit: an application-specific keyword supplied via KyrolusAuditSanitizationOptions is also redacted")]
    public async Task Audit_ExtraKeyword_FromOptions_IsRedacted()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var options = new KyrolusAuditSanitizationOptions { AdditionalSensitiveKeywords = ["InternalCode"] };
        var behavior = new KyrolusAuditBehavior<ApiKeyCommand, string>(sink, sanitizationOptions: options);

        await behavior.Handle(new ApiKeyCommand("abc123", "secret-code"), _ => Task.FromResult("ok"), CancellationToken.None);

        var payload = sink.Entries.Single().Payload.ShouldBeOfType<Dictionary<string, object?>>();
        payload["InternalCode"].ShouldBe("***REDACTED***");
    }

    #endregion

    #region Outbox: claiming a message must prevent a second, overlapping processing pass from also picking it up

    [Fact(DisplayName = "Outbox: TryClaimAsync only lets one caller win the claim on a pending message")]
    public async Task Outbox_TryClaimAsync_OnlyOneCallerWins()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var msg = new KyrolusOutboxMessage { EventType = "Some.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        await store.SaveAsync(msg);

        var firstClaim = await store.TryClaimAsync(msg.Id);
        var secondClaim = await store.TryClaimAsync(msg.Id);

        firstClaim.ShouldBeTrue();
        secondClaim.ShouldBeFalse();
        store.AllMessages.Single().Status.ShouldBe(KyrolusOutboxMessageStatus.Processing);
    }

    [Fact(DisplayName = "Outbox: a claimed message is excluded from the next GetPendingAsync batch")]
    public async Task Outbox_ClaimedMessage_ExcludedFromPendingBatch()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var msg = new KyrolusOutboxMessage { EventType = "Some.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        await store.SaveAsync(msg);

        (await store.TryClaimAsync(msg.Id)).ShouldBeTrue();

        var pending = await store.GetPendingAsync();
        pending.ShouldBeEmpty();
    }

    #endregion

    #region Validation: Info/Warning failures are non-blocking by default; Error still blocks; threshold is configurable

    public sealed record SomeCommand(string Value) : IKyrolusCommand<string>;

    private sealed class FixedSeverityValidator(KyrolusValidationSeverity severity) : IKyrolusRequestValidator<SomeCommand>
    {
        public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(SomeCommand request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(
                [new KyrolusValidationFailure(nameof(SomeCommand.Value), "hint", Severity: severity)]);
    }

    [Fact(DisplayName = "Validation: an Info-level failure does not block the request by default")]
    public async Task Validation_InfoSeverity_DoesNotBlockByDefault()
    {
        var behavior = new KyrolusValidationBehavior<SomeCommand, string>([new FixedSeverityValidator(KyrolusValidationSeverity.Info)]);

        var result = await behavior.Handle(new SomeCommand("x"), _ => Task.FromResult("ok"), CancellationToken.None);

        result.ShouldBe("ok");
    }

    [Fact(DisplayName = "Validation: an Error-level failure still blocks the request")]
    public async Task Validation_ErrorSeverity_StillBlocks()
    {
        var behavior = new KyrolusValidationBehavior<SomeCommand, string>([new FixedSeverityValidator(KyrolusValidationSeverity.Error)]);

        await Should.ThrowAsync<KyrolusValidationException>(() =>
            behavior.Handle(new SomeCommand("x"), _ => Task.FromResult("ok"), CancellationToken.None));
    }

    [Fact(DisplayName = "Validation: MinimumBlockingSeverity is configurable down to Warning")]
    public async Task Validation_MinimumBlockingSeverity_ConfigurableToWarning()
    {
        var options = new KyrolusValidationBehaviorOptions { MinimumBlockingSeverity = KyrolusValidationSeverity.Warning };
        var behavior = new KyrolusValidationBehavior<SomeCommand, string>(
            [new FixedSeverityValidator(KyrolusValidationSeverity.Warning)],
            options: options);

        await Should.ThrowAsync<KyrolusValidationException>(() =>
            behavior.Handle(new SomeCommand("x"), _ => Task.FromResult("ok"), CancellationToken.None));
    }

    #endregion
}
