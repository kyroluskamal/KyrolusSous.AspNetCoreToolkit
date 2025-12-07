using System.Reflection;
using KyrolusSous.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerService(options =>
{
    options.EnableApiVersioning = true;
    options.ApiVersions = new List<ApiVersionInfo>
    {
        new ApiVersionInfo
        {
            Version = "v1",
            Title = "My API - V1 (Full Test)",
            Description = "This is the first version of my API.",
            TermsOfServiceUrl = "https://example.com/terms",
            ContactName = "Support Team",
            ContactEmail = "support@example.com",
            ContactUrl = "https://example.com/contact"
        },
        new ApiVersionInfo
        {
            Version = "v2",
            Title = "My API - V2 (Full Test)",
            Description = "Version 2 with new features.",
            LicenseName = "MIT License",
            LicenseUrl = "https://example.com/license"
        }
    };

    options.EnableXmlComments = true;
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.XmlDocAbsolutePaths.Add(Path.Combine(AppContext.BaseDirectory, xmlFile));

    options.EnableJwtBearerAuth = true;
    options.JwtBearerDescription = "My custom JWT auth description.";

    options.EnableOAuth2Auth = true;
    options.OAuth2SchemeName = "CustomOAuth2";
    options.OAuth2Description = "Custom OAuth2 Authorization Code flow.";
    options.OAuth2Flow = "authorizationCode";
    options.OAuth2AuthorizationUrl = "https://auth.myapi.com/oauth/authorize";
    options.OAuth2TokenUrl = "https://auth.myapi.com/oauth/token";
    options.OAuth2Scopes = new Dictionary<string, string>
    {
        { "api.read", "Grants read access to API" },
        { "api.write", "Grants write access to API" }
    };

    options.EnableAnnotations = true;
    options.EnableNullableReferenceTypesSupport = true;
    options.SupportNonNullableReferenceTypes = true;

    options.UiRoutePrefix = "my-docs";
    options.UiDocumentTitle = "Custom API Documentation";
    options.UiDisplayRequestDuration = true;
    options.UiEnableDeepLinking = false;
    options.UiEnableFilter = true;
    options.UiShowExtensions = true;
    options.UiEnablePersistAuthorization = true;
    options.UiSupportedSubmitMethods = new List<string> { "get", "post", "put", "delete", "patch" };
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = "https://localhost:5001";
    options.RequireHttpsMetadata = false;
    options.Audience = "myapi";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TestPolicy", policy => policy.RequireClaim("test", "value"));
});

var app = builder.Build();

app.UseConfiguredSwaggerUI(uiOptions =>
{
    uiOptions.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

await app.RunAsync();
/// <summary>Represents a weather forecast.</summary>
internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
public partial class Program;
