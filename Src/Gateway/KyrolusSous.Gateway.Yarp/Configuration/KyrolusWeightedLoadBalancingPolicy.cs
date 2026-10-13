namespace KyrolusSous.Gateway.Yarp.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Custom YARP load balancing policy that distributes traffic across destination replicas proportionally to their configured weights.
/// Enables canary releases, progressive rollouts, and weighted traffic splitting across heterogeneous instances.
/// </summary>
public sealed class KyrolusWeightedLoadBalancingPolicy : ILoadBalancingPolicy
{
    /// <inheritdoc />
    public string Name => KyrolusLoadBalancingPolicies.Weighted;

    /// <inheritdoc />
    public DestinationState? PickDestination(
        HttpContext context,
        ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations)
    {
        ArgumentNullException.ThrowIfNull(availableDestinations);

        if (availableDestinations.Count == 0)
        {
            return null;
        }

        if (availableDestinations.Count == 1)
        {
            return availableDestinations[0];
        }

        // 1. Check for Canary Header Routing override if present in request headers
        // (e.g., if client specifies "X-Canary-Destination: node2" or "X-Canary: true")
        if (context.Request.Headers.TryGetValue("X-Canary-Destination", out var canaryTarget) &&
            !string.IsNullOrWhiteSpace(canaryTarget))
        {
            var matchedCanary = availableDestinations.FirstOrDefault(
                d => string.Equals(d.DestinationId, canaryTarget, StringComparison.OrdinalIgnoreCase));
            if (matchedCanary != null)
            {
                return matchedCanary;
            }
        }

        // 2. Compute cumulative weights for random weighted selection
        var weights = new int[availableDestinations.Count];
        long totalWeight = 0;

        for (var i = 0; i < availableDestinations.Count; i++)
        {
            var dest = availableDestinations[i];
            var weight = 1;

            if (dest.Model.Config.Metadata != null &&
                dest.Model.Config.Metadata.TryGetValue(KyrolusGatewayMetadataKeys.Weighted.Weight, out var rawWeight) &&
                int.TryParse(rawWeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWeight) &&
                parsedWeight > 0)
            {
                weight = parsedWeight;
            }

            weights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0)
        {
            return availableDestinations[0];
        }

        // 3. Cryptographically uniform weighted random selection without lock contention
        var randomValue = RandomNumberGenerator.GetInt32(0, (int)totalWeight);
        long cumulative = 0;

        for (var i = 0; i < availableDestinations.Count; i++)
        {
            cumulative += weights[i];
            if (randomValue < cumulative)
            {
                return availableDestinations[i];
            }
        }

        return availableDestinations[^1];
    }
}
