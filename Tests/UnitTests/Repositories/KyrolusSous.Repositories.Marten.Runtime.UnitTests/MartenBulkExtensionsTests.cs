using KyrolusSous.Repositories.Marten.Runtime.Bulk;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenBulkExtensionsTests
{
    private sealed class LogEntry
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "BulkExtensions: BulkInsertDocumentsAsync returns CompletedTask when collection is empty")]
    public async Task BulkInsertDocumentsAsync_EmptyCollection_DoesNotCallStore()
    {
        var store = Substitute.For<IDocumentStore>();
        await store.BulkInsertDocumentsAsync<LogEntry>([]);

        await store.DidNotReceiveWithAnyArgs().BulkInsertAsync<LogEntry>(default!);
    }
}
