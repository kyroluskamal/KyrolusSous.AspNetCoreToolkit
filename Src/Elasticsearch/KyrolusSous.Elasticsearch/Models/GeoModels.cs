namespace KyrolusSous.Elasticsearch;

public record KyrolusGeoCoordinate(double Latitude, double Longitude)
{
    public string LatLonString => $"{Latitude},{Longitude}";
}

public enum TenantIsolationMode
{
    IndexPerTenant,
    DocumentFilter
}

public interface IKyrolusTenantProvider
{
    string? CurrentTenantId { get; }
}

public class KyrolusDefaultTenantProvider : IKyrolusTenantProvider
{
    public virtual string? CurrentTenantId => null;
}
