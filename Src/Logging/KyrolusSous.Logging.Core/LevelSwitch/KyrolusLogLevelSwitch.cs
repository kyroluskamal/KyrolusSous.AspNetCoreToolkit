using KyrolusSous.Logging.Abstractions.LevelSwitch;

namespace KyrolusSous.Logging.Core.LevelSwitch;

/// <summary>
/// Thread-safe in-memory dynamic log level switch with timed temporary boosts.
/// </summary>
public sealed class KyrolusLogLevelSwitch : IKyrolusLogLevelSwitch
{
    private readonly object _lock = new();
    private volatile LogLevel _minimumLevel;
    private LogLevel _baseLevel;
    private Timer? _boostTimer;
    private long _boostSequence;
    private long _activeBoostId;

    /// <summary>
    /// Event triggered whenever the minimum level changes.
    /// </summary>
    public event Action<LogLevel>? MinimumLevelChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusLogLevelSwitch"/> class.
    /// </summary>
    /// <param name="initialLevel">The initial minimum log level.</param>
    public KyrolusLogLevelSwitch(LogLevel initialLevel = LogLevel.Information)
    {
        _minimumLevel = initialLevel;
        _baseLevel = initialLevel;
    }

    /// <inheritdoc/>
    public LogLevel MinimumLevel => _minimumLevel;

    /// <inheritdoc/>
    public void SetMinimumLevel(LogLevel newLevel)
    {
        lock (_lock)
        {
            _boostTimer?.Dispose();
            _boostTimer = null;
            _activeBoostId = 0;
            _baseLevel = newLevel;
            _minimumLevel = newLevel;
        }

        MinimumLevelChanged?.Invoke(newLevel);
    }

    /// <inheritdoc/>
    public IDisposable BoostLevel(LogLevel boostLevel, TimeSpan duration)
    {
        long boostId;
        lock (_lock)
        {
            _boostTimer?.Dispose();
            boostId = ++_boostSequence;
            _activeBoostId = boostId;
            _minimumLevel = boostLevel;

            _boostTimer = new Timer(state =>
            {
                if (state is long id)
                {
                    RevertBoost(id);
                }
            }, boostId, duration, Timeout.InfiniteTimeSpan);
        }

        MinimumLevelChanged?.Invoke(boostLevel);
        return new BoostScope(this, boostId);
    }

    private void RevertBoost(long boostId)
    {
        LogLevel targetLevel;
        var changed = false;

        lock (_lock)
        {
            if (_activeBoostId == boostId)
            {
                _boostTimer?.Dispose();
                _boostTimer = null;
                _activeBoostId = 0;
                _minimumLevel = _baseLevel;
                targetLevel = _baseLevel;
                changed = true;
            }
            else
            {
                targetLevel = _minimumLevel;
            }
        }

        if (changed)
        {
            MinimumLevelChanged?.Invoke(targetLevel);
        }
    }

    private sealed class BoostScope(KyrolusLogLevelSwitch parent, long boostId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                parent.RevertBoost(boostId);
                _disposed = true;
            }
        }
    }
}
