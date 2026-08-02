using KyrolusSous.Mediator.Generator;
using Shouldly;
using System;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class ExceptionHandlingGenerationTests
{
    private const string ExceptionSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Exceptions;

        public record MyFailingQuery(int Id) : IKyrolusQuery<string>;

        public class MyFailingQueryHandler : IKyrolusQueryHandler<MyFailingQuery, string>
        {
            public Task<string> Handle(MyFailingQuery request, CancellationToken cancellationToken)
                => throw new InvalidOperationException("Failed");
        }

        // Exception Action
        public class LogExceptionAction : IKyrolusRequestExceptionAction<MyFailingQuery, InvalidOperationException>
        {
            public Task Execute(MyFailingQuery request, InvalidOperationException exception, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        // Exception Handler
        public class FallbackExceptionHandler : IKyrolusRequestExceptionHandler<MyFailingQuery, InvalidOperationException, string>
        {
            public Task Handle(MyFailingQuery request, InvalidOperationException exception, KyrolusRequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
            {
                state.SetHandled("FallbackResult");
                return Task.CompletedTask;
            }
        }
        """;

    [Fact(DisplayName = "Exception Generation should create GeneratedExceptionDispatch with actions and handlers bound")]
    public void ExceptionGeneration_ShouldGenerateExceptionDispatchSource_WithActionsAndHandlers()
    {
        // Act
        var result = GeneratorTestHost.Run(ExceptionSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedExceptionDispatch.g.cs");
        var exceptionCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedExceptionDispatch.g.cs"];

        exceptionCode.ShouldContain("typeof(global::MyApp.Exceptions.MyFailingQuery)");
        exceptionCode.ShouldContain("typeof(global::System.InvalidOperationException)");
    }
}
