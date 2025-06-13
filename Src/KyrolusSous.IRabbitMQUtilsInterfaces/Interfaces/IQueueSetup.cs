namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

public  interface IQueueSetup
{
public string Name { get; set; }
public string RoutingKey { get; set; }
public bool Durable { get; set; } 
public bool Exclusive { get; set; } 
public bool Autodelete { get; set; } 
public IDictionary<string, object?>? Arguments { get; set; } 
}