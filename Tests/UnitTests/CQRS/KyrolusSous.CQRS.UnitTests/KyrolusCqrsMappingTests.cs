using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Mapping;
using KyrolusSous.CQRS.Mapping.Behaviors;
using KyrolusSous.CQRS.Mapping.Contracts;
using KyrolusSous.CQRS.Mapping.Extensions;
using KyrolusSous.CQRS.Mapping.Handlers;
using KyrolusSous.Mapping.Abstractions;
using KyrolusSous.Mapping.Abstractions.Context;
using KyrolusSous.Mapping.Abstractions.Contracts;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusCqrsMappingTests
{
    // Sample models for testing
    public record CreateUserCommand(string Username, string Email) : IKyrolusMappedCommand<UserEntity>;

    public sealed class AllowListedUserCommand : IKyrolusMappedCommand<UserEntity>
    {
        public AllowListedUserCommand(string username, string email, bool isAdmin, bool allowIsAdmin = false)
        {
            Username = username;
            Email = email;
            IsAdmin = isAdmin;
            AllowedProperties = allowIsAdmin
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Username", "Email", "IsAdmin" }
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Username", "Email" };
        }

        public string Username { get; }
        public string Email { get; }
        public bool IsAdmin { get; }
        public IReadOnlySet<string>? AllowedProperties { get; }
    }

    public record UserEntity { public Guid Id { get; set; } public string Username { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; }
    public record UserDto(Guid Id, string Username, string Email);

    public record UserQuery(Guid Id) : IKyrolusMappedQuery<UserEntity, UserDto>;
    public record UserExistsQuery(Guid Id) : IKyrolusMappedQuery<UserEntity, bool>;
    public record UserExistsNullableQuery(Guid Id) : IKyrolusMappedQuery<UserEntity, bool?>;
    public record UserPagedQuery(int Page, int Size) : IKyrolusMappedPagedQuery<UserEntity, UserDto>;
    public record UserSeekQuery(int Size, string? Cursor = null) : IKyrolusMappedSeekQuery<UserEntity, UserDto>;
    public record UserListQuery : IKyrolusMappedListQuery<UserEntity, UserDto>;

    // ==========================================
    // 1. Dependency Injection Registration Tests
    // ==========================================

    [Fact(DisplayName = "AddKyrolusCqrsMapping registers pipeline behavior")]
    public void AddKyrolusCqrsMapping_RegistersBehavior()
    {
        var services = new ServiceCollection();
        services.AddKyrolusCqrsMapping();

        var provider = services.BuildServiceProvider();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<,>));
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(KyrolusMappingPipelineBehavior<,>));
    }

    [Fact(DisplayName = "AddKyrolusCqrsMapping with instance registers IKyrolusObjectMapper singleton")]
    public void AddKyrolusCqrsMapping_WithInstance_RegistersSingleton()
    {
        var services = new ServiceCollection();
        var mockMapper = Substitute.For<IKyrolusObjectMapper>();

        services.AddKyrolusCqrsMapping(mockMapper);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IKyrolusObjectMapper>();
        resolved.ShouldBeSameAs(mockMapper);
    }

    [Fact(DisplayName = "AddKyrolusCqrsMapping with factory registers IKyrolusObjectMapper")]
    public void AddKyrolusCqrsMapping_WithFactory_RegistersMapper()
    {
        var services = new ServiceCollection();
        var mockMapper = Substitute.For<IKyrolusObjectMapper>();

        services.AddKyrolusCqrsMapping(sp => mockMapper);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IKyrolusObjectMapper>();
        resolved.ShouldBeSameAs(mockMapper);
    }

    // ==========================================
    // 2. Command to Entity Extension Tests
    // ==========================================

    [Fact(DisplayName = "ToEntity maps command implementing IKyrolusMapTo")]
    public void ToEntity_WithMapTo_MapsCorrectly()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var command = new CreateUserCommand("kyrolus", "kyrolus@example.com");
        var expectedEntity = new UserEntity { Id = Guid.NewGuid(), Username = "kyrolus", Email = "kyrolus@example.com" };

        mapper.Map<UserEntity>(command).Returns(expectedEntity);

        var result = command.ToEntity(mapper);

        result.ShouldBeSameAs(expectedEntity);
        mapper.Received(1).Map<UserEntity>(command);
    }

    [Fact(DisplayName = "ToEntity maps generic command object")]
    public void ToEntity_GenericObject_MapsCorrectly()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var rawCommand = new { Username = "kyrolus", Email = "test@example.com" };
        var expectedEntity = new UserEntity { Id = Guid.NewGuid(), Username = "kyrolus" };

        mapper.Map<UserEntity>(rawCommand).Returns(expectedEntity);

        var result = rawCommand.ToEntity<UserEntity>(mapper);

        result.ShouldBeSameAs(expectedEntity);
        mapper.Received(1).Map<UserEntity>(rawCommand);
    }

    [Fact(DisplayName = "ApplyTo applies command mutations onto existing entity")]
    public void ApplyTo_AppliesChangesOntoEntity()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var command = new CreateUserCommand("updatedUser", "updated@example.com");
        var existingEntity = new UserEntity { Id = Guid.NewGuid(), Username = "oldUser", Email = "old@example.com" };

        // ApplyTo always routes through the context-accepting overload so it can default null-safe
        // in-place mapping on for its own call (see KyrolusMappingContext.IgnoreNullValuesOnInPlaceMapKey),
        // even when the caller passes no context of its own.
        mapper.Map(command, existingEntity, Arg.Any<KyrolusMappingContext>()).Returns(existingEntity);

        var result = command.ApplyTo(existingEntity, mapper);

        result.ShouldBeSameAs(existingEntity);
        mapper.Received(1).Map(command, existingEntity, Arg.Any<KyrolusMappingContext>());
    }

    [Fact(DisplayName = "ApplyTo with AllowedProperties rejects a disallowed command property")]
    public void ApplyTo_WithAllowedProperties_RejectsDisallowedProperty()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var command = new AllowListedUserCommand("newUser", "new@example.com", isAdmin: true);
        var existingEntity = new UserEntity { Id = Guid.NewGuid(), Username = "oldUser", Email = "old@example.com" };

        var ex = Should.Throw<KyrolusSecurityException>(() => command.ApplyTo(existingEntity, mapper));
        ex.Message.ShouldContain("IsAdmin");
        ex.Message.ShouldContain(nameof(AllowListedUserCommand));

        mapper.DidNotReceive().Map(Arg.Any<AllowListedUserCommand>(), Arg.Any<UserEntity>(), Arg.Any<KyrolusMappingContext>());
    }

    [Fact(DisplayName = "ApplyTo with AllowedProperties permits only listed command properties")]
    public void ApplyTo_WithAllowedProperties_PermitsListedProperties()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var command = new AllowListedUserCommand("newUser", "new@example.com", isAdmin: false, allowIsAdmin: true);
        var existingEntity = new UserEntity { Id = Guid.NewGuid(), Username = "oldUser", Email = "old@example.com" };

        mapper.Map(command, existingEntity, Arg.Any<KyrolusMappingContext>()).Returns(existingEntity);

        var result = command.ApplyTo(existingEntity, mapper);

        result.ShouldBeSameAs(existingEntity);
    }

    [Fact(DisplayName = "ApplyTo with AllowedProperties null behaves exactly as unrestricted")]
    public void ApplyTo_WithNullAllowedProperties_IsUnrestricted()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var command = new CreateUserCommand("updatedUser", "updated@example.com");
        var existingEntity = new UserEntity { Id = Guid.NewGuid(), Username = "oldUser", Email = "old@example.com" };

        mapper.Map(command, existingEntity, Arg.Any<KyrolusMappingContext>()).Returns(existingEntity);

        Should.NotThrow(() => command.ApplyTo(existingEntity, mapper));
    }

    // ==========================================
    // 3. Result Mapping Extension Tests
    // ==========================================

    [Fact(DisplayName = "PagedResult.MapTo converts items and preserves metadata")]
    public void PagedResult_MapTo_ConvertsItemsAndPreservesMetadata()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity1 = new UserEntity { Id = Guid.NewGuid(), Username = "u1" };
        var entity2 = new UserEntity { Id = Guid.NewGuid(), Username = "u2" };
        var sourcePaged = new KyrolusPagedResult<UserEntity>([entity1, entity2], TotalCount: 10, PageNumber: 2, PageSize: 2);

        var dto1 = new UserDto(entity1.Id, entity1.Username, "u1@example.com");
        var dto2 = new UserDto(entity2.Id, entity2.Username, "u2@example.com");
        mapper.MapList<UserEntity, UserDto>(Arg.Any<IReadOnlyCollection<UserEntity>>()).Returns([dto1, dto2]);

        var targetPaged = sourcePaged.MapTo<UserEntity, UserDto>(mapper);

        targetPaged.ShouldNotBeNull();
        targetPaged.TotalCount.ShouldBe(10);
        targetPaged.PageNumber.ShouldBe(2);
        targetPaged.PageSize.ShouldBe(2);
        targetPaged.TotalPages.ShouldBe(5);
        targetPaged.HasNextPage.ShouldBeTrue();
        targetPaged.HasPreviousPage.ShouldBeTrue();
        targetPaged.Items.Count.ShouldBe(2);
        targetPaged.Items[0].ShouldBe(dto1);
        targetPaged.Items[1].ShouldBe(dto2);
    }

    [Fact(DisplayName = "SeekResult.MapTo converts items and preserves keyset metadata")]
    public void SeekResult_MapTo_ConvertsItemsAndPreservesMetadata()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "u1" };
        var sourceSeek = new KyrolusSeekResult<UserEntity>([entity], NextToken: "token123", TotalCount: 50, PageSize: 10);

        var dto = new UserDto(entity.Id, entity.Username, "u1@example.com");
        mapper.MapList<UserEntity, UserDto>(Arg.Any<IReadOnlyCollection<UserEntity>>()).Returns([dto]);

        var targetSeek = sourceSeek.MapTo<UserEntity, UserDto>(mapper);

        targetSeek.ShouldNotBeNull();
        targetSeek.NextToken.ShouldBe("token123");
        targetSeek.TotalCount.ShouldBe(50);
        targetSeek.PageSize.ShouldBe(10);
        targetSeek.HasMore.ShouldBeTrue();
        targetSeek.Items.Count.ShouldBe(1);
        targetSeek.Items[0].ShouldBe(dto);
    }

    [Fact(DisplayName = "ToDto and ToDtoList map single objects and collections")]
    public void ToDto_And_ToDtoList_WorkAsExpected()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "u1" };
        var dto = new UserDto(entity.Id, entity.Username, "u1@test.com");

        mapper.Map<UserDto>(entity).Returns(dto);
        mapper.MapList<UserEntity, UserDto>(Arg.Any<IReadOnlyCollection<UserEntity>>()).Returns([dto]);

        var singleResult = entity.ToDto<UserDto>(mapper);
        singleResult.ShouldBe(dto);

        var listResult = new[] { entity }.ToDtoList<UserEntity, UserDto>(mapper);
        listResult.Count.ShouldBe(1);
        listResult[0].ShouldBe(dto);
    }

    // ==========================================
    // 4. Base Mapped Request Handlers Tests
    // ==========================================

    private class TestMappedUserHandler(IKyrolusObjectMapper mapper, UserEntity entity)
        : KyrolusMappedRequestHandler<UserQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<UserEntity> HandleCoreAsync(UserQuery request, CancellationToken cancellationToken)
            => Task.FromResult(entity);
    }

    [Fact(DisplayName = "KyrolusMappedRequestHandler maps entity from HandleCoreAsync to response")]
    public async Task KyrolusMappedRequestHandler_MapsToResponse()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");

        mapper.Map<UserEntity, UserDto>(entity).Returns(dto);

        var handler = new TestMappedUserHandler(mapper, entity);
        var result = await handler.Handle(new UserQuery(entity.Id), CancellationToken.None);

        result.ShouldBe(dto);
    }

    [Fact(DisplayName = "KyrolusMappedRequestHandler returns null for a reference-type response when source is null")]
    public async Task KyrolusMappedRequestHandler_NullSource_ReferenceTypeResponse_ReturnsNull()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var handler = new TestMappedUserHandler(mapper, null!);

        var result = await handler.Handle(new UserQuery(Guid.NewGuid()), CancellationToken.None);

        result.ShouldBeNull();
    }

    private class TestMappedBoolHandler(IKyrolusObjectMapper mapper, UserEntity? entity)
        : KyrolusMappedRequestHandler<UserExistsQuery, UserEntity, bool>(mapper)
    {
        protected override Task<UserEntity> HandleCoreAsync(UserExistsQuery request, CancellationToken cancellationToken)
            => Task.FromResult(entity!);
    }

    [Fact(DisplayName = "KyrolusMappedRequestHandler throws when TResponse is a non-nullable value type and source is null")]
    public async Task KyrolusMappedRequestHandler_NullSource_NonNullableValueTypeResponse_Throws()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var handler = new TestMappedBoolHandler(mapper, null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new UserExistsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private class TestMappedNullableBoolHandler(IKyrolusObjectMapper mapper)
        : KyrolusMappedRequestHandler<UserExistsNullableQuery, UserEntity, bool?>(mapper)
    {
        protected override Task<UserEntity> HandleCoreAsync(UserExistsNullableQuery request, CancellationToken cancellationToken)
            => Task.FromResult<UserEntity>(null!);
    }

    [Fact(DisplayName = "KyrolusMappedRequestHandler returns default(null) for a nullable value-type response when source is null")]
    public async Task KyrolusMappedRequestHandler_NullSource_NullableValueTypeResponse_ReturnsDefault()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var handler = new TestMappedNullableBoolHandler(mapper);

        var result = await handler.Handle(new UserExistsNullableQuery(Guid.NewGuid()), CancellationToken.None);

        result.ShouldBeNull();
    }

    private class TestMappedPagedHandler(IKyrolusObjectMapper mapper, KyrolusPagedResult<UserEntity> paged)
        : KyrolusMappedPagedRequestHandler<UserPagedQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<KyrolusPagedResult<UserEntity>> HandleCoreAsync(UserPagedQuery request, CancellationToken cancellationToken)
            => Task.FromResult(paged);
    }

    [Fact(DisplayName = "KyrolusMappedPagedRequestHandler maps paged result from HandleCoreAsync")]
    public async Task KyrolusMappedPagedRequestHandler_MapsPagedResult()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var sourcePaged = new KyrolusPagedResult<UserEntity>([entity], 1, 1, 10);
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");

        mapper.MapList<UserEntity, UserDto>(Arg.Any<IReadOnlyCollection<UserEntity>>()).Returns([dto]);

        var handler = new TestMappedPagedHandler(mapper, sourcePaged);
        var result = await handler.Handle(new UserPagedQuery(1, 10), CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].ShouldBe(dto);
        result.TotalCount.ShouldBe(1);
    }

    private class TestMappedPagedHandlerWithContext(IKyrolusObjectMapper mapper, KyrolusPagedResult<UserEntity> paged, KyrolusMappingContext context)
        : KyrolusMappedPagedRequestHandler<UserPagedQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<KyrolusPagedResult<UserEntity>> HandleCoreAsync(UserPagedQuery request, CancellationToken cancellationToken)
            => Task.FromResult(paged);

        protected override KyrolusMappingContext? CreateMappingContext(UserPagedQuery request) => context;
    }

    [Fact(DisplayName = "KyrolusMappedPagedRequestHandler's CreateMappingContext override is observed by the mapper")]
    public async Task KyrolusMappedPagedRequestHandler_CreateMappingContext_IsObservedByMapper()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var sourcePaged = new KyrolusPagedResult<UserEntity>([entity], 1, 1, 10);
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");
        var context = new KyrolusMappingContext();

        mapper.MapList<UserEntity, UserDto>(Arg.Any<IEnumerable<UserEntity>>(), context).Returns([dto]);

        var handler = new TestMappedPagedHandlerWithContext(mapper, sourcePaged, context);
        var result = await handler.Handle(new UserPagedQuery(1, 10), CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].ShouldBe(dto);
        mapper.Received(1).MapList<UserEntity, UserDto>(Arg.Any<IEnumerable<UserEntity>>(), context);
    }

    private class TestMappedSeekHandler(IKyrolusObjectMapper mapper, KyrolusSeekResult<UserEntity> seek)
        : KyrolusMappedSeekRequestHandler<UserSeekQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<KyrolusSeekResult<UserEntity>> HandleCoreAsync(UserSeekQuery request, CancellationToken cancellationToken)
            => Task.FromResult(seek);
    }

    [Fact(DisplayName = "KyrolusMappedSeekRequestHandler maps seek result from HandleCoreAsync")]
    public async Task KyrolusMappedSeekRequestHandler_MapsSeekResult()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var sourceSeek = new KyrolusSeekResult<UserEntity>([entity], "cursor-token", 1, 10);
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");

        mapper.MapList<UserEntity, UserDto>(Arg.Any<IReadOnlyCollection<UserEntity>>()).Returns([dto]);

        var handler = new TestMappedSeekHandler(mapper, sourceSeek);
        var result = await handler.Handle(new UserSeekQuery(10), CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].ShouldBe(dto);
        result.NextToken.ShouldBe("cursor-token");
    }

    private class TestMappedSeekHandlerWithContext(IKyrolusObjectMapper mapper, KyrolusSeekResult<UserEntity> seek, KyrolusMappingContext context)
        : KyrolusMappedSeekRequestHandler<UserSeekQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<KyrolusSeekResult<UserEntity>> HandleCoreAsync(UserSeekQuery request, CancellationToken cancellationToken)
            => Task.FromResult(seek);

        protected override KyrolusMappingContext? CreateMappingContext(UserSeekQuery request) => context;
    }

    [Fact(DisplayName = "KyrolusMappedSeekRequestHandler's CreateMappingContext override is observed by the mapper")]
    public async Task KyrolusMappedSeekRequestHandler_CreateMappingContext_IsObservedByMapper()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var sourceSeek = new KyrolusSeekResult<UserEntity>([entity], "cursor-token", 1, 10);
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");
        var context = new KyrolusMappingContext();

        mapper.MapList<UserEntity, UserDto>(Arg.Any<IEnumerable<UserEntity>>(), context).Returns([dto]);

        var handler = new TestMappedSeekHandlerWithContext(mapper, sourceSeek, context);
        var result = await handler.Handle(new UserSeekQuery(10), CancellationToken.None);

        result.Items.Count.ShouldBe(1);
        result.Items[0].ShouldBe(dto);
        mapper.Received(1).MapList<UserEntity, UserDto>(Arg.Any<IEnumerable<UserEntity>>(), context);
    }

    private class TestMappedListHandler(IKyrolusObjectMapper mapper, List<UserEntity> list)
        : KyrolusMappedListRequestHandler<UserListQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<IEnumerable<UserEntity>> HandleCoreAsync(UserListQuery request, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<UserEntity>>(list);
    }

    [Fact(DisplayName = "KyrolusMappedListRequestHandler maps collection from HandleCoreAsync")]
    public async Task KyrolusMappedListRequestHandler_MapsListResult()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");

        mapper.MapList<UserEntity, UserDto>(Arg.Any<IReadOnlyCollection<UserEntity>>()).Returns([dto]);

        var handler = new TestMappedListHandler(mapper, [entity]);
        var result = await handler.Handle(new UserListQuery(), CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].ShouldBe(dto);
    }

    private class TestMappedListHandlerWithContext(IKyrolusObjectMapper mapper, List<UserEntity> list, KyrolusMappingContext context)
        : KyrolusMappedListRequestHandler<UserListQuery, UserEntity, UserDto>(mapper)
    {
        protected override Task<IEnumerable<UserEntity>> HandleCoreAsync(UserListQuery request, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<UserEntity>>(list);

        protected override KyrolusMappingContext? CreateMappingContext(UserListQuery request) => context;
    }

    [Fact(DisplayName = "KyrolusMappedListRequestHandler's CreateMappingContext override is observed by the mapper")]
    public async Task KyrolusMappedListRequestHandler_CreateMappingContext_IsObservedByMapper()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var entity = new UserEntity { Id = Guid.NewGuid(), Username = "user1" };
        var dto = new UserDto(entity.Id, entity.Username, "u@test.com");
        var context = new KyrolusMappingContext();

        mapper.MapList<UserEntity, UserDto>(Arg.Any<IEnumerable<UserEntity>>(), context).Returns([dto]);

        var handler = new TestMappedListHandlerWithContext(mapper, [entity], context);
        var result = await handler.Handle(new UserListQuery(), CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].ShouldBe(dto);
        mapper.Received(1).MapList<UserEntity, UserDto>(Arg.Any<IEnumerable<UserEntity>>(), context);
    }

    // ==========================================
    // 5. Pipeline Behavior Tests
    // ==========================================

    private record ContextAwareRequest(string TenantId) : IKyrolusRequest<string>, IKyrolusContextAwareMapping
    {
        public void ConfigureMappingContext(KyrolusMappingContext context)
        {
            context.Items["TenantId"] = TenantId;
        }
    }

    private class PostMappableResponse : IKyrolusPostMappableResponse
    {
        public bool WasMapped { get; private set; }
        public string? CapturedTenant { get; private set; }

        public void OnMapped(IKyrolusObjectMapper mapper, KyrolusMappingContext? context = null)
        {
            WasMapped = true;
            CapturedTenant = context?.Items.TryGetValue("TenantId", out var t) == true ? t?.ToString() : null;
        }
    }

    [Fact(DisplayName = "KyrolusMappingPipelineBehavior configures context and calls OnMapped")]
    public async Task KyrolusMappingPipelineBehavior_ExecutesContextAndPostProcessing()
    {
        var mapper = Substitute.For<IKyrolusObjectMapper>();
        var behavior = new KyrolusMappingPipelineBehavior<ContextAwareRequest, PostMappableResponse>(mapper);

        var request = new ContextAwareRequest("tenant-123");
        var response = new PostMappableResponse();

        var result = await behavior.Handle(request, _ => Task.FromResult(response), CancellationToken.None);

        result.ShouldBeSameAs(response);
        result.WasMapped.ShouldBeTrue();
        result.CapturedTenant.ShouldBe("tenant-123");
    }
}
