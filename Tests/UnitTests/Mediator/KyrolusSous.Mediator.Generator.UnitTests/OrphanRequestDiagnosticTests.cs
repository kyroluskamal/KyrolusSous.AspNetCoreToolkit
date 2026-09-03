using KyrolusSous.Mediator.Generator;
using Microsoft.CodeAnalysis;
using Shouldly;

namespace KyrolusSous.Mediator.Generator.UnitTests;

/// <summary>
/// SMG005: a request declared and sent through the mediator in this project, with no handler for
/// it anywhere in this project either.
/// </summary>
public sealed class OrphanRequestDiagnosticTests
{
    [Fact(DisplayName = "SMG005: a request declared and sent in this project with no handler is reported")]
    public void OrphanRequest_DeclaredAndSent_NoHandler_ReportsSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record DeleteUser(int Id) : IKyrolusCommand;

            public class Caller(IKyrolusMediatorSender sender)
            {
                public Task RunAsync(CancellationToken ct) => sender.SendAsync(new DeleteUser(1), ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG005" && d.Severity == DiagnosticSeverity.Warning);
        var diagnostic = result.GeneratorDiagnostics.Single(d => d.Id == "SMG005");
        diagnostic.GetMessage().ShouldContain("DeleteUser");
        diagnostic.Location.ShouldNotBe(Location.None);
    }

    [Fact(DisplayName = "SMG005: a request declared and sent in this project with a handler is not reported")]
    public void OrphanRequest_DeclaredAndSent_WithHandler_DoesNotReportSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record DeleteUser(int Id) : IKyrolusCommand;

            public class DeleteUserHandler : IKyrolusCommandHandler<DeleteUser>
            {
                public Task Handle(DeleteUser request, CancellationToken cancellationToken) => Task.CompletedTask;
            }

            public class Caller(IKyrolusMediatorSender sender)
            {
                public Task RunAsync(CancellationToken ct) => sender.SendAsync(new DeleteUser(1), ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldNotContain(d => d.Id == "SMG005");
    }

    [Fact(DisplayName = "SMG005: a request that is declared but never sent is not reported")]
    public void OrphanRequest_DeclaredButNeverSent_DoesNotReportSMG005()
    {
        const string source = """
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record UnusedCommand(int Id) : IKyrolusCommand;
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldNotContain(d => d.Id == "SMG005");
    }

    [Fact(DisplayName = "SMG005: an open generic handler anywhere in the project suppresses the warning")]
    public void OrphanRequest_WithOpenGenericHandlerPresent_DoesNotReportSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record DeleteUser(int Id) : IKyrolusCommand;

            public class CatchAllHandler<TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>
                where TRequest : IKyrolusRequest<TResponse>
            {
                public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken) => Task.FromResult<TResponse>(default!);
            }

            public class Caller(IKyrolusMediatorSender sender)
            {
                public Task RunAsync(CancellationToken ct) => sender.SendAsync(new DeleteUser(1), ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldNotContain(d => d.Id == "SMG005");
    }

    [Fact(DisplayName = "SMG005: the MediatR-compat Send(...) extension method is also detected")]
    public void OrphanRequest_ViaMediatRCompatSend_ReportsSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;
            using KyrolusSous.Mediator.Abstractions.Compatibility;

            namespace MyApp.Orphans;

            public record DeleteUser(int Id) : IKyrolusCommand;

            public class Caller(IKyrolusMediatorSender sender)
            {
                public Task RunAsync(CancellationToken ct) => sender.Send(new DeleteUser(1), ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG005");
    }

    [Fact(DisplayName = "SMG005: sending through the combined IKyrolusMediator facade is also detected")]
    public void OrphanRequest_ViaIKyrolusMediator_ReportsSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record DeleteUser(int Id) : IKyrolusCommand;

            public class Caller(IKyrolusMediator mediator)
            {
                public Task RunAsync(CancellationToken ct) => mediator.SendAsync(new DeleteUser(1), ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG005");
    }

    [Fact(DisplayName = "SMG005: a query sent through the mediator with no handler is reported")]
    public void OrphanQuery_DeclaredAndSent_NoHandler_ReportsSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record GetUser(int Id) : IKyrolusQuery<string>;

            public class Caller(IKyrolusMediatorSender sender)
            {
                public Task<string> RunAsync(CancellationToken ct) => sender.SendAsync(new GetUser(1), ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG005" && d.GetMessage().Contains("GetUser"));
    }

    [Fact(DisplayName = "SMG005: sending a variable of an interface type names nothing concrete, so nothing is reported")]
    public void OrphanRequest_SentAsInterfaceTypedVariable_DoesNotReportSMG005()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using KyrolusSous.Mediator.Abstractions.Interfaces;

            namespace MyApp.Orphans;

            public record DeleteUser(int Id) : IKyrolusCommand;

            public class Caller(IKyrolusMediatorSender sender)
            {
                public Task RunAsync(IKyrolusCommand command, CancellationToken ct) => sender.SendAsync(command, ct);
            }
            """;

        var result = GeneratorTestHost.Run(source);

        result.GeneratorDiagnostics.ShouldNotContain(d => d.Id == "SMG005");
    }
}
