namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a command that requires explicit transaction boundary control.
/// </summary>
public interface ITransactionalCommand : IKyrolusCommandBase
{
    /// <summary>
    /// Optional transaction isolation level. If null, database default is used.
    /// </summary>
    IsolationLevel? IsolationLevel => null;

    /// <summary>
    /// If true, disables the automatic ambient transaction behavior for this command.
    /// </summary>
    bool DisableAutoTransaction => false;
}
