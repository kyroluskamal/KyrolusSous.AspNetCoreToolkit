using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.Resilience;

public static class CircuitBreakerDashboardEndpointExtensions
{
    /// <summary>
    /// Maps the real-time circuit breaker status and control dashboard endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapKyrolusCircuitBreakerDashboard(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/resilience/circuits")
    {
        var group = endpoints.MapGroup(basePath)
            .WithTags("Resilience Circuit Breakers");

        // GET /resilience/circuits
        group.MapGet("/", (IKyrolusCircuitBreakerObserver observer) =>
        {
            var states = observer.GetAllCircuitStates();
            var details = states.Select(kvp => observer.GetCircuitInfo(kvp.Key)).ToList();
            return Results.Ok(details);
        })
        .WithName("GetKyrolusCircuitBreakers")
        .WithSummary("Retrieves real-time diagnostics and states for all registered circuit breakers.");

        // GET /resilience/circuits/{pipelineName}
        group.MapGet("/{pipelineName}", (string pipelineName, IKyrolusCircuitBreakerObserver observer) =>
        {
            var info = observer.GetCircuitInfo(pipelineName);
            return Results.Ok(info);
        })
        .WithName("GetKyrolusCircuitBreakerByName")
        .WithSummary("Retrieves diagnostic info for a specific circuit breaker.");

        // POST /resilience/circuits/{pipelineName}/force-open
        group.MapPost("/{pipelineName}/force-open", (string pipelineName, IKyrolusCircuitBreakerObserver observer) =>
        {
            observer.ForceOpen(pipelineName);
            return Results.Ok(new { Pipeline = pipelineName, State = KyrolusCircuitState.Open.ToString(), Message = "Circuit breaker manually forced OPEN." });
        })
        .WithName("ForceOpenKyrolusCircuitBreaker")
        .WithSummary("Manually trips and forces a circuit breaker OPEN.");

        // POST /resilience/circuits/{pipelineName}/force-close
        group.MapPost("/{pipelineName}/force-close", (string pipelineName, IKyrolusCircuitBreakerObserver observer) =>
        {
            observer.ForceClose(pipelineName);
            return Results.Ok(new { Pipeline = pipelineName, State = KyrolusCircuitState.Closed.ToString(), Message = "Circuit breaker manually forced CLOSED." });
        })
        .WithName("ForceCloseKyrolusCircuitBreaker")
        .WithSummary("Manually resets and forces a circuit breaker CLOSED.");

        // POST /resilience/circuits/{pipelineName}/reset
        group.MapPost("/{pipelineName}/reset", (string pipelineName, IKyrolusCircuitBreakerObserver observer) =>
        {
            observer.Reset(pipelineName);
            return Results.Ok(new { Pipeline = pipelineName, State = KyrolusCircuitState.Closed.ToString(), Message = "Circuit breaker statistics and state successfully RESET." });
        })
        .WithName("ResetKyrolusCircuitBreaker")
        .WithSummary("Resets circuit breaker state and clears all error counters.");

        return endpoints;
    }
}
