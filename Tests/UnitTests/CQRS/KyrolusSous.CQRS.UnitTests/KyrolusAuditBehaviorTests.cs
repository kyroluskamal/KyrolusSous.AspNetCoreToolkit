using System.Globalization;
using System.Security.Claims;
using KyrolusSous.CQRS.Abstractions.Audit;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Localization.Abstractions;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusAuditBehaviorTests
{
    public sealed record AuditedTransferCommand(string From, string To, decimal Amount)
        : IKyrolusCommand<string>, IKyrolusAuditableCommand
    {
        public string? AuditAction => "FundsTransfer";
        public string? AuditCategory => "Banking";
    }

    public sealed record PlainCommand(string Action) : IKyrolusCommand<string>;

    public sealed record PaymentDetails(string CardNumber, string Holder);
    public sealed record CheckoutCommand(string OrderId, PaymentDetails Payment, List<PaymentDetails> BackupCards)
        : IKyrolusCommand<string>, IKyrolusAuditableCommand;

    [Fact(DisplayName = "Audit redaction recurses into nested objects and collections, not just top-level properties")]
    public async Task Audit_redaction_recurses_into_nested_properties()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var behavior = new KyrolusAuditBehavior<CheckoutCommand, string>(sink);

        await behavior.Handle(
            new CheckoutCommand(
                "ord-1",
                new PaymentDetails("4111111111111111", "Alice"),
                [new PaymentDetails("5500000000000004", "Alice")]),
            ct => Task.FromResult("ok"),
            CancellationToken.None);

        sink.Entries.Count.ShouldBe(1);
        var payload = sink.Entries.First().Payload.ShouldBeOfType<Dictionary<string, object?>>();

        var nestedPayment = payload["Payment"].ShouldBeOfType<Dictionary<string, object?>>();
        nestedPayment["CardNumber"].ShouldBe("***REDACTED***");
        nestedPayment["Holder"].ShouldBe("Alice"); // not sensitive, must survive

        var backupCards = payload["BackupCards"].ShouldBeOfType<List<object?>>();
        var firstBackup = backupCards.Single().ShouldBeOfType<Dictionary<string, object?>>();
        firstBackup["CardNumber"].ShouldBe("***REDACTED***");
    }

    [Fact(DisplayName = "Audited command should emit successful entry to sink")]
    public async Task Audited_command_should_emit_successful_entry_to_sink()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "bank-user-1"),
            new Claim(ClaimTypes.Name, "Bob"),
            new Claim("tenant_id", "tenant-alpha")
        ], "TestAuth");

        var userContext = new KyrolusDefaultCurrentUserContext(new ClaimsPrincipal(identity));
        var behavior = new KyrolusAuditBehavior<AuditedTransferCommand, string>(sink, userContext);

        var response = await behavior.Handle(
            new AuditedTransferCommand("Acc1", "Acc2", 500m),
            ct => Task.FromResult("Transferred"),
            CancellationToken.None);

        response.ShouldBe("Transferred");
        sink.Entries.Count.ShouldBe(1);

        var entry = sink.Entries.First();
        entry.Action.ShouldBe("FundsTransfer");
        entry.Category.ShouldBe("Banking");
        entry.UserId.ShouldBe("bank-user-1");
        entry.UserName.ShouldBe("Bob");
        entry.TenantId.ShouldBe("tenant-alpha");
        entry.IsSuccess.ShouldBeTrue();
        entry.ErrorMessage.ShouldBeNull();
        entry.Payload.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Audited command failure should emit failed entry and rethrow")]
    public async Task Audited_command_failure_should_emit_failed_entry_and_rethrow()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var userContext = new KyrolusDefaultCurrentUserContext();
        var behavior = new KyrolusAuditBehavior<AuditedTransferCommand, string>(sink, userContext);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new AuditedTransferCommand("Acc1", "Acc2", 500m),
                ct => throw new InvalidOperationException("Insufficient funds"),
                CancellationToken.None));

        sink.Entries.Count.ShouldBe(1);
        var entry = sink.Entries.First();
        entry.IsSuccess.ShouldBeFalse();
        entry.ErrorMessage.ShouldBe("Insufficient funds");
    }

    [Fact(DisplayName = "Non auditable command should not emit entries")]
    public async Task Non_auditable_command_should_not_emit_entries()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var behavior = new KyrolusAuditBehavior<PlainCommand, string>(sink);

        var response = await behavior.Handle(
            new PlainCommand("do-nothing"),
            ct => Task.FromResult("done"),
            CancellationToken.None);

        response.ShouldBe("done");
        sink.Entries.ShouldBeEmpty();
    }

    public sealed record LocalizedPayrollCommand(decimal Amount, int EmployeeCount)
        : IKyrolusCommand<string>, IKyrolusAuditableCommand
    {
        public string? AuditAction => "PayrollCommand";
        public string? BusinessAction => "Payroll.Disburse";
        public object? BusinessActionArgs => new Dictionary<string, object?> { ["count"] = EmployeeCount };
    }

    private sealed class TestLocalizer : IKyrolusLocalizer
    {
        public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
        {
            if (key == "Payroll.Disburse")
                return new KyrolusLocalizationResult("صرف الرواتب الشهرية", ResourceNotFound: false);

            return new KyrolusLocalizationResult(key, ResourceNotFound: true);
        }

        public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
        {
            if (key == "Payroll.Disburse")
            {
                var formatted = Format("صرف الرواتب لعدد {count} موظف", arguments);
                return new KyrolusLocalizationResult(formatted, ResourceNotFound: false);
            }

            return new KyrolusLocalizationResult(key, ResourceNotFound: true);
        }

        public string Format(string template, object? arguments) => KyrolusLocalizationFormatter.Format(template, arguments);
    }

    [Fact(DisplayName = "Audited command populates Action, BusinessAction, and LocalizedAction correctly with localizer")]
    public async Task Audited_command_populates_action_businessaction_and_localizedaction()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var localizer = new TestLocalizer();
        var behavior = new KyrolusAuditBehavior<LocalizedPayrollCommand, string>(sink, localizer: localizer);

        var response = await behavior.Handle(
            new LocalizedPayrollCommand(50000m, 25),
            ct => Task.FromResult("Disbursed"),
            CancellationToken.None);

        response.ShouldBe("Disbursed");
        sink.Entries.Count.ShouldBe(1);

        var entry = sink.Entries.First();
        entry.Action.ShouldBe("PayrollCommand"); // Tech action preserved
        entry.BusinessAction.ShouldBe("Payroll.Disburse"); // Business code preserved
        entry.LocalizedAction.ShouldBe("صرف الرواتب لعدد 25 موظف"); // Localized text evaluated
        entry.CommandName.ShouldBe("LocalizedPayrollCommand");
        entry.IsSuccess.ShouldBeTrue();
    }
}
