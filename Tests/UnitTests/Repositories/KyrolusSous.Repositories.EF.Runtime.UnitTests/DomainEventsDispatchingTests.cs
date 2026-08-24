using KyrolusSous.Repositories.EF.Abstractions.Events;
using KyrolusSous.Repositories.EF.Runtime.Interceptors;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class DomainEventsDispatchingTests
{
    private sealed record UserCreatedEvent(int UserId, string Email);

    private sealed class UserAccount : IKyrolusHasDomainEvents
    {
        private readonly List<object> _domainEvents = [];

        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;

        public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    private sealed class MockDispatcher : IKyrolusDomainEventDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public Task DispatchEventsAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default)
        {
            Dispatched.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    private sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
    {
        public DbSet<UserAccount> Accounts => Set<UserAccount>();
    }

    [Fact(DisplayName = "DomainEventsInterceptor: Dispatches and clears domain events automatically on SaveChanges")]
    public async Task DomainEvents_DispatchedAndClearedOnSave()
    {
        var dispatcher = new MockDispatcher();
        var options = new DbContextOptionsBuilder<AccountsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new KyrolusDomainEventInterceptor(dispatcher))
            .Options;

        using var context = new AccountsDbContext(options);
        var user = new UserAccount { Id = 101, Email = "user@test.com" };
        user.AddDomainEvent(new UserCreatedEvent(101, "user@test.com"));

        context.Accounts.Add(user);
        await context.SaveChangesAsync();

        dispatcher.Dispatched.Count.ShouldBe(1);
        dispatcher.Dispatched[0].ShouldBeOfType<UserCreatedEvent>();
        user.DomainEvents.ShouldBeEmpty(); // Events cleared after dispatch
    }
}
