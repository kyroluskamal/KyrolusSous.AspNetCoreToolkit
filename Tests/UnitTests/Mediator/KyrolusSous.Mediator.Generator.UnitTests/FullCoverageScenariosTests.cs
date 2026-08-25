using KyrolusSous.Mediator.Generator;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class FullCoverageScenariosTests
{
    // 1. Single class implementing multiple handler interfaces
    private const string MultiInterfaceSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Multi;

        public record MultiQuery(int Id) : IKyrolusQuery<string>;
        public record MultiCommand(string Name) : IKyrolusCommand<Guid>;

        public class DualHandler : IKyrolusQueryHandler<MultiQuery, string>, IKyrolusCommandHandler<MultiCommand, Guid>
        {
            public Task<string> Handle(MultiQuery request, CancellationToken cancellationToken)
                => Task.FromResult("Result");

            public Task<Guid> Handle(MultiCommand request, CancellationToken cancellationToken)
                => Task.FromResult(Guid.NewGuid());
        }
        """;

    [Fact(DisplayName = "Multi Interface: Class implementing query and command handlers should register both")]
    public void MultiInterface_ClassImplementingQueryAndCommand_ShouldRegisterBoth()
    {
        var result = GeneratorTestHost.Run(MultiInterfaceSource);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var dispatcherCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"];
        dispatcherCode.ShouldContain("typeof(global::MyApp.Multi.MultiQuery)");
        dispatcherCode.ShouldContain("typeof(global::MyApp.Multi.MultiCommand)");
    }

    // 2. Types in Global Namespace (No namespace keyword)
    private const string GlobalNamespaceSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        public record GlobalQuery(int Id) : IKyrolusQuery<int>;

        public class GlobalQueryHandler : IKyrolusQueryHandler<GlobalQuery, int>
        {
            public Task<int> Handle(GlobalQuery request, CancellationToken cancellationToken)
                => Task.FromResult(request.Id * 2);
        }

        public record GlobalEvent(string Message) : IKyrolusNotification;

        public class GlobalEventHandler : IKyrolusNotificationHandler<GlobalEvent>
        {
            public Task Handle(GlobalEvent notification, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

    [Fact(DisplayName = "Global Namespace: Types in global namespace should generate valid global type references")]
    public void GlobalNamespace_TypesWithoutNamespace_ShouldBeHandledCorrectly()
    {
        var result = GeneratorTestHost.Run(GlobalNamespaceSource);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var dispatcherCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"];
        dispatcherCode.ShouldContain("typeof(global::GlobalQuery)");

        var notificationCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedNotificationDispatch.g.cs"];
        notificationCode.ShouldContain("typeof(global::GlobalEvent)");
    }

    // 3. Class implementing multiple notifications
    private const string MultiNotificationSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.MultiEvents;

        public record EventA(int Id) : IKyrolusNotification;
        public record EventB(string Name) : IKyrolusNotification;

        public class MultiEventHandler : IKyrolusNotificationHandler<EventA>, IKyrolusNotificationHandler<EventB>
        {
            public Task Handle(EventA notification, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task Handle(EventB notification, CancellationToken cancellationToken) => Task.CompletedTask;
        }
        """;

    [Fact(DisplayName = "Multi Notification: Single handler class listening to multiple events should bind both")]
    public void MultiNotification_SingleHandlerClassListeningToMultipleEvents_ShouldBindBoth()
    {
        var result = GeneratorTestHost.Run(MultiNotificationSource);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var dispatchCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedNotificationDispatch.g.cs"];
        dispatchCode.ShouldContain("typeof(global::MyApp.MultiEvents.EventA)");
        dispatchCode.ShouldContain("typeof(global::MyApp.MultiEvents.EventB)");
    }
}
