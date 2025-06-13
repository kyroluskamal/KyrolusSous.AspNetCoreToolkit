using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule;

public class KyrolusMapper : IKyrolusMapper
{
    public dynamic MapModelToEntity<TModel, TResponse>(TModel model) where TModel : class
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }
        var response = model.Adapt<TResponse>() ?? throw new InvalidOperationException("Mapping resulted in a null response.");
        return response;
    }

    public dynamic MapModelToEntity<TModel, TResponse>(IEnumerable<TModel> model) where TModel : class
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }
        var response = model.Adapt<IEnumerable<TResponse>>() ?? throw new InvalidOperationException("Mapping resulted in a null response.");
        return response;
    }

    public dynamic MapResponseToViewModel<TResponse>(TResponse model, Type viewModelType, int statusCode, string message = "Success") where TResponse : class
    {
        Type targetType;
        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetInterfaces().Contains(typeof(IEnumerable<>)))
        {
            targetType = typeof(IEnumerable<>).MakeGenericType(viewModelType);
        }
        else
        {
            targetType = viewModelType;
        }

        var result = viewModelType == null ? model : model.Adapt(model.GetType(), targetType);

        return new Response(statusCode, message, true, result);
    }
}
