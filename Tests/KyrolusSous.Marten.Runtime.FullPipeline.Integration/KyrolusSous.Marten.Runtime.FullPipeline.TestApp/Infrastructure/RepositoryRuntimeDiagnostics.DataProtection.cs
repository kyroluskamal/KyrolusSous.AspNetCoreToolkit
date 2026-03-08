using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.Runtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunDataProtectionRuntimeAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"kyrolus-dp-diag-{Guid.NewGuid():N}");
        var keyDirectory = Path.Combine(rootDirectory, "keys");
        var escrowDirectory = Path.Combine(rootDirectory, "escrow");
        var encryptedEscrowDirectory = Path.Combine(rootDirectory, "encrypted");
        var backupDirectory = Path.Combine(rootDirectory, "backup");

        Directory.CreateDirectory(rootDirectory);

        try
        {
            var notifier = new DataProtectionRuntimeNotifier();
            var keyBytes = CreateDiagnosticsAesKey();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IKyrolusKeyRingRefreshNotifier>(notifier);

            var builder = services.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = "repository-runtime-dataprotection";
                options.DefaultKeyLifetime = TimeSpan.FromDays(14);
                options.AutoGenerateKeys = false;
            });

            builder.DataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
            builder
                .AddKyrolusDataProtectionInstrumentation()
                .AddKyrolusDataProtectionHealthChecks(name: "diag-dataprotection")
                .AddKyrolusDataProtectionTenantIsolation(options =>
                {
                    options.PurposePrefix = "tenant-scope";
                    options.UseTenantPrefix = true;
                })
                .AddKyrolusDataProtectionFileKeyEscrow(escrowDirectory)
                .AddKyrolusDataProtectionFileKeyEscrowEncrypted(encryptedEscrowDirectory, options =>
                {
                    options.EncryptionKeyBase64 = Convert.ToBase64String(keyBytes);
                })
                .AddKyrolusDataProtectionKeyRingRefreshHooks(options =>
                {
                    options.Enabled = true;
                    options.EnableCrossInstanceNotifications = true;
                    options.PublishLocalChanges = true;
                    options.InstanceId = "diag-instance";
                });

            using var provider = services.BuildServiceProvider();

            var runtimeOptions = provider.GetRequiredService<IOptions<KyrolusDataProtectionOptions>>().Value;
            var tenantOptions = provider.GetRequiredService<IOptions<KyrolusDataProtectionTenantOptions>>().Value;
            var keyManager = provider.GetRequiredService<IKyrolusDataProtectionKeyManager>();
            var repository = provider.GetRequiredService<IKyrolusDataProtectionKeyRepository>();
            var protectorFactory = provider.GetRequiredService<IKyrolusDataProtectorFactory>();
            var tenantProvider = provider.GetRequiredService<IKyrolusTenantDataProtectionProvider>();
            var backupService = provider.GetRequiredService<KyrolusDataProtectionKeyBackupService>();
            var instrumentation = provider.GetRequiredService<KyrolusDataProtectionInstrumentation>();
            var healthChecks = provider.GetRequiredService<HealthCheckService>();
            var decoratedKeyManager = provider.GetRequiredService<IKeyManager>();
            var instanceId = provider.GetRequiredService<KyrolusDataProtectionInstanceId>();

            Require(runtimeOptions.ApplicationName == "repository-runtime-dataprotection", "ApplicationName should match.", ref checks);
            Require(runtimeOptions.DefaultKeyLifetime == TimeSpan.FromDays(14), "Default key lifetime should match.", ref checks);
            Require(runtimeOptions.AutoGenerateKeys == false, "AutoGenerateKeys should match.", ref checks);
            Require(tenantOptions.UseTenantPrefix, "Tenant prefix should be enabled.", ref checks);
            Require(tenantOptions.PurposePrefix == "tenant-scope", "Tenant prefix should match.", ref checks);
            Require(decoratedKeyManager is KyrolusKeyManagerRefreshDecorator, "IKeyManager should be decorated.", ref checks);
            Require(protectorFactory is KyrolusInstrumentedDataProtectorFactory, "Protector factory should be instrumented.", ref checks);
            Require(instanceId.Value == "diag-instance", "Configured instance id should be preserved.", ref checks);
            Require(instrumentation is not null, "Instrumentation should resolve.", ref checks);

            var activationDate = DateTimeOffset.UtcNow.AddMinutes(-5);
            var createdKey = await keyManager.CreateKeyAsync(activationDate, TimeSpan.FromDays(5), cancellationToken).ConfigureAwait(false);
            var rotatedKey = await keyManager.RotateKeyAsync(TimeSpan.FromDays(2), cancellationToken).ConfigureAwait(false);
            var allKeys = await keyManager.GetAllKeysAsync(cancellationToken).ConfigureAwait(false);
            var loadedKey = await keyManager.GetKeyAsync(createdKey.KeyId, cancellationToken).ConfigureAwait(false);

            Require(createdKey.ExpirationDate - createdKey.ActivationDate == TimeSpan.FromDays(5), "Created key lifetime should match.", ref checks);
            Require(rotatedKey.KeyId != createdKey.KeyId, "RotateKeyAsync should create a new key.", ref checks);
            Require(allKeys.Count >= 2, "Two keys should be present.", ref checks);
            Require(loadedKey is not null && loadedKey.KeyId == createdKey.KeyId, "GetKeyAsync should return the created key.", ref checks);

            var protector = protectorFactory.CreateProtector("diagnostics-purpose");
            var payload = Encoding.UTF8.GetBytes("pipeline-payload");
            var protectedPayload = protector.Protect(payload);
            var unprotectedPayload = protector.Unprotect(protectedPayload);
            Require(Encoding.UTF8.GetString(unprotectedPayload) == "pipeline-payload", "Protect/unprotect should round-trip.", ref checks);
            ExpectThrows<ArgumentException>(
                () => protectorFactory.CreateProtector(" "),
                "CreateProtector should reject blank purposes.",
                ref checks);
            ExpectThrows<CryptographicException>(
                () => protector.Unprotect(RandomNumberGenerator.GetBytes(32)),
                "Unprotect should fail for invalid payloads.",
                ref checks);

            var tenantProtector = tenantProvider.CreateProtector(tenantId, "orders");
            var otherTenantProtector = tenantProvider.CreateProtector($"{tenantId}-other", "orders");
            var tenantPayload = tenantProtector.Protect(payload);
            Require(
                Encoding.UTF8.GetString(tenantProtector.Unprotect(tenantPayload)) == "pipeline-payload",
                "Tenant-scoped protector should round-trip.",
                ref checks);
            ExpectThrows<CryptographicException>(
                () => otherTenantProtector.Unprotect(tenantPayload),
                "Tenant isolation should reject foreign payloads.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => tenantProvider.CreateProtector(" ", "orders"),
                "Tenant provider should reject blank tenant ids.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => tenantProvider.CreateProtector(tenantId, " "),
                "Tenant provider should reject blank purposes.",
                ref checks);

            await keyManager.RevokeKeyAsync(createdKey.KeyId, "manual", cancellationToken).ConfigureAwait(false);
            await keyManager.RevokeAllKeysAsync(DateTimeOffset.UtcNow.AddMinutes(1), "global", cancellationToken).ConfigureAwait(false);
            Require(notifier.PublishedSignals.Count >= 4, "Key mutations should publish refresh notifications.", ref checks);

            var exportedDocuments = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
            Require(exportedDocuments.Count >= 2, "Repository export should contain keys.", ref checks);
            var importedElement = XElement.Parse(exportedDocuments[0].Xml);
            importedElement.SetAttributeValue("id", Guid.NewGuid().ToString("D"));
            await repository.ImportAsync(
                [
                    new KyrolusDataProtectionKeyDocument("ignored-empty", " "),
                    new KyrolusDataProtectionKeyDocument("imported-copy", importedElement.ToString(SaveOptions.DisableFormatting))
                ],
                cancellationToken).ConfigureAwait(false);
            checks++;

            await backupService.ExportToDirectoryAsync(backupDirectory, cancellationToken).ConfigureAwait(false);
            Require(Directory.EnumerateFiles(backupDirectory, "*.xml").Any(), "Backup export should write files.", ref checks);
            checks++;

            ExpectThrows<ArgumentException>(
                () => backupService.ExportToDirectoryAsync(" ", cancellationToken).GetAwaiter().GetResult(),
                "Backup export should reject blank directories.",
                ref checks);
            ExpectThrows<DirectoryNotFoundException>(
                () => backupService.ImportFromDirectoryAsync(Path.Combine(rootDirectory, "missing"), cancellationToken).GetAwaiter().GetResult(),
                "Backup import should reject missing directories.",
                ref checks);

            var report = await healthChecks.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            Require(
                report.Entries.TryGetValue("diag-dataprotection", out var entry) &&
                entry.Status == HealthStatus.Healthy,
                "Health check should be healthy for a configured repository.",
                ref checks);

            var unhealthyResult = await new KyrolusDataProtectionHealthCheck(new ThrowingDataProtectionKeyRepository())
                .CheckHealthAsync(new HealthCheckContext(), cancellationToken)
                .ConfigureAwait(false);
            Require(unhealthyResult.Status == HealthStatus.Unhealthy, "Health check should be unhealthy when repository export fails.", ref checks);

            var validator = new KyrolusDataProtectionOptionsValidator();
            Require(validator.Validate(null, null!).Failed, "Validator should reject null options.", ref checks);
            Require(validator.Validate(null, new KyrolusDataProtectionOptions { ApplicationName = " " }).Failed, "Validator should reject blank app names.", ref checks);
            Require(
                validator.Validate(null, new KyrolusDataProtectionOptions
                {
                    ApplicationName = "certificate",
                    KeyProtection = new KyrolusKeyProtectionOptions { Kind = KyrolusKeyProtectionKind.Certificate }
                }).Failed,
                "Validator should require a certificate thumbprint.",
                ref checks);
            Require(
                OperatingSystem.IsWindows()
                    ? validator.Validate(null, new KyrolusDataProtectionOptions
                    {
                        ApplicationName = "dpapi",
                        KeyProtection = new KyrolusKeyProtectionOptions { Kind = KyrolusKeyProtectionKind.Dpapi }
                    }).Succeeded
                    : validator.Validate(null, new KyrolusDataProtectionOptions
                    {
                        ApplicationName = "dpapi",
                        KeyProtection = new KyrolusKeyProtectionOptions { Kind = KyrolusKeyProtectionKind.Dpapi }
                    }).Failed,
                "Validator should honor DPAPI platform rules.",
                ref checks);

            ExpectThrows<InvalidOperationException>(
                () =>
                {
                    var certificateServices = new ServiceCollection();
                    certificateServices.AddLogging();
                    _ = certificateServices.AddKyrolusDataProtection(options =>
                    {
                        options.ApplicationName = "certificate-protection";
                        options.KeyProtection = new KyrolusKeyProtectionOptions
                        {
                            Kind = KyrolusKeyProtectionKind.Certificate,
                            CertificateThumbprint = Guid.NewGuid().ToString("N"),
                            StoreLocation = StoreLocation.CurrentUser,
                            StoreName = StoreName.My
                        };
                    });
                },
                "Certificate protection should fail when the certificate is missing.",
                ref checks);

            ExpectThrows<InvalidOperationException>(
                () => _ = new KyrolusDataProtectionKeyRepository(Options.Create(new KeyManagementOptions())),
                "Repository should require an XML repository.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => _ = new KyrolusFileKeyEscrowSink(" "),
                "File escrow sink should reject blank directories.",
                ref checks);

            var passthroughSink = new KyrolusEncryptedFileKeyEscrowSink(
                Path.Combine(rootDirectory, "passthrough"),
                new KyrolusDataProtectionKeyEscrowEncryptionOptions
                {
                    Enabled = false,
                    EncryptionKey = keyBytes
                });
            var passthroughId = Guid.NewGuid();
            passthroughSink.Store(passthroughId, new XElement("key", new XAttribute("id", passthroughId), new XElement("value", "plain")));
            Require(
                File.ReadAllText(Path.Combine(rootDirectory, "passthrough", $"{passthroughId}.xml")).Contains("<key", StringComparison.Ordinal),
                "Disabled encrypted escrow should write plain xml.",
                ref checks);

            ExpectThrows<InvalidOperationException>(
                () =>
                {
                    var sink = new KyrolusEncryptedFileKeyEscrowSink(Path.Combine(rootDirectory, "missing-key"), new KyrolusDataProtectionKeyEscrowEncryptionOptions());
                    sink.Store(Guid.NewGuid(), new XElement("key"));
                },
                "Encrypted escrow should require a key.",
                ref checks);
            ExpectThrows<InvalidOperationException>(
                () =>
                {
                    var sink = new KyrolusEncryptedFileKeyEscrowSink(
                        Path.Combine(rootDirectory, "invalid-key"),
                        new KyrolusDataProtectionKeyEscrowEncryptionOptions
                        {
                            EncryptionKey = [1, 2, 3]
                        });
                    sink.Store(Guid.NewGuid(), new XElement("key"));
                },
                "Encrypted escrow should reject invalid key sizes.",
                ref checks);

            Require(Directory.EnumerateFiles(escrowDirectory, "*.xml").Any(), "Plain escrow directory should contain files.", ref checks);
            Require(Directory.EnumerateFiles(encryptedEscrowDirectory, "*.xml").Any(), "Encrypted escrow directory should contain files.", ref checks);
            Require(
                File.ReadAllText(Directory.EnumerateFiles(encryptedEscrowDirectory, "*.xml").First()).StartsWith("kyrolus-escrow:v1:", StringComparison.Ordinal),
                "Encrypted escrow output should use the expected prefix.",
                ref checks);

            var tokenSource = new KyrolusKeyRingRefreshTokenSource();
            var token = tokenSource.GetToken(CancellationToken.None);
            Require(!token.IsCancellationRequested, "Refresh token should start active.", ref checks);
            tokenSource.SignalExternal();
            Require(token.IsCancellationRequested, "Refresh token should cancel after signaling.", ref checks);

            var generatedInstanceId = new KyrolusDataProtectionInstanceId(new StaticOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions>(new()));
            Require(!string.IsNullOrWhiteSpace(generatedInstanceId.Value), "InstanceId should be generated when omitted.", ref checks);

            return new RepositoryRuntimeDiagnosticsResponse(
                Mode: "data-protection-runtime",
                DataProtectionChecks: checks);
        }
        finally
        {
            TryDeleteDirectory(rootDirectory);
        }
    }

    private static byte[] CreateDiagnosticsAesKey()
        => Enumerable.Range(1, 32).Select(static index => (byte)index).ToArray();

    private static void Require(bool condition, string message, ref int checks)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        checks++;
    }

    private static void ExpectThrows<TException>(Action action, string message, ref int checks)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            checks++;
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}

internal sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    where T : class
{
    public T CurrentValue => currentValue;

    public T Get(string? name) => currentValue;

    public IDisposable OnChange(Action<T, string?> listener) => new DisposableScope(static () => { });
}

internal sealed class DataProtectionRuntimeNotifier : IKyrolusKeyRingRefreshNotifier
{
    public List<KyrolusKeyRingRefreshSignal> PublishedSignals { get; } = [];

    public Task PublishAsync(KyrolusKeyRingRefreshSignal signal, CancellationToken cancellationToken = default)
    {
        PublishedSignals.Add(signal);
        return Task.CompletedTask;
    }

    public Task ListenAsync(
        Func<KyrolusKeyRingRefreshSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}

internal sealed class ThrowingDataProtectionKeyRepository : IKyrolusDataProtectionKeyRepository
{
    public Task<IReadOnlyList<KyrolusDataProtectionKeyDocument>> ExportAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Repository export failure.");

    public Task ImportAsync(IEnumerable<KyrolusDataProtectionKeyDocument> documents, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Repository import failure.");
}
