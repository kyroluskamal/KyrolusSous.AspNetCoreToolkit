using KyrolusSous.Validation.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.EndpointKit.Core.Filters;

/// <summary>
/// Minimal API Endpoint Filter that automatically validates request models using <see cref="IKyrolusValidationEngine"/>.
/// Reuses existing validation and error handling abstractions without duplicate code.
/// </summary>
public sealed class KyrolusValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validationEngine = context.HttpContext.RequestServices.GetService<IKyrolusValidationEngine>();
        if (validationEngine is null)
        {
            return await next(context);
        }

        foreach (var argument in context.Arguments)
        {
            if (argument is null || argument is CancellationToken || argument is HttpContext)
            {
                continue;
            }

            var type = argument.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(decimal))
            {
                continue;
            }

            var failures = await validationEngine.ValidateAsync(argument, context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (failures is not null && failures.Count > 0)
            {
                var errors = failures
                    .GroupBy(e => e.PropertyName ?? string.Empty)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray());

                return Results.ValidationProblem(errors);
            }
        }

        return await next(context);
    }
}
