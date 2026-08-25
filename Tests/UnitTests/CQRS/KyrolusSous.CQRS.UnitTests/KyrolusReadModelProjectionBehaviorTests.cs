using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Projections;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusReadModelProjectionBehaviorTests
{
    public sealed record ProductReadModel(int Id, string Name, decimal Price);

    public sealed record UpdateProductCommand(int Id, string Name, decimal Price)
        : IKyrolusCommand<int>, IProjectableCommand<ProductReadModel>
    {
        public ProductReadModel? ToReadModel() => new(Id, Name, Price);
    }

    public sealed record PlainCommand(string Text) : IKyrolusCommand<string>;

    private sealed class FakeProductProjector : IReadModelProjector<ProductReadModel>
    {
        public List<ProductReadModel> ProjectedModels { get; } = [];

        public Task ProjectAsync(ProductReadModel model, CancellationToken cancellationToken = default)
        {
            ProjectedModels.Add(model);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingProjector : IReadModelProjector<ProductReadModel>
    {
        public Task ProjectAsync(ProductReadModel model, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection sync failed");
    }

    [Fact]
    public async Task Projectable_command_should_invoke_projector_upon_success()
    {
        var services = new ServiceCollection();
        var projector = new FakeProductProjector();
        services.AddSingleton<IReadModelProjector<ProductReadModel>>(projector);
        var sp = services.BuildServiceProvider();

        var behavior = new KyrolusReadModelProjectionBehavior<UpdateProductCommand, int>(sp);
        var cmd = new UpdateProductCommand(10, "Tablet", 500m);

        var result = await behavior.Handle(cmd, ct => Task.FromResult(10), CancellationToken.None);

        result.ShouldBe(10);
        projector.ProjectedModels.Count.ShouldBe(1);
        projector.ProjectedModels[0].Id.ShouldBe(10);
        projector.ProjectedModels[0].Name.ShouldBe("Tablet");
    }

    [Fact]
    public async Task Projector_exception_should_be_isolated_and_not_throw()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReadModelProjector<ProductReadModel>>(new ThrowingProjector());
        var sp = services.BuildServiceProvider();

        var behavior = new KyrolusReadModelProjectionBehavior<UpdateProductCommand, int>(sp);
        var cmd = new UpdateProductCommand(10, "Tablet", 500m);

        var result = await behavior.Handle(cmd, ct => Task.FromResult(10), CancellationToken.None);

        result.ShouldBe(10);
    }

    [Fact]
    public async Task Plain_command_should_not_invoke_projectors()
    {
        var services = new ServiceCollection();
        var projector = new FakeProductProjector();
        services.AddSingleton<IReadModelProjector<ProductReadModel>>(projector);
        var sp = services.BuildServiceProvider();

        var behavior = new KyrolusReadModelProjectionBehavior<PlainCommand, string>(sp);
        var cmd = new PlainCommand("test");

        var result = await behavior.Handle(cmd, ct => Task.FromResult("ok"), CancellationToken.None);

        result.ShouldBe("ok");
        projector.ProjectedModels.ShouldBeEmpty();
    }
}
