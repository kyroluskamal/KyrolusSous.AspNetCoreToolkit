namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Unified mediator contract for sending requests and publishing notifications.
/// </summary>
/// <remarks>
/// Inherits the two halves instead of restating their members, mirroring MediatR's
/// <c>IMediator : ISender, IPublisher</c>. Restating them meant an extension method written for
/// the sender did not apply to the mediator, and every new member had to be declared twice.
/// Inject <see cref="IKyrolusMediatorSender"/> or <see cref="IKyrolusMediatorPublisher"/> on its
/// own when a class only needs one half.
/// </remarks>
public interface IKyrolusMediator : IKyrolusMediatorSender, IKyrolusMediatorPublisher
{
}
