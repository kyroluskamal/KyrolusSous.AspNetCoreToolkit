

namespace KyrolusSous.Mediator.Reflection.UnitTests;
#region Request mesages
internal class TestRequest : IKyrolusRequest<string>
{
    public string Value { get; set; } = "This is a test request";
}
internal class TestRequestHandler : IKyrolusRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest request, CancellationToken cancellationToken) => Task.FromResult(request.Value);

}
#endregion
#region Stream messages
internal class TestStream : IKyrolusStreamRequest<string>
{
    public string Value { get; set; } = "This is a test stream";
}

internal class TestStreamHandler : IKyrolusStreamRequestHandler<TestStream, string>
{
    public async IAsyncEnumerable<string> Handle(TestStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return request.Value;
        await Task.Delay(100, cancellationToken);
        yield return "This is the second item in the stream";
    }
}
#endregion
#region Command messages
internal class TestCommand : IKyrolusCommand
{
    public string Value { get; set; } = "This is a test command";
}

internal class TestCommandHandler : IKyrolusCommandHandler<TestCommand>
{
    public Task Handle(TestCommand command, CancellationToken cancellationToken) => Task.FromResult(command.Value);
}

internal class TestCommandWithRespone : IKyrolusCommand<string>
{
    public string Value { get; set; } = "This is a test command with response";
}
internal class TestCommandWithResponeHandler : IKyrolusCommandHandler<TestCommandWithRespone, string>
{
    public Task<string> Handle(TestCommandWithRespone command, CancellationToken cancellationToken) => Task.FromResult(command.Value);
}

internal class TestCommand_Without_Respone_And_IKyrolusRequest : IKyrolusRequest
{
    public string Value { get; set; } = "This is a test command without response";
}
internal class TestCommand_Without_Respone_And_IKyrolusRequest_Handler : IKyrolusRequestHandler<TestCommand_Without_Respone_And_IKyrolusRequest>
{
    public Task Handle(TestCommand_Without_Respone_And_IKyrolusRequest request, CancellationToken cancellationToken)
    => Task.FromResult(request.Value);
}
internal class TestCommand_Without_Respone_And_IKyrolusRequest_unit : IKyrolusRequest<Unit>
{
    public string Value { get; set; } = "This is a test command without response";
}
internal class TestCommand_Without_Respone_And_IKyrolusRequest_unit_Handler : IKyrolusRequestHandler<TestCommand_Without_Respone_And_IKyrolusRequest_unit>
{
    public Task Handle(TestCommand_Without_Respone_And_IKyrolusRequest_unit request, CancellationToken cancellationToken)
    => Task.FromResult(request.Value);
}

internal class TestCommand_With_Respone_And_IKyrolusRequest : IKyrolusRequest<string>
{
    public string Value { get; set; } = "This is a test command with response and IKyrolusRequest";
}
internal class TestCommand_With_Respone_And_IKyrolusRequest_Handler : IKyrolusRequestHandler<TestCommand_With_Respone_And_IKyrolusRequest, string>
{
    public Task<string> Handle(TestCommand_With_Respone_And_IKyrolusRequest request, CancellationToken cancellationToken)
    => Task.FromResult(request.Value);
}
#endregion
internal class TestQuery : IKyrolusQuery<string>
{
    public string Value { get; set; } = "This is a test Query";
}

internal class TestQueryHandler : IKyrolusQueryHandler<TestQuery, string>
{
    public Task<string> Handle(TestQuery query, CancellationToken cancellationToken) => Task.FromResult(query.Value);
}
internal class TestQueryHandlerWithRequestHandler : IKyrolusRequestHandler<TestQuery, string>
{
    public Task<string> Handle(TestQuery query, CancellationToken cancellationToken) => Task.FromResult(query.Value);
}

internal class ExplicitHandleRequest : IKyrolusRequest<string> { }

internal class ExplicitHandleRequestHandler : IKyrolusRequestHandler<ExplicitHandleRequest, string>
{
    Task<string> IKyrolusRequestHandler<ExplicitHandleRequest, string>.Handle(ExplicitHandleRequest request, CancellationToken cancellationToken)
        => Task.FromResult("Explicit");
}

internal class ThrowingRequest : IKyrolusRequest<string> { }

internal class ThrowingRequestHandler : IKyrolusRequestHandler<ThrowingRequest, string>
{
    public Task<string> Handle(ThrowingRequest request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Custom Handler Error");
}

