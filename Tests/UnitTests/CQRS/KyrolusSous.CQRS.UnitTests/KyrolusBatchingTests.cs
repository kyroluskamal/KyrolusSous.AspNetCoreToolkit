using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusBatchingTests
{
    public sealed record BulkCreateProductsCommand(IReadOnlyList<string> Items) : IBatchCommand<string, int>;

    public sealed record BulkGetUsersQuery(IReadOnlyList<int> Keys) : IBatchQuery<int, string>;

    public sealed class BulkCreateProductsHandler : IKyrolusCommandHandler<BulkCreateProductsCommand, int>
    {
        public Task<int> Handle(BulkCreateProductsCommand request, CancellationToken cancellationToken)
            => Task.FromResult(request.Items.Count);
    }

    public sealed class BulkGetUsersHandler : IKyrolusQueryHandler<BulkGetUsersQuery, IReadOnlyList<string>>
    {
        public Task<IReadOnlyList<string>> Handle(BulkGetUsersQuery request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(request.Keys.Select(k => $"User_{k}").ToList());
    }

    [Fact(DisplayName = "Batch command handler should process items correctly")]
    public async Task Batch_command_handler_should_process_items_correctly()
    {
        var handler = new BulkCreateProductsHandler();
        var cmd = new BulkCreateProductsCommand(["Laptop", "Phone", "Tablet"]);

        var count = await handler.Handle(cmd, CancellationToken.None);

        count.ShouldBe(3);
    }

    [Fact(DisplayName = "Batch query handler should retrieve items by keys")]
    public async Task Batch_query_handler_should_retrieve_items_by_keys()
    {
        var handler = new BulkGetUsersHandler();
        var query = new BulkGetUsersQuery([10, 20, 30]);

        var results = await handler.Handle(query, CancellationToken.None);

        results.Count.ShouldBe(3);
        results.ShouldContain("User_10");
        results.ShouldContain("User_20");
        results.ShouldContain("User_30");
    }
}
