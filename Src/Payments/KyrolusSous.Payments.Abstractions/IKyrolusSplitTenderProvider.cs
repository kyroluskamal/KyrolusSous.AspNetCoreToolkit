namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusSplitTenderProvider
{
    Task<KyrolusSplitTenderResult> ExecuteSplitTenderAsync(
        KyrolusSplitTenderRequest request,
        CancellationToken cancellationToken = default);
}
