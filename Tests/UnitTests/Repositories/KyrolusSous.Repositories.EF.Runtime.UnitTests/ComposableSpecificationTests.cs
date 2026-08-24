using KyrolusSous.Repositories.EF.Runtime.Specifications;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class ComposableSpecificationTests
{
    private sealed class Vehicle
    {
        public int Id { get; set; }
        public string Brand { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
    }

    private sealed class VehicleDbContext(DbContextOptions<VehicleDbContext> options) : DbContext(options)
    {
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    }

    private static VehicleDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VehicleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new VehicleDbContext(options);
        context.Vehicles.AddRange(
            new Vehicle { Id = 1, Brand = "Toyota", Price = 25000, IsAvailable = true },
            new Vehicle { Id = 2, Brand = "BMW", Price = 55000, IsAvailable = false },
            new Vehicle { Id = 3, Brand = "Mercedes", Price = 65000, IsAvailable = true },
            new Vehicle { Id = 4, Brand = "Toyota", Price = 35000, IsAvailable = false },
            new Vehicle { Id = 5, Brand = "Audi", Price = 45000, IsAvailable = true }
        );
        context.SaveChanges();
        return context;
    }

    [Fact(DisplayName = "Specification: Evaluates criteria, sorting, and paging")]
    public void Specification_AppliesFiltersAndSorting()
    {
        using var context = CreateContext();

        var spec = new KyrolusSpecification<Vehicle>(v => v.IsAvailable)
            .ApplyOrderByDescending(v => v.Price)
            .ApplyPaging(skip: 0, take: 2)
            .AsNoTracking();

        var results = KyrolusSpecificationEvaluator.GetQuery(context.Vehicles, spec).ToList();

        results.Count.ShouldBe(2);
        results[0].Brand.ShouldBe("Mercedes"); // Highest price available
        results[1].Brand.ShouldBe("Audi");
    }

    [Fact(DisplayName = "Specification: Combines multiple specifications with AND logic")]
    public void Specification_CombinesWithAnd()
    {
        using var context = CreateContext();

        var spec1 = new KyrolusSpecification<Vehicle>(v => v.Brand == "Toyota");
        var spec2 = new KyrolusSpecification<Vehicle>(v => v.IsAvailable);

        var combined = spec1.And(spec2);
        var results = KyrolusSpecificationEvaluator.GetQuery(context.Vehicles, combined).ToList();

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(1);
    }

    [Fact(DisplayName = "Specification: Combines multiple specifications with OR logic")]
    public void Specification_CombinesWithOr()
    {
        using var context = CreateContext();

        var spec1 = new KyrolusSpecification<Vehicle>(v => v.Brand == "BMW");
        var spec2 = new KyrolusSpecification<Vehicle>(v => v.Brand == "Audi");

        var combined = spec1.Or(spec2);
        var results = KyrolusSpecificationEvaluator.GetQuery(context.Vehicles, combined).ToList();

        results.Count.ShouldBe(2);
        results.ShouldContain(v => v.Brand == "BMW");
        results.ShouldContain(v => v.Brand == "Audi");
    }
}
