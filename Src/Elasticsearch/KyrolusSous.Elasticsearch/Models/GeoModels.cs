namespace KyrolusSous.Elasticsearch;

public record GeoCoordinate(double Latitude, double Longitude)
{
    public string LatLonString => $"{Latitude},{Longitude}";
}

public enum TenantIsolationMode
{
    IndexPerTenant,
    DocumentFilter
}

public interface ITenantProvider
{
    string? CurrentTenantId { get; }
}

public class DefaultTenantProvider : ITenantProvider
{
    public virtual string? CurrentTenantId => null;
}
