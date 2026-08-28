using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.EntityFramework.UnitTests;

public class TestAuthDbContext(DbContextOptions<TestAuthDbContext> options) : DbContext(options)
{
    public DbSet<KyrolusEfAuthUserEntity> Users => Set<KyrolusEfAuthUserEntity>();
    public DbSet<KyrolusEfExternalLoginEntity> ExternalLogins => Set<KyrolusEfExternalLoginEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyKyrolusAuthConfig();
    }
}

public class EfAuthUserStoreTests
{
    private static TestAuthDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestAuthDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new TestAuthDbContext(options);
    }

    [Fact(DisplayName = "Create Async And Find By Id Async Work Correctly")]
    public async Task CreateAsync_And_FindByIdAsync_WorkCorrectly()
    {
        using var db = CreateDbContext(nameof(CreateAsync_And_FindByIdAsync_WorkCorrectly));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        var user = new KyrolusAuthUser
        {
            UserName = "kyrolus.sous",
            Email = "kyrolus@example.com",
            DisplayName = "Kyrolus Sous",
            Roles = ["Admin", "Developer"]
        };

        var created = await store.CreateAsync(user);

        created.ShouldNotBeNull();
        created.Id.ShouldNotBeNullOrWhiteSpace();

        var found = await store.FindByIdAsync(created.Id);

        found.ShouldNotBeNull();
        found.UserName.ShouldBe("kyrolus.sous");
        found.Email.ShouldBe("kyrolus@example.com");
        found.Roles.ShouldContain("Admin");
    }

    [Fact(DisplayName = "Find By User Name And Find By Email Return Matching User")]
    public async Task FindByUserName_And_FindByEmail_ReturnMatchingUser()
    {
        using var db = CreateDbContext(nameof(FindByUserName_And_FindByEmail_ReturnMatchingUser));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        var user = new KyrolusAuthUser
        {
            UserName = "johndoe",
            Email = "john@example.com"
        };

        await store.CreateAsync(user);

        var byName = await store.FindByUserNameAsync("johndoe");
        byName.ShouldNotBeNull();
        byName.Email.ShouldBe("john@example.com");

        var byEmail = await store.FindByEmailAsync("john@example.com");
        byEmail.ShouldNotBeNull();
        byEmail.UserName.ShouldBe("johndoe");
    }

    [Fact(DisplayName = "External Login Link And Find Works Correctly")]
    public async Task ExternalLogin_LinkAndFind_WorksCorrectly()
    {
        using var db = CreateDbContext(nameof(ExternalLogin_LinkAndFind_WorksCorrectly));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        var user = await store.CreateAsync(new KyrolusAuthUser
        {
            UserName = "alice",
            Email = "alice@example.com"
        });

        await store.AddExternalLoginAsync(user.Id, "Google", "google-sub-12345");

        var found = await store.FindByExternalLoginAsync("Google", "google-sub-12345");

        found.ShouldNotBeNull();
        found.Id.ShouldBe(user.Id);
        found.UserName.ShouldBe("alice");
    }

    [Fact(DisplayName = "Lockout Store Record And Reset Works Correctly")]
    public async Task LockoutStore_RecordAndReset_WorksCorrectly()
    {
        using var db = CreateDbContext(nameof(LockoutStore_RecordAndReset_WorksCorrectly));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        var user = await store.CreateAsync(new KyrolusAuthUser
        {
            UserName = "bob",
            Email = "bob@example.com"
        });

        var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
        await store.RecordFailedAttemptAsync(user.Id, 3, lockoutEnd);

        var userAfterFailed = await store.FindByIdAsync(user.Id);
        userAfterFailed.ShouldNotBeNull();
        userAfterFailed.AccessFailedCount.ShouldBe(3);
        userAfterFailed.LockoutEnd.ShouldNotBeNull();

        await store.ResetFailedAttemptsAsync(user.Id);

        var userAfterReset = await store.FindByIdAsync(user.Id);
        userAfterReset.ShouldNotBeNull();
        userAfterReset.AccessFailedCount.ShouldBe(0);
        userAfterReset.LockoutEnd.ShouldBeNull();
    }

    [Fact(DisplayName = "Di Registration Add Kyrolus Ef Auth Store Registers Store Interfaces")]
    public void DiRegistration_AddKyrolusEfAuthStore_RegistersStoreInterfaces()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestAuthDbContext>(options => options.UseInMemoryDatabase("di_test"));
        services.AddKyrolusEfAuthStore<TestAuthDbContext>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusAuthUserStore>().ShouldNotBeNull();
        provider.GetService<IKyrolusAuthUserLockoutStore>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "Find By Email Async And Find By User Name Async Succeeds With Padded And Mixed Case")]
    public async Task FindByEmailAsync_And_FindByUserNameAsync_Succeeds_WithPaddedAndMixedCase()
    {
        using var db = CreateDbContext(nameof(FindByEmailAsync_And_FindByUserNameAsync_Succeeds_WithPaddedAndMixedCase));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        await store.CreateAsync(new KyrolusAuthUser
        {
            UserName = "charlie",
            Email = "charlie.brown@example.com"
        });

        // 1. Mixed-case and padded email lookup
        var byEmail = await store.FindByEmailAsync("  CHARLIE.BROWN@EXAMPLE.COM  ");
        byEmail.ShouldNotBeNull();
        byEmail.UserName.ShouldBe("charlie");

        // 2. Mixed-case and padded username lookup
        var byName = await store.FindByUserNameAsync("  CHARLIE  ");
        byName.ShouldNotBeNull();
        byName.Email.ShouldBe("charlie.brown@example.com");
    }

    [Fact(DisplayName = "Add External Login Async Is Idempotent")]
    public async Task AddExternalLoginAsync_IsIdempotent()
    {
        using var db = CreateDbContext(nameof(AddExternalLoginAsync_IsIdempotent));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        var user = await store.CreateAsync(new KyrolusAuthUser
        {
            UserName = "david",
            Email = "david@example.com"
        });

        await store.AddExternalLoginAsync(user.Id, "GitHub", "gh-123");
        await store.AddExternalLoginAsync(user.Id, "GitHub", "gh-123");

        var found = await store.FindByExternalLoginAsync("GitHub", "gh-123");
        found.ShouldNotBeNull();
        found.Id.ShouldBe(user.Id);
    }

    [Fact(DisplayName = "Record Failed Attempt Async Clamps Negative Access Failed Count")]
    public async Task RecordFailedAttemptAsync_ClampsNegativeAccessFailedCount()
    {
        using var db = CreateDbContext(nameof(RecordFailedAttemptAsync_ClampsNegativeAccessFailedCount));
        var store = new KyrolusEfAuthUserStore<TestAuthDbContext>(db);

        var user = await store.CreateAsync(new KyrolusAuthUser
        {
            UserName = "eve",
            Email = "eve@example.com"
        });

        await store.RecordFailedAttemptAsync(user.Id, -5, null);

        var userAfter = await store.FindByIdAsync(user.Id);
        userAfter.ShouldNotBeNull();
        userAfter.AccessFailedCount.ShouldBe(0);
    }
}
