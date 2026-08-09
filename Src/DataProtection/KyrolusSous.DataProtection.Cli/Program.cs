using System.Reflection;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.AzureKeyVault;
using KyrolusSous.DataProtection.AzureStorage;
using KyrolusSous.DataProtection.AwsKms;
using KyrolusSous.DataProtection.CustomXml;
using KyrolusSous.DataProtection.EntityFramework;
using KyrolusSous.DataProtection.Ephemeral;
using KyrolusSous.DataProtection.FileSystem;
using KyrolusSous.DataProtection.GoogleKms;
using KyrolusSous.DataProtection.Marten;
using KyrolusSous.DataProtection.Redis;
using KyrolusSous.DataProtection.Runtime;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var provider = (GetOption(args, "--provider") ?? "file").Trim().ToLowerInvariant();
        var appName = GetOption(args, "--app") ?? "default";
        var path = GetOption(args, "--path");
        var redis = GetOption(args, "--redis");
        var key = GetOption(args, "--key") ?? "DataProtection-Keys";
        var azureConn = GetOption(args, "--azure-conn");
        var azureContainer = GetOption(args, "--azure-container");
        var azureBlob = GetOption(args, "--azure-blob");
        var keyVault = GetOption(args, "--keyvault");
        var keyVaultCredentialMode = GetOption(args, "--keyvault-credential");
        var keyVaultTenantId = GetOption(args, "--keyvault-tenant-id");
        var keyVaultClientId = GetOption(args, "--keyvault-client-id");
        var keyVaultClientSecret = GetOption(args, "--keyvault-client-secret");
        var keyVaultManagedIdentity = GetOption(args, "--keyvault-managed-identity");
        var awsKmsKey = GetOption(args, "--aws-kms-key");
        var awsKmsContext = GetOption(args, "--aws-kms-context");
        var gcpKmsKey = GetOption(args, "--gcp-kms-key");
        var efConnection = GetOption(args, "--ef-connection");
        var efProvider = GetOption(args, "--ef-provider");
        var efMySqlVersion = GetOption(args, "--ef-mysql-version");
        var martenConn = GetOption(args, "--marten-conn");
        var martenSchema = GetOption(args, "--marten-schema");
        var martenTenant = GetOption(args, "--marten-tenant");
        var martenSession = GetOption(args, "--marten-session");
        var xmlRepoType = GetOption(args, "--xml-repo-type");
        var xmlRepoAssembly = GetOption(args, "--xml-repo-assembly");
        var lifetime = ParseLifetime(GetOption(args, "--lifetime") ?? GetOption(args, "--lifetime-days"));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var services = new ServiceCollection();
        var builder = services.AddKyrolusDataProtection(options =>
        {
            options.ApplicationName = appName;
            options.DefaultKeyLifetime = lifetime;
        });

        var providerArgs = new ProviderArgs(
            provider,
            path,
            redis,
            key,
            azureConn,
            azureContainer,
            azureBlob,
            keyVault,
            keyVaultCredentialMode,
            keyVaultTenantId,
            keyVaultClientId,
            keyVaultClientSecret,
            keyVaultManagedIdentity,
            awsKmsKey,
            awsKmsContext,
            gcpKmsKey,
            efConnection,
            efProvider,
            efMySqlVersion,
            martenConn,
            martenSchema,
            martenTenant,
            martenSession,
            xmlRepoType,
            xmlRepoAssembly);

        ConfigureProvider(builder, providerArgs);

        await using var providerScope = services.BuildServiceProvider();
        var keyManager = providerScope.GetRequiredService<IKyrolusDataProtectionKeyManager>();
        var repository = providerScope.GetRequiredService<IKyrolusDataProtectionKeyRepository>();

        switch (command)
        {
            case "list":
                await ListAsync(keyManager, cts.Token).ConfigureAwait(false);
                return 0;
            case "rotate":
                await RotateAsync(keyManager, lifetime, cts.Token).ConfigureAwait(false);
                return 0;
            case "revoke":
                await RevokeAsync(keyManager, GetOption(args, "--id"), GetOption(args, "--reason"), cts.Token)
                    .ConfigureAwait(false);
                return 0;
            case "revoke-all":
                await RevokeAllAsync(keyManager, GetOption(args, "--reason"), cts.Token).ConfigureAwait(false);
                return 0;
            case "export":
                await ExportAsync(repository, GetOption(args, "--out"), cts.Token).ConfigureAwait(false);
                return 0;
            case "import":
                await ImportAsync(repository, GetOption(args, "--in"), cts.Token).ConfigureAwait(false);
                return 0;
            default:
                PrintHelp();
                return 1;
        }
    }

    private static void ConfigureProvider(
        KyrolusDataProtectionBuilder builder,
        ProviderArgs args)
    {
        switch (args.Provider)
        {
            case "file":
                if (string.IsNullOrWhiteSpace(args.Path))
                    throw new ArgumentException("--path is required for file provider.");
                builder.AddKyrolusDataProtectionFileSystem(args.Path);
                break;
            case "redis":
                if (string.IsNullOrWhiteSpace(args.Redis))
                    throw new ArgumentException("--redis is required for redis provider.");
                builder.AddKyrolusDataProtectionRedis(args.Redis, args.Key);
                break;
            case "azure-blob":
            case "azurestorage":
                if (string.IsNullOrWhiteSpace(args.AzureConn))
                    throw new ArgumentException("--azure-conn is required for azure-blob provider.");
                if (string.IsNullOrWhiteSpace(args.AzureContainer))
                    throw new ArgumentException("--azure-container is required for azure-blob provider.");
                builder.AddKyrolusDataProtectionAzureBlobStorage(
                    args.AzureConn,
                    args.AzureContainer,
                    string.IsNullOrWhiteSpace(args.AzureBlob) ? "dataprotection-keys.xml" : args.AzureBlob);
                break;
            case "azure-keyvault":
            case "keyvault":
                if (string.IsNullOrWhiteSpace(args.KeyVault))
                    throw new ArgumentException("--keyvault is required for azure-keyvault provider.");
                builder.AddKyrolusDataProtectionAzureKeyVault(
                    args.KeyVault,
                    ResolveKeyVaultCredential(
                        args.KeyVaultCredentialMode,
                        args.KeyVaultTenantId,
                        args.KeyVaultClientId,
                        args.KeyVaultClientSecret,
                        args.KeyVaultManagedIdentity));
                break;
            case "aws-kms":
                if (string.IsNullOrWhiteSpace(args.AwsKmsKey))
                    throw new ArgumentException("--aws-kms-key is required for aws-kms provider.");
                builder.AddKyrolusDataProtectionAwsKms(args.AwsKmsKey, ParseKeyValuePairs(args.AwsKmsContext));
                break;
            case "gcp-kms":
                if (string.IsNullOrWhiteSpace(args.GcpKmsKey))
                    throw new ArgumentException("--gcp-kms-key is required for gcp-kms provider.");
                builder.AddKyrolusDataProtectionGoogleKms(args.GcpKmsKey);
                break;
            case "ef":
            case "entityframework":
                if (string.IsNullOrWhiteSpace(args.EfConnection))
                    throw new ArgumentException("--ef-connection is required for ef provider.");
                builder.AddKyrolusDataProtectionEntityFramework(
                    options => ConfigureEf(options, args.EfProvider, args.EfConnection, args.EfMySqlVersion));
                break;
            case "ephemeral":
                builder.AddKyrolusEphemeralDataProtection();
                break;
            case "custom-xml":
            case "xml":
                builder.AddKyrolusDataProtectionXmlRepository(
                    ResolveXmlRepository(args.XmlRepoType, args.XmlRepoAssembly));
                break;
            case "marten":
                if (string.IsNullOrWhiteSpace(args.MartenConn))
                    throw new ArgumentException("--marten-conn is required for marten provider.");

                builder.AddKyrolusDataProtectionMarten(
                    args.MartenConn,
                    args.MartenSchema,
                    options =>
                    {
                        options.TenantId = args.MartenTenant;
                        options.UseLightweightSession = !string.Equals(
                            args.MartenSession,
                            "identity",
                            StringComparison.OrdinalIgnoreCase);
                    });
                break;
            default:
                throw new ArgumentException(
                    "Provider must be 'file', 'redis', 'azure-blob', 'azure-keyvault', 'aws-kms', 'gcp-kms', 'ef', 'ephemeral', 'custom-xml', or 'marten'.");
        }
    }

    private sealed record ProviderArgs(
        string Provider,
        string? Path,
        string? Redis,
        string Key,
        string? AzureConn,
        string? AzureContainer,
        string? AzureBlob,
        string? KeyVault,
        string? KeyVaultCredentialMode,
        string? KeyVaultTenantId,
        string? KeyVaultClientId,
        string? KeyVaultClientSecret,
        string? KeyVaultManagedIdentity,
        string? AwsKmsKey,
        string? AwsKmsContext,
        string? GcpKmsKey,
        string? EfConnection,
        string? EfProvider,
        string? EfMySqlVersion,
        string? MartenConn,
        string? MartenSchema,
        string? MartenTenant,
        string? MartenSession,
        string? XmlRepoType,
        string? XmlRepoAssembly);

    private static async Task ListAsync(
        IKyrolusDataProtectionKeyManager keyManager,
        CancellationToken cancellationToken)
    {
        var keys = await keyManager.GetAllKeysAsync(cancellationToken).ConfigureAwait(false);
        if (keys.Count == 0)
        {
            Console.WriteLine("No keys found.");
            return;
        }

        foreach (var key in keys)
        {
            Console.WriteLine(
                $"{key.KeyId} active={key.ActivationDate:u} expires={key.ExpirationDate:u} revoked={key.IsRevoked}");
        }
    }

    private static async Task RotateAsync(
        IKyrolusDataProtectionKeyManager keyManager,
        TimeSpan? lifetime,
        CancellationToken cancellationToken)
    {
        var key = await keyManager.RotateKeyAsync(lifetime, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Rotated key: {key.KeyId}");
    }

    private static async Task RevokeAsync(
        IKyrolusDataProtectionKeyManager keyManager,
        string? idValue,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idValue, out var keyId))
        {
            throw new ArgumentException("--id must be a valid GUID.");
        }

        await keyManager.RevokeKeyAsync(keyId, reason, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Revoked key: {keyId}");
    }

    private static async Task RevokeAllAsync(
        IKyrolusDataProtectionKeyManager keyManager,
        string? reason,
        CancellationToken cancellationToken)
    {
        await keyManager.RevokeAllKeysAsync(DateTimeOffset.UtcNow, reason, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Revoked all keys.");
    }

    private static async Task ExportAsync(
        IKyrolusDataProtectionKeyRepository repository,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        var documents = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(documents, new JsonSerializerOptions { WriteIndented = true });

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(json);
            return;
        }

        await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Exported {documents.Count} keys to {outputPath}");
    }

    private static async Task ImportAsync(
        IKyrolusDataProtectionKeyRepository repository,
        string? inputPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("--in is required for import.");
        }

        var json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var documents = JsonSerializer.Deserialize<IReadOnlyList<KyrolusDataProtectionKeyDocument>>(json)
            ?? Array.Empty<KyrolusDataProtectionKeyDocument>();

        await repository.ImportAsync(documents, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Imported {documents.Count} keys from {inputPath}");
    }

    private static TimeSpan? ParseLifetime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var days))
        {
            return TimeSpan.FromDays(days);
        }

        return TimeSpan.TryParse(value, out var parsed) ? parsed : null;
    }

    private static IReadOnlyDictionary<string, string>? ParseKeyValuePairs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var pairs = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pairs.Length == 0)
        {
            return null;
        }

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                throw new ArgumentException(
                    "Invalid --aws-kms-context format. Use 'key=value;key2=value2'.");
            }

            dictionary[parts[0]] = parts[1];
        }

        return dictionary;
    }

    private static void ConfigureEf(
        DbContextOptionsBuilder options,
        string? provider,
        string connectionString,
        string? efMySqlVersion)
    {
        var value = provider?.Trim().ToLowerInvariant() ?? "sqlite";
        switch (value)
        {
            case "sqlite":
                options.UseSqlite(connectionString);
                break;
            case "sqlserver":
            case "mssql":
                options.UseSqlServer(connectionString);
                break;
            case "postgres":
            case "postgresql":
            case "pgsql":
                ConfigureEfPostgres(options, connectionString);
                break;
            case "mysql":
            case "mariadb":
                ConfigureEfMySql(options, connectionString, efMySqlVersion);
                break;
            default:
                throw new ArgumentException(
                    "--ef-provider must be 'sqlite', 'sqlserver', 'postgres', or 'mysql'.");
        }
    }

    private static void ConfigureEfPostgres(DbContextOptionsBuilder options, string connectionString)
    {
        var extensionType = LoadType(
            "Npgsql.EntityFrameworkCore.PostgreSQL.NpgsqlDbContextOptionsBuilderExtensions",
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        var method = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "UseNpgsql"
                && m.GetParameters().Length >= 2
                && m.GetParameters()[0].ParameterType == typeof(DbContextOptionsBuilder));

        if (method is null)
        {
            throw new InvalidOperationException(
                "UseNpgsql not found. Add Npgsql.EntityFrameworkCore.PostgreSQL to the CLI project.");
        }

        var args = method.GetParameters().Length switch
        {
            2 => new object?[] { options, connectionString },
            _ => new object?[] { options, connectionString, null }
        };

        method.Invoke(null, args);
    }

    private static void ConfigureEfMySql(
        DbContextOptionsBuilder options,
        string connectionString,
        string? versionText)
    {
        var extensionType = LoadType(
            "Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlDbContextOptionsBuilderExtensions",
            "Pomelo.EntityFrameworkCore.MySql");
        var serverVersionType = LoadType(
            "Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerVersion",
            "Pomelo.EntityFrameworkCore.MySql");

        object? serverVersion = null;
        if (!string.IsNullOrWhiteSpace(versionText))
        {
            var parse = serverVersionType.GetMethod("Parse", new[] { typeof(string) });
            if (parse is null)
            {
                throw new InvalidOperationException(
                    "ServerVersion.Parse not found. Update Pomelo.EntityFrameworkCore.MySql.");
            }

            serverVersion = parse.Invoke(null, new object?[] { versionText });
        }
        else
        {
            var autoDetect = serverVersionType.GetMethod("AutoDetect", new[] { typeof(string) });
            if (autoDetect is null)
            {
                throw new InvalidOperationException(
                    "ServerVersion.AutoDetect not found. Update Pomelo.EntityFrameworkCore.MySql.");
            }

            serverVersion = autoDetect.Invoke(null, new object?[] { connectionString });
        }

        if (serverVersion is null)
        {
            throw new InvalidOperationException("Could not resolve MySQL server version.");
        }

        var method = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "UseMySql"
                && m.GetParameters().Length >= 3
                && m.GetParameters()[0].ParameterType == typeof(DbContextOptionsBuilder));

        if (method is null)
        {
            throw new InvalidOperationException(
                "UseMySql not found. Add Pomelo.EntityFrameworkCore.MySql to the CLI project.");
        }

        var args = method.GetParameters().Length switch
        {
            3 => new object?[] { options, connectionString, serverVersion },
            _ => new object?[] { options, connectionString, serverVersion, null }
        };

        method.Invoke(null, args);
    }

    private static Type LoadType(string typeName, string assemblyName)
    {
        var type = Type.GetType(typeName, throwOnError: false);
        if (type is not null)
        {
            return type;
        }

        try
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            type = assembly.GetType(typeName, throwOnError: false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required assembly '{assemblyName}' not found.", ex);
        }

        return type ?? throw new InvalidOperationException(
            $"Type '{typeName}' not found in '{assemblyName}'.");
    }

    private static IXmlRepository ResolveXmlRepository(string? typeName, string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("--xml-repo-type is required for custom-xml provider.");
        }

        Type? type = null;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
        }
        else
        {
            type = Type.GetType(typeName, throwOnError: false);
        }

        if (type is null)
        {
            throw new InvalidOperationException($"Type '{typeName}' could not be loaded.");
        }

        if (!typeof(IXmlRepository).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Type '{typeName}' does not implement IXmlRepository.");
        }

        if (Activator.CreateInstance(type) is not IXmlRepository repository)
        {
            throw new InvalidOperationException($"Type '{typeName}' must have a public parameterless constructor.");
        }

        return repository;
    }

    private static TokenCredential ResolveKeyVaultCredential(
        string? mode,
        string? tenantId,
        string? clientId,
        string? clientSecret,
        string? managedIdentityId)
    {
        var normalized = mode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized is "client-secret")
        {
            if (string.IsNullOrWhiteSpace(tenantId)
                || string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException(
                    "--keyvault-tenant-id, --keyvault-client-id, and --keyvault-client-secret are required for client-secret mode.");
            }

            return new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        if (normalized is "managed-identity")
        {
            return string.IsNullOrWhiteSpace(managedIdentityId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentityId));
        }

        return new DefaultAzureCredential();
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name)
        => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static void PrintHelp()
    {
        Console.WriteLine("Kyrolus DataProtection CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dp list --provider file --path <dir> [--app name]");
        Console.WriteLine("  dp rotate --provider redis --redis <conn> [--key name] [--lifetime-days 90]");
        Console.WriteLine("  dp revoke --provider file --path <dir> --id <guid> [--reason text]");
        Console.WriteLine("  dp revoke-all --provider redis --redis <conn> [--reason text]");
        Console.WriteLine("  dp export --provider file --path <dir> [--out keys.json]");
        Console.WriteLine("  dp import --provider redis --redis <conn> --in keys.json");
        Console.WriteLine("  dp list --provider azure-blob --azure-conn <conn> --azure-container <container>");
        Console.WriteLine("  dp rotate --provider azure-keyvault --keyvault <key-identifier>");
        Console.WriteLine("  dp list --provider aws-kms --aws-kms-key <key-id> [--aws-kms-context k=v;k2=v2]");
        Console.WriteLine("  dp list --provider gcp-kms --gcp-kms-key <crypto-key>");
        Console.WriteLine("  dp list --provider ef --ef-provider sqlite --ef-connection <conn>");
        Console.WriteLine("  dp list --provider ef --ef-provider sqlserver --ef-connection <conn>");
        Console.WriteLine("  dp list --provider ef --ef-provider postgres --ef-connection <conn>");
        Console.WriteLine("  dp list --provider ef --ef-provider mysql --ef-connection <conn> [--ef-mysql-version 8.0.36]");
        Console.WriteLine("  dp list --provider marten --marten-conn <conn> [--marten-schema <schema>]");
        Console.WriteLine("  dp list --provider marten --marten-conn <conn> --marten-tenant <tenant> --marten-session identity");
        Console.WriteLine("  dp list --provider ephemeral");
        Console.WriteLine("  dp list --provider custom-xml --xml-repo-type <Full.TypeName> [--xml-repo-assembly <path>]");
        Console.WriteLine("  dp rotate --provider azure-keyvault --keyvault <key-identifier> --keyvault-credential client-secret --keyvault-tenant-id <id> --keyvault-client-id <id> --keyvault-client-secret <secret>");
        Console.WriteLine("  dp rotate --provider azure-keyvault --keyvault <key-identifier> --keyvault-credential managed-identity --keyvault-managed-identity <client-id>");
    }
}
