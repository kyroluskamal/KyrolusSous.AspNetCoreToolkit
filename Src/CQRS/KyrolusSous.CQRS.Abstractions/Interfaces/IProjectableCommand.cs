namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a command that yields or updates a read-model projection.
/// </summary>
/// <typeparam name="TReadModel">The read model type to synchronize.</typeparam>
public interface IProjectableCommand<out TReadModel>
{
    /// <summary>
    /// Projects or extracts the read model representation from this command.
    /// </summary>
    TReadModel? ToReadModel() => default;
}
