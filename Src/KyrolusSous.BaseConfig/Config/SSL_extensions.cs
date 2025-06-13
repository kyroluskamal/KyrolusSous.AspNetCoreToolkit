using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KyrolusSous.BaseConfig.Config;

public static class SslExtensions
{
    public static WebApplicationBuilder ConfigureHttps(this WebApplicationBuilder builder, int port = 443,
    string certificatePath = "/https/aspnetapp.pfx",
     string certificatePassword = "codingbible")
    {
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(port, listenOptions =>
            {
                listenOptions.UseHttps(certificatePath, certificatePassword);
            });
        });

        return builder;
    }


    public static void AllowServiceNamesInSSL(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("default")
         .ConfigurePrimaryHttpMessageHandler(() =>
         {
             var handler = new HttpClientHandler
             {
                 // Customize the handler to validate or bypass SSL certificates
                 ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                 {
                     // List of allowed SANs or Common Names (CN) for internal services
                     var allowedServiceNames = new List<string>
                     {
                         "CN=localhost",
                         "CN=admins",
                         "CN=dashboard_gateway",
                         "CN=pgbouncer",
                         "CN=admindb",
                         "CN=redis",
                         "CN=data_protection_db"
                     };

                     // Check if the certificate has the required CN or SAN (e.g., for internal services)
                     if (cert != null && cert.Extensions != null)
                     {
                         Log.Information(" ------------------------- There is certificates with this request {CN} -----------------------------", cert.Subject);
                         var subjectAlternativeNames = cert.Extensions["2.5.29.17"]?.Format(false);
                         Log.Information(" ---------------------------- subjectAlternativeNames {subjectAlternativeNames} ------------------------", subjectAlternativeNames);
                         if (subjectAlternativeNames != null)
                         {
                             foreach (var serviceName in allowedServiceNames)
                             {
                                 if (subjectAlternativeNames.Contains(serviceName))
                                 {
                                     // The certificate is accepted if the CN/SAN matches one of the allowed service names
                                     return true;
                                 }
                             }
                         }

                         // Check the subject for allowed service names
                         if (allowedServiceNames.Exists(serviceName => cert.Subject.Contains(serviceName)))
                         {
                             return true;
                         }
                     }

                     Log.Warning("There is not certificates with this request");
                     // For development, you can bypass the validation altogether (NOT recommended for production)
                     return builder.Environment.IsDevelopment() || errors == System.Net.Security.SslPolicyErrors.None;
                 }
             };

             return handler;
         });
    }
}
