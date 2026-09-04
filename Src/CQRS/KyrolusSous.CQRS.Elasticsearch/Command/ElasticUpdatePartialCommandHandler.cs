namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command handler executing <see cref="ElasticUpdatePartialCommand{TDocument, TId}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticUpdatePartialCommandHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticUpdatePartialCommandHandler<TDocument, TId>>? logger = null)
    : IKyrolusCommandHandler<ElasticUpdatePartialCommand<TDocument, TId>, bool>
    where TDocument : class
{
    public async Task<bool> Handle(ElasticUpdatePartialCommand<TDocument, TId> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.PartialDocument);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Partially updating '{DocumentType}' document with Id '{Id}'",
            typeof(TDocument).Name,
            command.Id);

        return await repository.UpdatePartialAsync(command.Id, command.PartialDocument, command.ExpectedSeqNo, command.ExpectedPrimaryTerm, cancellationToken).ConfigureAwait(false);
    }
}
