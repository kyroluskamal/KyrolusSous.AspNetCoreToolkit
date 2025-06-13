using Serilog.Events;
using Serilog.Formatting;

namespace KyrolusSous.BaseConfig.LoggingService;

public class CustomTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        if (logEvent.Level.ToString() != "Information")
        {
            output.WriteLine("----------------------------------------------------------------------");
            output.WriteLine($"Timestamp - {logEvent.Timestamp} | Level - {logEvent.Level} |");
            output.WriteLine("----------------------------------------------------------------------");
            foreach (var item in logEvent.Properties)
            {
                output.WriteLine(item.Key + " : " + item.Value);
            }
            if (logEvent.Exception != null)
            {
                output.WriteLine("----------------------EXCEPTION DETAILS-------------------------------");
                output.Write("Exception - {0}", logEvent.Exception);
                output.Write("StackTrace - {0}", logEvent.Exception.StackTrace);
                output.Write("Message - {0}", logEvent.Exception.Message);
                output.Write("Source - {0}", logEvent.Exception.Source);
                output.Write("InnerException - {0}", logEvent.Exception.InnerException);
                output.Write("TargetSite - {0}", logEvent.Exception.TargetSite);
                output.Write("TraceId - {0}", logEvent.TraceId);
            }
            output.WriteLine("---------------------------------------------------------------------------");
        }
    }
}