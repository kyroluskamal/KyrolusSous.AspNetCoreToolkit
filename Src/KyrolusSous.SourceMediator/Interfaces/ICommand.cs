namespace KyrolusSous.SourceMediator.Interfaces;

public interface ICommand
{

}
public interface ICommand<out TResponse> : ICommand
{
}