using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Marten;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace KyrolusSous.Auth.Marten.UnitTests;

public class MartenAuthUserStoreTests
{
    [Fact]
    public void ModelConversion_ToAuthUser_And_CopyFrom_PreserveAllFields()
    {
        var authUser = new KyrolusAuthUser
        {
            Id = "user-marten-1",
            UserName = "kyrolus",
            Email = "kyrolus@marten.com",
            EmailConfirmed = true,
            DisplayName = "Kyrolus Sous",
            Roles = ["Admin", "Architect"],
            Claims = new Dictionary<string, string> { ["dept"] = "Engineering" },
            AccessFailedCount = 2,
            LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10),
            LockoutEnabled = true,
            SecurityStamp = "stamp-123"
        };

        var doc = new KyrolusMartenAuthUser();
        doc.CopyFrom(authUser);

        doc.Id.ShouldBe("user-marten-1");
        doc.UserName.ShouldBe("kyrolus");
        doc.Email.ShouldBe("kyrolus@marten.com");
        doc.Roles.ShouldContain("Admin");
        doc.Claims["dept"].ShouldBe("Engineering");

        var backToAuth = doc.ToAuthUser();
        backToAuth.Id.ShouldBe(authUser.Id);
        backToAuth.UserName.ShouldBe(authUser.UserName);
        backToAuth.Roles.ShouldContain("Architect");
        backToAuth.Claims["dept"].ShouldBe("Engineering");
    }

    [Fact]
    public async Task CreateAsync_StoresDocumentAndSavesChanges()
    {
        var session = Substitute.For<IDocumentSession>();
        var store = new KyrolusMartenAuthUserStore(session);

        var user = new KyrolusAuthUser
        {
            UserName = "doc-user",
            Email = "doc@example.com"
        };

        var created = await store.CreateAsync(user);

        created.ShouldNotBeNull();
        session.Received(1).Store(Arg.Any<KyrolusMartenAuthUser>());
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByIdAsync_CallsSessionLoad()
    {
        var session = Substitute.For<IDocumentSession>();
        var doc = new KyrolusMartenAuthUser
        {
            Id = "user-99",
            UserName = "alice",
            Email = "alice@example.com"
        };

        session.LoadAsync<KyrolusMartenAuthUser>("user-99", Arg.Any<CancellationToken>())
            .Returns(doc);

        var store = new KyrolusMartenAuthUserStore(session);
        var found = await store.FindByIdAsync("user-99");

        found.ShouldNotBeNull();
        found.Id.ShouldBe("user-99");
        found.UserName.ShouldBe("alice");
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_UpdatesDocumentAndSaves()
    {
        var session = Substitute.For<IDocumentSession>();
        var doc = new KyrolusMartenAuthUser
        {
            Id = "user-lockout",
            AccessFailedCount = 0
        };

        session.LoadAsync<KyrolusMartenAuthUser>("user-lockout", Arg.Any<CancellationToken>())
            .Returns(doc);

        var store = new KyrolusMartenAuthUserStore(session);
        await store.RecordFailedAttemptAsync("user-lockout", 3, DateTimeOffset.UtcNow.AddMinutes(5));

        doc.AccessFailedCount.ShouldBe(3);
        doc.LockoutEnd.ShouldNotBeNull();
        session.Received(1).Store(doc);
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DiRegistration_AddKyrolusMartenAuthStore_RegistersInterfaces()
    {
        var services = new ServiceCollection();
        var session = Substitute.For<IDocumentSession>();
        services.AddScoped(_ => session);
        services.AddKyrolusMartenAuthStore();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusAuthUserStore>().ShouldNotBeNull();
        provider.GetService<IKyrolusAuthUserLockoutStore>().ShouldNotBeNull();
    }

    [Fact]
    public async Task FindByEmailAsync_And_FindByUserNameAsync_Throw_OnWhitespaceOrEmpty()
    {
        var session = Substitute.For<IDocumentSession>();
        var store = new KyrolusMartenAuthUserStore(session);

        await Should.ThrowAsync<ArgumentException>(async () => await store.FindByEmailAsync("   "));
        await Should.ThrowAsync<ArgumentException>(async () => await store.FindByUserNameAsync("   "));
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_ClampsNegativeCount()
    {
        var session = Substitute.For<IDocumentSession>();
        var user = new KyrolusMartenAuthUser { Id = "user-marten", UserName = "marten" };
        session.LoadAsync<KyrolusMartenAuthUser>("user-marten", Arg.Any<CancellationToken>()).Returns(user);

        var store = new KyrolusMartenAuthUserStore(session);
        await store.RecordFailedAttemptAsync("user-marten", -10, null);

        user.AccessFailedCount.ShouldBe(0);
    }
}
