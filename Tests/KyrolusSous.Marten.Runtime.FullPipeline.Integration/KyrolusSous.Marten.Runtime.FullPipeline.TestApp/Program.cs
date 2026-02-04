using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Caching.Redis;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Marten.Command.Add;
using KyrolusSous.CQRS.Marten.Command.Remove;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.CQRS.Validation;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.Redis;
using KyrolusSous.DataProtection.Runtime;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Marten.Config;
using KyrolusSous.ExceptionHandling;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.FluentValidation;
using KyrolusSous.ExceptionHandling.Redis;
using KyrolusSous.Logging.Serilog;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Mediator.Runtime.Config;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Auth;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.MenuItems;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.Orders;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Modules;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.SoftDelete;
using KyrolusSous.Repositories.Marten.Runtime;
using KyrolusSous.Validation.FluentValidation;
using KyrolusSous.Validation.Runtime;
using FluentValidation;
using Marten;
using Npgsql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddKyrolusLogging(builder.Configuration);
builder.Host.UseKyrolusLogging();

var postgresConnString = builder.Configuration.GetConnectionString("Marten")
    ?? "Host=localhost;Port=5432;Database=kyrolus_marten_fullpipeline_tests;Username=postgres;Password=postgres";
var authConnString = builder.Configuration.GetConnectionString("Auth") ?? BuildAuthConnectionString(postgresConnString);
var redisConnString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddSingleton(new KyrolusRepositoryCachePolicyRegistry()
    .SetForType<MenuItem>("GetAllAsync", new KyrolusCachePolicy(TimeSpan.FromMinutes(5)))
    .SetForType<MenuItem>("GetByIdAsync", new KyrolusCachePolicy(TimeSpan.FromMinutes(5))));
builder.Services.AddSingleton<IKyrolusRepositoryCachePolicyProvider>(sp => sp.GetRequiredService<KyrolusRepositoryCachePolicyRegistry>());

builder.Services.AddMarten(options =>
{
    options.Connection(postgresConnString);
    options.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
    options.DatabaseSchemaName = "kyrolus_fullpipeline";
    options.Policies.AllDocumentsAreMultiTenanted();

    options.Schema.For<MenuItem>().Identity(x => x.Id);
    options.Schema.For<Order>().Identity(x => x.Id);
    options.Schema.For<Payment>().Identity(x => x.Id);
});

builder.Services.AddKyrolusRedisCacheProvider(redisConnString, options =>
{
    options.KeyPrefix = "kyrolus:fullpipeline";
    options.EnableGracefulFallback = true;
});

builder.Services.AddScoped<IDocumentSession>(sp =>
{
    var store = sp.GetRequiredService<IDocumentStore>();
    var tenantResolver = sp.GetRequiredService<ITenantResolver>();
    var tenantId = tenantResolver.ResolveTenantId() ?? "default";
    return store.LightweightSession(tenantId);
});

builder.Services.AddKyrolusMartenRuntime();
builder.Services.AddSingleton<IKyrolusMartenSoftDeletePolicy>(KyrolusMartenSoftDeletePolicy.IsDeleted());
builder.Services.AddScoped<ITenantResolver, HttpTenantResolver>();

builder.Services.AddKyrolusDefaults();
builder.Services.Configure<KyrolusEndpointKitOptions>(options =>
{
    options.TenantHeaderName = "X-Tenant-Id";
    options.ScopeHeaderName = "X-Scope";
});

builder.Services.AddKyrolusValidationRuntime();
builder.Services.AddKyrolusFluentValidation();
builder.Services.AddKyrolusCqrsValidation();
builder.Services.AddKyrolusCqrsExceptionHandling();
builder.Services.AddKyrolusExceptionHandling();
builder.Services.AddKyrolusFluentValidationExceptionHandling();
builder.Services.AddKyrolusRedisExceptionHandling();
builder.Services.AddKyrolusMediatorFromAssemblies(typeof(Program).Assembly);
builder.Services.AddTransient<IValidator<AddCommand<MenuItem>>, AddMenuItemValidator>();
builder.Services.AddTransient<IValidator<UpdateCommand<MenuItem>>, UpdateMenuItemValidator>();
builder.Services.AddTransient<IValidator<MenuItemPatchCommand>, PatchMenuItemValidator>();
builder.Services.AddTransient<IValidator<MenuItem>, MenuItemValidator>();
builder.Services.AddTransient<IValidator<PlaceOrderCommand>, PlaceOrderValidator>();
builder.Services.AddTransient<IValidator<IKyrolusCommand<Order>>, PlaceOrderCommandInterfaceValidator>();

builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
builder.Services.AddSingleton<TestUserStore>();

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseNpgsql(authConnString);
    options.UseOpenIddict<OpenIddictEntityFrameworkCoreApplication,
        OpenIddictEntityFrameworkCoreAuthorization,
        OpenIddictEntityFrameworkCoreScope,
        OpenIddictEntityFrameworkCoreToken, string>();
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddAuthorization();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<AuthDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");
        options.AllowPasswordFlow();
        options.AcceptAnonymousClients();
        options.RegisterScopes("api");
        if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("OpenIddict:UseEphemeralKeys"))
        {
            options.AddEphemeralEncryptionKey()
                   .AddEphemeralSigningKey();
        }
        else
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        options.UseAspNetCore()
               .DisableTransportSecurityRequirement();

        options.AddEventHandler<OpenIddictServerEvents.HandleTokenRequestContext>(builder =>
        {
            builder.UseScopedHandler<PasswordGrantHandler>();
        });
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var dataProtection = builder.Services.AddKyrolusDataProtection(options =>
{
    options.ApplicationName = "Kyrolus.Marten.FullPipeline";
});
dataProtection.AddKyrolusDataProtectionRedis(redisConnString, "kyrolus:dataprotection");
dataProtection.AddKyrolusDataProtectionKeyRingRefreshHooks();

builder.Services.AddKyrolus(builder =>
{
    builder.AddModule<MenuItemModule, MenuItem, MenuItem, Guid>(new KyrolusMartenApiConfig<MenuItem>
    {
        ApiName = "MenuItems",
        Prefix = "api",
        Route = "menu-item",
        UseEnrichedCustomResponse = false,
        QueryById = new GetByIdQuery<MenuItem, Guid>(Guid.Empty),
        QueryAll = new GetAllQuery<MenuItem>(),
        QueryByProperty = new GetAllQuery<MenuItem>(),
        AddCommand = new AddCommand<MenuItem>(new MenuItem()),
        AddRangeCommand = new AddRangeCommand<MenuItem>(Array.Empty<MenuItem>()),
        UpdateCommand = new UpdateCommand<MenuItem>(new MenuItem()),
        UpdateRangeCommand = new UpdateRangeCommand<MenuItem>(Array.Empty<MenuItem>()),
        RemoveCommand = new RemoveByIdCommand<MenuItem, Guid>(Guid.Empty),
        RemoveRangeCommand = new RemoveRangeCommand<MenuItem>(Array.Empty<MenuItem>()),
        PatchCommand = new MenuItemPatchCommand(),
        UpdateActiviationStateCommand = new SetMenuItemActiveCommand(),
        EnableQueryEndpoints = true,
        EnablePagedEndpoints = false,
        EnableSeekEndpoints = false,
        EnableCountEndpoint = false,
        EnableHeadEndpoint = false,
        EnableBulkEndpoints = false,
        EnableCompositeKeyEndpoints = false,
        EnableSoftDeleteEndpoints = true,
        UseSoftDeleteForDelete = true,
        Endpoints = new[]
        {
            EndpointNames.GetAll,
            EndpointNames.GetById,
            EndpointNames.Add,
            EndpointNames.Update,
            EndpointNames.Patch,
            EndpointNames.Delete,
            EndpointNames.GetDeleted,
            EndpointNames.Restore,
            EndpointNames.Query
        }
    });
});

var app = builder.Build();

await EnsureDatabaseExistsAsync(postgresConnString);
await EnsureDatabaseExistsAsync(authConnString);

using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseKyrolusExceptionHandling();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapKyrolus();

app.MapPost("/api/orders", async (PlaceOrderRequest request, IKyrolusMediatorSender mediator, CancellationToken ct) =>
{
    var command = new PlaceOrderCommand(request.CustomerEmail, request.Lines, request.PaymentMethod);
    var result = await mediator.SendAsync(command, ct).ConfigureAwait(false);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/api/orders/{id:guid}", async (Guid id, IKyrolusMediatorSender mediator, CancellationToken ct) =>
{
    var result = await mediator.SendAsync(new GetOrderByIdQuery(id), ct).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/diagnostics/protect", (ProtectRequest request, IKyrolusTenantDataProtectionProvider provider, ITenantResolver resolver) =>
{
    var tenantId = resolver.ResolveTenantId() ?? "default";
    var protector = provider.CreateProtector(tenantId, "fullpipeline");
    var bytes = System.Text.Encoding.UTF8.GetBytes(request.Value);
    var protectedBytes = protector.Protect(bytes);
    var protectedValue = Convert.ToBase64String(protectedBytes);
    var unprotected = System.Text.Encoding.UTF8.GetString(protector.Unprotect(Convert.FromBase64String(protectedValue)));
    return Results.Ok(new ProtectResponse(tenantId, protectedValue, unprotected));
}).RequireAuthorization();

app.MapGet("/api/diagnostics/secure", () => Results.Ok(new { status = "ok" }))
    .RequireAuthorization();

app.MapGet("/api/diagnostics/exception", () =>
{
    throw new KyrolusBadRequestException("Bad input", "Simulated failure");
});

app.UseHttpsRedirection();

await app.RunAsync();

static async Task EnsureDatabaseExistsAsync(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var dbName = builder.Database;
    if (string.IsNullOrWhiteSpace(dbName))
    {
        return;
    }

    var adminBuilder = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = "postgres"
    };

    await using var conn = new NpgsqlConnection(adminBuilder.ConnectionString);
    await conn.OpenAsync();

    await using var existsCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", conn);
    existsCmd.Parameters.AddWithValue("name", dbName);
    var exists = await existsCmd.ExecuteScalarAsync();
    if (exists is not null)
    {
        return;
    }

    var safeDbName = dbName.Replace("\"", "\"\"");
    await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{safeDbName}\"", conn);
    try
    {
        await createCmd.ExecuteNonQueryAsync();
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        // Database was created by another concurrent test host.
    }
}

static string BuildAuthConnectionString(string baseConnectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
    builder.Database = $"{builder.Database}_auth";
    return builder.ConnectionString;
}

public sealed class SetMenuItemActiveCommand : IKyrolusCommand<bool>
{
    public Guid Id { get; set; }
    public bool Active { get; set; }
}

public partial class Program { }
