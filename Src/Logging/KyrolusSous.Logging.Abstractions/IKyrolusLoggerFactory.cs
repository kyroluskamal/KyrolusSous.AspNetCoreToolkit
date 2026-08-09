namespace KyrolusSous.Logging.Abstractions;

public interface IKyrolusLoggerFactory
{
    IKyrolusLogger Create(string categoryName);

    IKyrolusLogger<TCategory> Create<TCategory>();
}
