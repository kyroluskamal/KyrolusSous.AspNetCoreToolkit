using KyrolusSous.Mediator.Generator;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class CommandGenerationTests
{
    private const string CommandSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Commands;

        // Command returning Unit (no return value)
        public record CreateUserCommand(string Name) : IKyrolusCommand;

        public class CreateUserCommandHandler : IKyrolusCommandHandler<CreateUserCommand>
        {
            public Task Handle(CreateUserCommand request, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        // Command returning a response (Guid)
        public record CreateOrderCommand(decimal Amount) : IKyrolusCommand<Guid>;

        public class CreateOrderCommandHandler : IKyrolusCommandHandler<CreateOrderCommand, Guid>
        {
            public Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Guid.NewGuid());
            }
        }
        """;

    [Fact(DisplayName = "Command Generation should handle Unit commands and typed response commands correctly")]
    public void CommandGeneration_ShouldGenerateDispatcherAndWrappers_ForUnitAndTypedCommands()
    {
        // Act
        var result = GeneratorTestHost.Run(CommandSource);

        // Assert: Compilation clean
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        // Assert: Dispatcher content for Unit command & Typed command
        var dispatcherCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"];
        dispatcherCode.ShouldContain("typeof(global::MyApp.Commands.CreateUserCommand)");
        dispatcherCode.ShouldContain("typeof(global::MyApp.Commands.CreateOrderCommand)");

        // Assert: Generated DI extensions register both handlers
        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];
        handlersDiCode.ShouldContain("services.TryAddTransient<global::MyApp.Commands.CreateUserCommandHandler>()");
        handlersDiCode.ShouldContain("services.TryAddTransient<global::MyApp.Commands.CreateOrderCommandHandler>()");
    }
}
