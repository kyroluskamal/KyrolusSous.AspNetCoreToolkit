global using Serilog.Core;
global using Serilog.Events;

namespace KyrolusSous.Logging.UnitTests;

public class TestSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent)
    {
        Events.Add(logEvent);
    }
}

