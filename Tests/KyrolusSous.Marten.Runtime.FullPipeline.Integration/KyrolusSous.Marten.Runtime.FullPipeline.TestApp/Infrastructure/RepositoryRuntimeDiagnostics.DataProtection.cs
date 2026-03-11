using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.Runtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                .AddKyrolusDataProtectionInstrumentation(options =>
                {
                    options.EnableMetrics = false;
                })
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
                .AddKyrolusDataProtectionKeyCleanup(options =>
                {
                    options.Interval = TimeSpan.FromMilliseconds(20);
                    options.DeleteExpiredKeys = true;
                    options.DeleteRevokedKeys = true;
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
            var instrumentationOptions = provider.GetRequiredService<IOptions<KyrolusDataProtectionInstrumentationOptions>>().Value;
            var cleanupOptions = provider.GetRequiredService<IOptions<KyrolusDataProtectionKeyCleanupOptions>>().Value;
            var keyManager = provider.GetRequiredService<IKyrolusDataProtectionKeyManager>();
            var repository = provider.GetRequiredService<IKyrolusDataProtectionKeyRepository>();
            var protectorFactory = provider.GetRequiredService<IKyrolusDataProtectorFactory>();
            var tenantProvider = provider.GetRequiredService<IKyrolusTenantDataProtectionProvider>();
            var backupService = provider.GetRequiredService<KyrolusDataProtectionKeyBackupService>();
            var instrumentation = provider.GetRequiredService<KyrolusDataProtectionInstrumentation>();
            var healthChecks = provider.GetRequiredService<HealthCheckService>();
            var decoratedKeyManager = provider.GetRequiredService<IKeyManager>();
            var instanceId = provider.GetRequiredService<KyrolusDataProtectionInstanceId>();
            var hostedServices = provider.GetServices<IHostedService>().ToArray();

            Require(runtimeOptions.ApplicationName == "repository-runtime-dataprotection", "ApplicationName should match.", ref checks);
            Require(runtimeOptions.DefaultKeyLifetime == TimeSpan.FromDays(14), "Default key lifetime should match.", ref checks);
            Require(runtimeOptions.AutoGenerateKeys == false, "AutoGenerateKeys should match.", ref checks);
            Require(tenantOptions.UseTenantPrefix, "Tenant prefix should be enabled.", ref checks);
            Require(tenantOptions.PurposePrefix == "tenant-scope", "Tenant prefix should match.", ref checks);
            Require(!instrumentationOptions.EnableMetrics, "Instrumentation options should honor runtime configuration.", ref checks);
            Require(
                cleanupOptions.Enabled &&
                cleanupOptions.Interval == TimeSpan.FromMilliseconds(20),
                "Cleanup options should honor runtime configuration.",
                ref checks);
            Require(decoratedKeyManager is KyrolusKeyManagerRefreshDecorator, "IKeyManager should be decorated.", ref checks);
            Require(protectorFactory is KyrolusInstrumentedDataProtectorFactory, "Protector factory should be instrumented.", ref checks);
            Require(instanceId.Value == "diag-instance", "Configured instance id should be preserved.", ref checks);
            Require(instrumentation is not null, "Instrumentation should resolve.", ref checks);

            var disabledInstrumentation = new KyrolusDataProtectionInstrumentation(
                Options.Create(new KyrolusDataProtectionInstrumentationOptions
                {
                    EnableActivities = false,
                    EnableMetrics = false
                }));
            Require(disabledInstrumentation.StartActivity("protect") is null, "Disabled instrumentation should skip activities.", ref checks);
            disabledInstrumentation.RecordSuccess("protect", 1);
            disabledInstrumentation.RecordFailure("unprotect", 1);
            checks++;
            disabledInstrumentation.Dispose();

            var activityStarts = 0;
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => string.Equals(source.Name, "kyrolus-dp-diag-activity", StringComparison.Ordinal),
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = _ => activityStarts++,
                ActivityStopped = _ => { }
            };
            ActivitySource.AddActivityListener(activityListener);

            var metricCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var metricDurations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            using var metricsInstrumentation = new KyrolusDataProtectionInstrumentation(
                Options.Create(new KyrolusDataProtectionInstrumentationOptions
                {
                    EnableActivities = true,
                    EnableMetrics = true,
                    ActivitySourceName = "kyrolus-dp-diag-activity",
                    MeterName = $"kyrolus-dp-diag-meter-{Guid.NewGuid():N}"
                }));
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, metricsInstrumentation.Meter.Name, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                metricCounts[instrument.Name] = metricCounts.GetValueOrDefault(instrument.Name) + measurement;
            });
            meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                var operation = instrument.Name;
                foreach (var tag in tags)
                {
                    if (string.Equals(tag.Key, "operation", StringComparison.Ordinal))
                    {
                        operation = tag.Value?.ToString() ?? instrument.Name;
                        break;
                    }
                }
                metricDurations[operation] = metricDurations.GetValueOrDefault(operation) + measurement;
            });
            meterListener.Start();
            using (var activity = metricsInstrumentation.StartActivity("protect"))
            {
                Require(activity is not null, "Enabled instrumentation should start activities.", ref checks);
            }
            metricsInstrumentation.RecordSuccess("protect", 5);
            metricsInstrumentation.RecordSuccess("unprotect", 7);
            metricsInstrumentation.RecordFailure("protect", 11);
            metricsInstrumentation.RecordFailure("unprotect", 13);
            Require(
                activityStarts >= 1 &&
                metricCounts.GetValueOrDefault("kyrolus.dataprotection.protect.success") == 1 &&
                metricCounts.GetValueOrDefault("kyrolus.dataprotection.protect.failure") == 1 &&
                metricCounts.GetValueOrDefault("kyrolus.dataprotection.unprotect.success") == 1 &&
                metricCounts.GetValueOrDefault("kyrolus.dataprotection.unprotect.failure") == 1 &&
                metricDurations.GetValueOrDefault("protect") > 0 &&
                metricDurations.GetValueOrDefault("unprotect") > 0,
                "Enabled instrumentation should emit activity and metric observations.",
                ref checks);

            Require(
                hostedServices.OfType<KyrolusDataProtectionKeyCleanupService>().Any() &&
                hostedServices.OfType<KyrolusDataProtectionKeyRingRefreshService>().Any() &&
                hostedServices.OfType<KyrolusKeyRingRefreshNotifierListener>().Any(),
                "Runtime data protection should register hosted services for cleanup and key-ring refresh.",
                ref checks);

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
            var documentsAfterImport = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
            Require(
                documentsAfterImport.Count >= exportedDocuments.Count,
                "Repository import should preserve or grow the persisted key set.",
                ref checks);
            checks++;

            await backupService.ExportToDirectoryAsync(backupDirectory, cancellationToken).ConfigureAwait(false);
            Require(Directory.EnumerateFiles(backupDirectory, "*.xml").Any(), "Backup export should write files.", ref checks);
            var importRepository = new RecordingDataProtectionKeyRepository();
            var importBackupService = new KyrolusDataProtectionKeyBackupService(importRepository);
            await importBackupService.ImportFromDirectoryAsync(backupDirectory, cancellationToken).ConfigureAwait(false);
            Require(
                importRepository.ImportedDocuments.Count > 0,
                "Backup import should replay exported key documents into the target repository.",
                ref checks);
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

            if (OperatingSystem.IsWindows())
            {
                var dpapiServices = new ServiceCollection();
                dpapiServices.AddLogging();
                _ = dpapiServices.AddKyrolusDataProtection(options =>
                {
                    options.ApplicationName = "dpapi-protection";
                    options.KeyProtection = new KyrolusKeyProtectionOptions
                    {
                        Kind = KyrolusKeyProtectionKind.Dpapi,
                        UseMachineStore = false
                    };
                });
                checks++;
            }
            else
            {
                ExpectThrows<PlatformNotSupportedException>(
                    () =>
                    {
                        var dpapiServices = new ServiceCollection();
                        dpapiServices.AddLogging();
                        _ = dpapiServices.AddKyrolusDataProtection(options =>
                        {
                            options.ApplicationName = "dpapi-protection";
                            options.KeyProtection = new KyrolusKeyProtectionOptions
                            {
                                Kind = KyrolusKeyProtectionKind.Dpapi
                            };
                        });
                    },
                    "DPAPI protection should reject unsupported platforms.",
                    ref checks);
            }

            ExpectThrows<InvalidOperationException>(
                () =>
                {
                    var missingThumbprintServices = new ServiceCollection();
                    missingThumbprintServices.AddLogging();
                    _ = missingThumbprintServices.AddKyrolusDataProtection(options =>
                    {
                        options.ApplicationName = "certificate-thumbprint";
                        options.KeyProtection = new KyrolusKeyProtectionOptions
                        {
                            Kind = KyrolusKeyProtectionKind.Certificate
                        };
                    });
                },
                "Certificate protection should require a thumbprint at registration time.",
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

            ExpectThrows<ArgumentNullException>(
                () => _ = new KyrolusDataProtectorFactory(null!),
                "Data protector factory should reject null providers.",
                ref checks);

            var directFactory = new KyrolusDataProtectorFactory(provider.GetRequiredService<IDataProtectionProvider>());
            var directProtector = directFactory.CreateProtector("direct-factory");
            var directProtected = directProtector.Protect(payload);
            Require(
                Encoding.UTF8.GetString(directProtector.Unprotect(directProtected)) == "pipeline-payload",
                "Direct data protector factory should round-trip payloads.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => directFactory.CreateProtector(" "),
                "Direct data protector factory should reject blank purposes.",
                ref checks);

            var cleanupLogger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<KyrolusDataProtectionKeyCleanupService>();
            var executeAsyncMethod = typeof(KyrolusDataProtectionKeyCleanupService).GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cleanup ExecuteAsync method was not found.");
            var cleanupOnceMethod = typeof(KyrolusDataProtectionKeyCleanupService).GetMethod(
                "CleanupOnceAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CleanupOnceAsync method was not found.");

            var disabledCleanupService = new KyrolusDataProtectionKeyCleanupService(
                decoratedKeyManager,
                new StaticOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions>(new KyrolusDataProtectionKeyCleanupOptions
                {
                    Enabled = false
                }),
                cleanupLogger);
            using (var cleanupCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25)))
            {
                await ExpectBackgroundServiceCancellationAsync(
                    () => (Task)executeAsyncMethod.Invoke(disabledCleanupService, [cleanupCts.Token])!,
                    "Disabled cleanup service should honor cancellation.").ConfigureAwait(false);
            }
            checks++;

            var unsupportedCleanupService = new KyrolusDataProtectionKeyCleanupService(
                new RuntimeRefreshKeyManager(Array.Empty<IKey>(), CancellationToken.None),
                new StaticOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions>(new KyrolusDataProtectionKeyCleanupOptions
                {
                    Enabled = true,
                    DeleteExpiredKeys = true,
                    DeleteRevokedKeys = true,
                    Interval = TimeSpan.Zero
                }),
                cleanupLogger);
            await ((Task)cleanupOnceMethod.Invoke(unsupportedCleanupService, [new KyrolusDataProtectionKeyCleanupOptions
            {
                Enabled = true,
                DeleteExpiredKeys = true,
                DeleteRevokedKeys = true,
                Interval = TimeSpan.Zero
            }, cancellationToken])!).ConfigureAwait(false);
            checks++;

            var innerKeyManagerField = decoratedKeyManager.GetType().GetField("inner", BindingFlags.Instance | BindingFlags.NonPublic);
            if (innerKeyManagerField?.GetValue(decoratedKeyManager) is IKeyManager innerKeyManager)
            {
                var canDeleteKeys = innerKeyManager is IDeletableKeyManager deletable && deletable.CanDeleteKeys;
                var expiredKey = await keyManager.CreateKeyAsync(DateTimeOffset.UtcNow.AddDays(-30), TimeSpan.FromDays(1), cancellationToken).ConfigureAwait(false);
                var revokedKey = await keyManager.CreateKeyAsync(DateTimeOffset.UtcNow.AddDays(-20), TimeSpan.FromDays(10), cancellationToken).ConfigureAwait(false);
                await keyManager.RevokeKeyAsync(revokedKey.KeyId, "cleanup", cancellationToken).ConfigureAwait(false);
                var cleanupOptionsValue = new KyrolusDataProtectionKeyCleanupOptions
                {
                    Enabled = true,
                    DeleteRevokedKeys = true,
                    RevokedKeyGracePeriod = TimeSpan.Zero,
                    DeleteExpiredKeys = true,
                    ExpiredKeyGracePeriod = TimeSpan.Zero,
                    Interval = TimeSpan.Zero
                };
                var cleanupService = new KyrolusDataProtectionKeyCleanupService(
                    innerKeyManager,
                    new StaticOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions>(cleanupOptionsValue),
                    cleanupLogger);
                var preCleanupDocuments = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
                await ((Task)cleanupOnceMethod.Invoke(cleanupService, [cleanupOptionsValue, cancellationToken])!).ConfigureAwait(false);

                var postCleanupDocuments = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
                Require(
                    canDeleteKeys
                        ? postCleanupDocuments.Count <= preCleanupDocuments.Count
                        : postCleanupDocuments.Count == preCleanupDocuments.Count,
                    "Cleanup service should not increase the persisted key set.",
                    ref checks);
            }

            var fakeCleanupOptions = new KyrolusDataProtectionKeyCleanupOptions
            {
                Enabled = true,
                DeleteExpiredKeys = true,
                ExpiredKeyGracePeriod = TimeSpan.Zero,
                DeleteRevokedKeys = true,
                RevokedKeyGracePeriod = TimeSpan.Zero,
                Interval = TimeSpan.Zero
            };
            var fakeDeleteManager = new RuntimeDeletableKeyManager(decoratedKeyManager.GetAllKeys(), canDeleteKeys: true);
            var fakeCleanupService = new KyrolusDataProtectionKeyCleanupService(
                fakeDeleteManager,
                new StaticOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions>(fakeCleanupOptions),
                cleanupLogger);
            await ((Task)cleanupOnceMethod.Invoke(fakeCleanupService, [fakeCleanupOptions, cancellationToken])!).ConfigureAwait(false);
            Require(
                fakeDeleteManager.DeleteCalls == 1 &&
                fakeDeleteManager.LastMatchedKeys.Count > 0,
                "Cleanup service should evaluate and delete expired or revoked keys when the key manager supports deletion.",
                ref checks);

            var noDeleteManager = new RuntimeDeletableKeyManager(Array.Empty<IKey>(), canDeleteKeys: true);
            var noDeleteCleanupService = new KyrolusDataProtectionKeyCleanupService(
                noDeleteManager,
                new StaticOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions>(fakeCleanupOptions),
                cleanupLogger);
            await ((Task)cleanupOnceMethod.Invoke(noDeleteCleanupService, [fakeCleanupOptions, cancellationToken])!).ConfigureAwait(false);
            Require(
                noDeleteManager.DeleteCalls == 1 &&
                noDeleteManager.LastMatchedKeys.Count == 0,
                "Cleanup service should tolerate deletable key managers when no keys match the cleanup predicate.",
                ref checks);

            var executingCleanupManager = new RuntimeDeletableKeyManager(decoratedKeyManager.GetAllKeys(), canDeleteKeys: true);
            var executingCleanupService = new KyrolusDataProtectionKeyCleanupService(
                executingCleanupManager,
                new StaticOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions>(fakeCleanupOptions),
                cleanupLogger);
            using (var enabledCleanupCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25)))
            {
                await ExpectBackgroundServiceCancellationAsync(
                    () => (Task)executeAsyncMethod.Invoke(executingCleanupService, [enabledCleanupCts.Token])!,
                    "Enabled cleanup service should honor cancellation after executing at least one cleanup cycle.").ConfigureAwait(false);
            }
            Require(
                executingCleanupManager.DeleteCalls >= 1,
                "Enabled cleanup service should execute cleanup work before cancellation.",
                ref checks);

            var refreshLogger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<KyrolusDataProtectionKeyRingRefreshService>();
            var refreshStopCts = new CancellationTokenSource();
            var refreshHook = new RuntimeDataProtectionRefreshHook(() => refreshStopCts.Cancel());
            var refreshService = new KyrolusDataProtectionKeyRingRefreshService(
                new RuntimeRefreshKeyManager(decoratedKeyManager.GetAllKeys(), new CancellationToken(canceled: true)),
                [refreshHook, new ThrowingDataProtectionRefreshHook()],
                new StaticOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions>(new KyrolusDataProtectionKeyRingRefreshOptions
                {
                    Enabled = true,
                    IncludeKeyDetails = true,
                    MinimumInterval = TimeSpan.Zero
                }),
                refreshLogger);
            var refreshExecuteAsyncMethod = typeof(KyrolusDataProtectionKeyRingRefreshService).GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Key ring refresh ExecuteAsync method was not found.");
            await ((Task)refreshExecuteAsyncMethod.Invoke(refreshService, [refreshStopCts.Token])!).ConfigureAwait(false);
            Require(
                refreshHook.InvocationCount == 1 &&
                refreshHook.LastContext?.Keys is { Count: > 0 },
                "Key ring refresh service should notify hooks with key details.",
                ref checks);

            var listenerLogger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<KyrolusKeyRingRefreshNotifierListener>();
            var listenerNotifier = new RuntimeListenerNotifier(
                new KyrolusKeyRingRefreshSignal(
                    runtimeOptions.ApplicationName,
                    "external-instance",
                    DateTimeOffset.UtcNow,
                    KyrolusKeyRingRefreshReason.KeyRotated));
            var listenerTokenSource = new KyrolusKeyRingRefreshTokenSource();
            var listener = new KyrolusKeyRingRefreshNotifierListener(
                listenerNotifier,
                listenerTokenSource,
                new StaticOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions>(new KyrolusDataProtectionKeyRingRefreshOptions
                {
                    EnableCrossInstanceNotifications = true,
                    RefreshOnExternalSignal = true
                }),
                Options.Create(runtimeOptions),
                instanceId,
                listenerLogger);
            var listenerToken = listenerTokenSource.GetToken(CancellationToken.None);
            var listenerExecuteAsyncMethod = typeof(KyrolusKeyRingRefreshNotifierListener).GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Key ring listener ExecuteAsync method was not found.");
            await ((Task)listenerExecuteAsyncMethod.Invoke(listener, [cancellationToken])!).ConfigureAwait(false);
            Require(
                listenerNotifier.ListenCalls == 1 &&
                listenerToken.IsCancellationRequested,
                "Key ring notifier listener should subscribe and signal token refresh for external instances.",
                ref checks);

            var handleSignalMethod = typeof(KyrolusKeyRingRefreshNotifierListener).GetMethod(
                "HandleSignalAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Key ring listener HandleSignalAsync method was not found.");
            var ignoredTokenSource = new KyrolusKeyRingRefreshTokenSource();
            var ignoredListener = new KyrolusKeyRingRefreshNotifierListener(
                new RuntimeListenerNotifier(
                    new KyrolusKeyRingRefreshSignal(
                        "ignored",
                        instanceId.Value,
                        DateTimeOffset.UtcNow,
                        KyrolusKeyRingRefreshReason.KeyRotated)),
                ignoredTokenSource,
                new StaticOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions>(new KyrolusDataProtectionKeyRingRefreshOptions
                {
                    EnableCrossInstanceNotifications = false,
                    RefreshOnExternalSignal = true
                }),
                Options.Create(runtimeOptions),
                instanceId,
                listenerLogger);
            var ignoredToken = ignoredTokenSource.GetToken(CancellationToken.None);
            await ((Task)handleSignalMethod.Invoke(ignoredListener, [new KyrolusKeyRingRefreshSignal(
                runtimeOptions.ApplicationName,
                instanceId.Value,
                DateTimeOffset.UtcNow,
                KyrolusKeyRingRefreshReason.KeyRotated), cancellationToken])!).ConfigureAwait(false);
            Require(
                !ignoredToken.IsCancellationRequested,
                "Key ring notifier listener should ignore disabled or same-instance signals.",
                ref checks);

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

    private static async Task ExpectBackgroundServiceCancellationAsync(Func<Task> action, string message)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException(message);
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

internal sealed class RecordingDataProtectionKeyRepository : IKyrolusDataProtectionKeyRepository
{
    public List<KyrolusDataProtectionKeyDocument> ImportedDocuments { get; } = [];

    public Task<IReadOnlyList<KyrolusDataProtectionKeyDocument>> ExportAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusDataProtectionKeyDocument>>(ImportedDocuments);

    public Task ImportAsync(IEnumerable<KyrolusDataProtectionKeyDocument> documents, CancellationToken cancellationToken = default)
    {
        ImportedDocuments.AddRange(documents);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingDataProtectionKeyRepository : IKyrolusDataProtectionKeyRepository
{
    public Task<IReadOnlyList<KyrolusDataProtectionKeyDocument>> ExportAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Repository export failure.");

    public Task ImportAsync(IEnumerable<KyrolusDataProtectionKeyDocument> documents, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Repository import failure.");
}

internal sealed class RuntimeRefreshKeyManager(
    IReadOnlyCollection<IKey> keys,
    CancellationToken cacheExpirationToken) : IKeyManager
{
    public IReadOnlyCollection<IKey> GetAllKeys() => keys;

    public IKey CreateNewKey(DateTimeOffset activationDate, DateTimeOffset expirationDate)
        => throw new NotSupportedException();

    public void RevokeKey(Guid keyId, string? reason = null)
        => throw new NotSupportedException();

    public void RevokeAllKeys(DateTimeOffset revocationDate, string? reason = null)
        => throw new NotSupportedException();

    public CancellationToken GetCacheExpirationToken() => cacheExpirationToken;
}

internal sealed class RuntimeDeletableKeyManager(
    IReadOnlyCollection<IKey> keys,
    bool canDeleteKeys) : IDeletableKeyManager
{
    private readonly IReadOnlyCollection<IKey> keys = keys;

    public int DeleteCalls { get; private set; }

    public IReadOnlyList<IKey> LastMatchedKeys { get; private set; } = [];

    public bool CanDeleteKeys => canDeleteKeys;

    public IReadOnlyCollection<IKey> GetAllKeys() => keys;

    public IKey CreateNewKey(DateTimeOffset activationDate, DateTimeOffset expirationDate)
        => throw new NotSupportedException();

    public void RevokeKey(Guid keyId, string? reason = null)
        => throw new NotSupportedException();

    public void RevokeAllKeys(DateTimeOffset revocationDate, string? reason = null)
        => throw new NotSupportedException();

    public CancellationToken GetCacheExpirationToken() => CancellationToken.None;

    public bool DeleteKeys(Func<IKey, bool> shouldDelete)
    {
        ArgumentNullException.ThrowIfNull(shouldDelete);
        DeleteCalls++;
        LastMatchedKeys = [.. keys.Where(shouldDelete)];
        return LastMatchedKeys.Count > 0;
    }
}

internal sealed class RuntimeDataProtectionRefreshHook(Action onInvoked) : IKyrolusKeyRingRefreshHook
{
    public int InvocationCount { get; private set; }
    public KyrolusKeyRingRefreshContext? LastContext { get; private set; }

    public Task OnKeyRingRefreshedAsync(
        KyrolusKeyRingRefreshContext context,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        LastContext = context;
        onInvoked();
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingDataProtectionRefreshHook : IKyrolusKeyRingRefreshHook
{
    public Task OnKeyRingRefreshedAsync(
        KyrolusKeyRingRefreshContext context,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Refresh hook failure.");
}

internal sealed class RuntimeListenerNotifier(KyrolusKeyRingRefreshSignal signal) : IKyrolusKeyRingRefreshNotifier
{
    public int ListenCalls { get; private set; }

    public Task PublishAsync(KyrolusKeyRingRefreshSignal signal, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ListenAsync(
        Func<KyrolusKeyRingRefreshSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ListenCalls++;
        return handler(signal, cancellationToken);
    }
}
