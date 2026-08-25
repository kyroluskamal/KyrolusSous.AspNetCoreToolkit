using System.Text;
using KyrolusSous.EndpointKit.Core.Export;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusCsvExporterTests
{
    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [Fact(DisplayName = "CsvExporter: Formats items with headers and escaped commas")]
    public void CsvExporter_Should_Format_Csv_Properly()
    {
        var items = new List<Item>
        {
            new() { Id = 1, Name = "Item, with comma", Price = 19.99m },
            new() { Id = 2, Name = "Simple Item", Price = 50.00m }
        };

        var bytes = KyrolusCsvExporter.ExportToCsv(items);
        var csvText = Encoding.UTF8.GetString(bytes);

        csvText.ShouldContain("Id,Name,Price");
        csvText.ShouldContain("1,\"Item, with comma\",19.99");
        csvText.ShouldContain("2,Simple Item,50.00");
    }

    [Fact(DisplayName = "CsvExporter: Respects selected fields projection")]
    public void CsvExporter_Should_Respect_Selected_Fields()
    {
        var items = new List<Item>
        {
            new() { Id = 1, Name = "Laptop", Price = 1200m }
        };

        var bytes = KyrolusCsvExporter.ExportToCsv(items, ["Name", "Price"]);
        var csvText = Encoding.UTF8.GetString(bytes);

        csvText.ShouldContain("Name,Price");
        csvText.ShouldNotContain("Id,Name,Price");
        csvText.ShouldContain("Laptop,1200");
    }
}
