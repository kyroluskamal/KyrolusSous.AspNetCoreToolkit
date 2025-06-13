namespace KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

public interface IKyrolusMapper
{
    dynamic MapResponseToViewModel<TResponse>(TResponse model, Type viewModel, int statusCode, string message = "Success") where TResponse : class;
    dynamic MapModelToEntity<TModel, TResponse>(TModel model) where TModel : class;
    dynamic MapModelToEntity<TModel, TResponse>(IEnumerable<TModel> model) where TModel : class;
}