using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

public sealed class BulkPatchCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<BulkPatchCommand<TResponse, TKey>, int>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<int> Handle(BulkPatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var items = command.Items ?? [];
        if (items.Count == 0) return 0;

        var keyLength = items[0].KeyValues.Length;
        if (keyLength <= 1)
        {
            var repo = unitOfWork.GetRepository<IKyrolusSingleKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
            foreach (var item in items)
            {
                var key = ConvertKey(item.KeyValues.Length > 0 ? item.KeyValues[0] : null);
                await repo.PatchAsync(key, item.Updates, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
            foreach (var item in items)
            {
                await repo.PatchAsync(item.KeyValues, item.Updates, cancellationToken).ConfigureAwait(false);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return items.Count;
    }

    private static TKey ConvertKey(object? raw)
    {
        if (raw is TKey typed) return typed;
        if (raw is null) throw new InvalidOperationException("Patch key is required.");
        var targetType = Nullable.GetUnderlyingType(typeof(TKey)) ?? typeof(TKey);
        return (TKey)Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }
}
