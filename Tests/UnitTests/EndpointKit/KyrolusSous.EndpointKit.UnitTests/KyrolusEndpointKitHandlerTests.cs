using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.EF.Command.Remove;
using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.Runtime;
using KyrolusSous.ExceptionHandling.Runtime.Interfaces;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointKitHandlerTests
{
    public sealed class TestItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public sealed class TestItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler: Integrates with IKyrolusErrorResponseWriter on failure without duplicating code")]
    public async Task Handler_Should_Integrate_With_ExceptionHandling_ErrorResponseWriter()
    {
        var services = new ServiceCollection();
        var mapper = Substitute.For<IKyrolusMapper>();
        var mediator = Substitute.For<IKyrolusMediatorSender>();
        var config = new KyrolusEfApiConfig<TestItemDto> { Route = "items" };
        var errorWriter = Substitute.For<IKyrolusErrorResponseWriter>();

        services.AddSingleton(errorWriter);
        var sp = services.BuildServiceProvider();

        var handler = new DefaultCommandQueryHandler<TestItemDto, TestItem, int>(
            mapper,
            mediator,
            config,
            sp);

        handler.ShouldNotBeNull();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler: HandleGetById sends CQRS Query via Mediator")]
    public async Task Handler_HandleGetById_Should_Send_CQRS_Query()
    {
        var services = new ServiceCollection();
        var mapper = Substitute.For<IKyrolusMapper>();
        var mediator = Substitute.For<IKyrolusMediatorSender>();
        var config = new KyrolusEfApiConfig<TestItemDto>
        {
            Route = "items",
            QueryById = new GetByIdQuery<TestItemDto, int>(42)
        };

        mediator.SendAsync<TestItemDto?>(Arg.Any<IKyrolusQuery<TestItemDto?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TestItemDto?>(new TestItemDto { Id = 42, Title = "Sample" }));

        var sp = services.BuildServiceProvider();

        var handler = new DefaultCommandQueryHandler<TestItemDto, TestItem, int>(
            mapper,
            mediator,
            config,
            sp);

        var result = await handler.HandleGetByIdAsync(42);
        result.ShouldNotBeNull();
        await mediator.Received(1).SendAsync<TestItemDto?>(Arg.Any<IKyrolusQuery<TestItemDto?>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler: HandleRemoveAsync sends CQRS Delete Command")]
    public async Task Handler_HandleRemove_Should_Send_Delete_Command()
    {
        var services = new ServiceCollection();
        var mapper = Substitute.For<IKyrolusMapper>();
        var mediator = Substitute.For<IKyrolusMediatorSender>();
        var config = new KyrolusEfApiConfig<TestItemDto>
        {
            Route = "items",
            QueryById = new GetByIdQuery<TestItemDto, int>(42),
            RemoveCommand = new RemoveByIdCommand<TestItemDto, int>([42]),
            UseSoftDeleteForDelete = false
        };

        mediator.SendAsync<TestItemDto?>(Arg.Any<IKyrolusQuery<TestItemDto?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TestItemDto?>(new TestItemDto { Id = 42, Title = "Sample" }));

        mediator.SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(true));

        var sp = services.BuildServiceProvider();

        var handler = new DefaultCommandQueryHandler<TestItemDto, TestItem, int>(
            mapper,
            mediator,
            config,
            sp);

        var result = await handler.HandleRemoveAsync(42);
        result.ShouldNotBeNull();
    }
}
