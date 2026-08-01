using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Mediator.Generator.TestApp;

// Every shape the generator has to emit code for. The point of this project is that it compiles:
// if the generator emits anything invalid, the build fails and says so.

// --- Query ---

public sealed record GetUser(Guid Id) : IKyrolusQuery<string>;

public sealed class GetUserHandler : IKyrolusQueryHandler<GetUser, string>
{
    public Task<string> Handle(GetUser request, CancellationToken cancellationToken)
        => Task.FromResult($"user:{request.Id}");
}

// --- Command returning a value ---

public sealed record CreateUser(string Email) : IKyrolusCommand<Guid>;

public sealed class CreateUserHandler : IKyrolusCommandHandler<CreateUser, Guid>
{
    public Task<Guid> Handle(CreateUser request, CancellationToken cancellationToken)
        => Task.FromResult(Guid.NewGuid());
}

// --- Command returning nothing ---

public sealed record DeleteUser(Guid Id) : IKyrolusCommand;

public sealed class DeleteUserHandler : IKyrolusCommandHandler<DeleteUser>
{
    public Task Handle(DeleteUser request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Plain request: neither command nor query ---

public sealed record RecalculateStats(int Year) : IKyrolusRequest<int>;

public sealed class RecalculateStatsHandler : IKyrolusRequestHandler<RecalculateStats, int>
{
    public Task<int> Handle(RecalculateStats request, CancellationToken cancellationToken)
        => Task.FromResult(request.Year);
}

// --- Stream ---

public sealed record CountTo(int Max) : IKyrolusStreamRequest<int>;

public sealed class CountToHandler : IKyrolusStreamRequestHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> Handle(
        CountTo request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= request.Max; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
    }
}

// --- One handler class serving two requests ---
// The generator must emit a separate, individually typed entry per request. A dispatcher that
// looked the Handle method up by handler type alone would call the wrong overload for the second.

public sealed record FirstRequest(int Value) : IKyrolusRequest<string>;
public sealed record SecondRequest(int Value) : IKyrolusRequest<string>;

public sealed class DualRequestHandler :
    IKyrolusRequestHandler<FirstRequest, string>,
    IKyrolusRequestHandler<SecondRequest, string>
{
    public Task<string> Handle(FirstRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"first:{request.Value}");

    public Task<string> Handle(SecondRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"second:{request.Value}");
}

// --- One request declaring two response types ---
// This is what the composite dictionary key exists for. Keyed on the request type alone, the two
// entries collide and whichever is written last silently wins.

public sealed record Ambidextrous(int Value) : IKyrolusRequest<string>, IKyrolusRequest<int>;

public sealed class AmbidextrousStringHandler : IKyrolusRequestHandler<Ambidextrous, string>
{
    public Task<string> Handle(Ambidextrous request, CancellationToken cancellationToken)
        => Task.FromResult($"text:{request.Value}");
}

public sealed class AmbidextrousIntHandler : IKyrolusRequestHandler<Ambidextrous, int>
{
    public Task<int> Handle(Ambidextrous request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value * 2);
}

// --- Notification with several handlers ---

public sealed record UserCreated(Guid Id) : INotification;

public sealed class SendWelcomeEmail : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class WriteAuditLog : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
