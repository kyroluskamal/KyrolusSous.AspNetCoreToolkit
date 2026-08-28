using System.Security.Claims;
using KyrolusSous.CQRS.Abstractions.Audit;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusAuditBehaviorTests
{
    public sealed record AuditedTransferCommand(string From, string To, decimal Amount)
        : IKyrolusCommand<string>, IAuditableCommand
    {
        public string? AuditAction => "FundsTransfer";
        public string? AuditCategory => "Banking";
    }

    public sealed record PlainCommand(string Action) : IKyrolusCommand<string>;

    [Fact(DisplayName = "Audited command should emit successful entry to sink")]
    public async Task Audited_command_should_emit_successful_entry_to_sink()
    {
        var sink = new InMemoryAuditSink();
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
        var sink = new InMemoryAuditSink();
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
        var sink = new InMemoryAuditSink();
        var behavior = new KyrolusAuditBehavior<PlainCommand, string>(sink);

        var response = await behavior.Handle(
            new PlainCommand("do-nothing"),
            ct => Task.FromResult("done"),
            CancellationToken.None);

        response.ShouldBe("done");
        sink.Entries.ShouldBeEmpty();
    }
}
