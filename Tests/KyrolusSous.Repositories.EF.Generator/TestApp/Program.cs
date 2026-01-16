var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Keep a single opened in-memory SQLite connection for the app lifetime
builder.Services.AddSingleton(_ =>
{
    var conn = new SqliteConnection("DataSource=:memory:");
    conn.Open();
    return conn;
});

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var conn = sp.GetRequiredService<SqliteConnection>();
    options.UseSqlite(conn);
});
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
builder.Services.AddGeneratedKyrolusRepositories();

var app = builder.Build();

// Create schema on startup for the in-memory database
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
app.MapEntity<Category, CategoryRepository, Guid>();
app.MapEntity<Product, ProductRepository, Guid>();
app.MapEntity<Review, ReviewRepository, Guid>();
app.MapEntity<Customer, CustomerRepository, Guid>();
app.MapEntity<Order, OrderRepository, Guid>();
app.MapEntity<OrderLine, OrderLineRepository, Guid>();
app.MapEntity<Payment, PaymentRepository, Guid>();
app.MapEntity<ProductCategory, ProductCategoryRepository, Guid>();
app.MapEntity<Role, RoleRepository, Guid>();
app.MapEntity<User, UserRepository, Guid>();
app.MapEntity<Store, StoreRepository, Guid>();
app.MapEntity<StoreUserRole, StoreUserRoleRepository, Guid>();
app.MapEntity<Tenant, TenantRepository, Guid>();

app.UseHttpsRedirection();


await app.RunAsync();
