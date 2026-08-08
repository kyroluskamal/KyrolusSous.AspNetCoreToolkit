var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var postgresConnString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=kyrolus_runtime_tests;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(postgresConnString));
builder.Services.AddKyrolusRuntimeRepositories();
builder.Services.AddKyrolusRuntimeDefaults<ApplicationDbContext>();
builder.Services.AddSingleton<InMemoryCacheProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<TestRepositoryObserver>();
builder.Services.AddSingleton<IKyrolusRepositoryObserver>(sp => sp.GetRequiredService<TestRepositoryObserver>());
builder.Services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<InMemoryCacheProvider>());
builder.Services.AddScoped<ICacheKeyContext>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var tenant = http?.Request?.Headers["X-Tenant-Id"].ToString();
    var branch = http?.Request?.Headers["X-Branch-Id"].ToString();

    return new SimpleCacheKeyContext($"tenant={tenant};branch={branch}");
});

var app = builder.Build();

// Create schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    await DataSeeder.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapEntity<Category, KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Category, Guid>, Guid, Guid>();
app.MapEntity<Product, KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>, Guid, Guid>();
app.MapEntity<Review, KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>, Guid, object?>();
app.MapEntity<Customer, KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Customer, Guid>, Guid, Guid>();
app.MapEntity<Order, KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Order, Guid>, Guid, Guid>();
app.MapEntity<OrderLine, KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, OrderLine>, Guid, object?>();
app.MapEntity<Payment, KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Payment, Guid>, Guid, Guid>();
app.MapEntity<ProductCategory, KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, ProductCategory>, Guid, object?>();
app.MapEntity<Role, KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Role, Guid>, Guid, Guid>();
app.MapEntity<User, KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, User, Guid>, Guid, Guid>();
app.MapEntity<Store, KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Store, Guid>, Guid, Guid>();
app.MapEntity<StoreUserRole, KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, StoreUserRole>, Guid, object?>();
app.MapEntity<Tenant, KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Tenant, Guid>, Guid, Guid>();

app.UseHttpsRedirection();

await app.RunAsync();
