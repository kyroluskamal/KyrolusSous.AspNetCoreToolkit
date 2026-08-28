namespace KyrolusSous.Resilience;

/// <summary>
/// Execution priority levels for requests subject to load shedding and resource scheduling.
/// </summary>
public enum KyrolusRequestPriority
{
    Critical = 0,
    High = 1,
    Normal = 2,
    Low = 3,
    Background = 4
}
