namespace KyrolusSous.Mediator.Reflection.UnitTests;

public class MediatorDispacherMock : IMediatorDispatcher
{
    public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider serviceProvider, CancellationToken ct)
    {
        return Task.FromResult((TResponse)(object)"This is a test response");
    }

    public Task DispatchCommandAsync(object command, IServiceProvider serviceProvider, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
    {
        async IAsyncEnumerable<TResponse> Stream()
        {
            yield return (TResponse)(object)"This is a test stream response";
            await Task.Delay(100, ct);
            yield return (TResponse)(object)"This is the second item in the test stream response";
        }

        return Stream();
    }
}
