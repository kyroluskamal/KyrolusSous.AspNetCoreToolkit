using KyrolusSous.Mediator.Generator;
using Shouldly;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class NotificationGenerationTests
{
    private const string NotificationSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Notifications;

        public record OrderPlacedEvent(int OrderId) : INotification;

        // Handler 1: Send Email
        public class SendEmailOnOrderPlacedHandler : INotificationHandler<OrderPlacedEvent>
        {
            public Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        // Handler 2: Audit Log
        public class AuditLogOnOrderPlacedHandler : INotificationHandler<OrderPlacedEvent>
        {
            public Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        // Abstract Handler (Should be ignored by Publisher Generator)
        public abstract class AbstractNotificationHandler<TNotification> : INotificationHandler<TNotification>
            where TNotification : INotification
        {
            public abstract Task Handle(TNotification notification, CancellationToken cancellationToken);
        }

        // Open Generic Handler (Should be registered in DI as open generic)
        public class GenericNotificationLogger<TNotification> : INotificationHandler<TNotification>
            where TNotification : INotification
        {
            public Task Handle(TNotification notification, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

    [Fact(DisplayName = "Notification Generation should generate NotificationDispatchSource and DI Registration binding all handlers")]
    public void NotificationGeneration_ShouldGenerateNotificationDispatchSource_WithMultipleHandlers()
    {
        // Act
        var result = GeneratorTestHost.Run(NotificationSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        // Check GeneratedNotificationDispatch
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedNotificationDispatch.g.cs");
        var dispatchCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedNotificationDispatch.g.cs"];

        dispatchCode.ShouldContain("typeof(global::MyApp.Notifications.OrderPlacedEvent)");
        dispatchCode.ShouldContain("Bind<global::MyApp.Notifications.OrderPlacedEvent>");

        // Check GeneratedNotificationHandlersDI
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedNotificationHandlersDI.g.cs");
        var diCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedNotificationHandlersDI.g.cs"];
        diCode.ShouldContain("SendEmailOnOrderPlacedHandler");
        diCode.ShouldContain("AuditLogOnOrderPlacedHandler");
        diCode.ShouldContain("typeof(global::MyApp.Notifications.GenericNotificationLogger<>)");
        diCode.ShouldNotContain("AbstractNotificationHandler");
    }

    private const string NestedNotificationSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace OuterNamespace;

        public class EnclosingClass
        {
            public record NestedEvent(int[] Items) : INotification;

            public class NestedEventHandler : INotificationHandler<NestedEvent>
            {
                public Task Handle(NestedEvent notification, CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
        }
        """;

    [Fact(DisplayName = "Notification Generation should collect namespaces for nested classes and array types")]
    public void NotificationWithNestedClassAndArray_ShouldCollectNamespaces()
    {
        // Act
        var result = GeneratorTestHost.Run(NestedNotificationSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedNotificationDispatch.g.cs");
    }
}
