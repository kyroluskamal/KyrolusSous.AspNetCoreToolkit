using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Caching.Redis;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.Redis;
using KyrolusSous.DataProtection.Runtime;
using KyrolusSous.ExceptionHandling.Abstractions;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Redis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunDataProtectionRedisRuntimeAsync(
        string redisConnectionString,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var unique = Guid.NewGuid().ToString("N");
        var applicationName = $"diag-dataprotection-redis-{unique}";
        var key = $"kyrolus:diag:dataprotection:{unique}";
        var stringKey = $"{key}:string";
        var channel = $"kyrolus:diag:dataprotection:{unique}:channel";

        using var publisherConnection = await ConnectRedisAsync(redisConnectionString).ConfigureAwait(false);
        using var listenerConnection = await ConnectRedisAsync(redisConnectionString).ConfigureAwait(false);

        try
        {
            var publisherServices = new ServiceCollection();
            publisherServices.AddLogging();
            var publisherBuilder = publisherServices.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = applicationName;
                options.DefaultKeyLifetime = TimeSpan.FromDays(7);
            });
            publisherBuilder
                .AddKyrolusDataProtectionRedis(publisherConnection, key)
                .AddKyrolusDataProtectionRedisKeyRingRefreshNotifications(options =>
                {
                    options.Channel = channel;
                    options.IncludeApplicationNameInChannel = true;
                });

            using var publisherProvider = publisherServices.BuildServiceProvider();
            var keyManager = publisherProvider.GetRequiredService<IKyrolusDataProtectionKeyManager>();
            var tenantProvider = publisherProvider.GetRequiredService<IKyrolusTenantDataProtectionProvider>();
            var notifier = publisherProvider.GetRequiredService<IKyrolusKeyRingRefreshNotifier>();
            var refreshOptions = publisherProvider.GetRequiredService<IOptions<KyrolusDataProtectionKeyRingRefreshOptions>>().Value;

            Require(
                refreshOptions.EnableCrossInstanceNotifications &&
                refreshOptions.RefreshOnExternalSignal &&
                refreshOptions.PublishLocalChanges,
                "Redis data protection runtime should enable key-ring refresh integration flags.",
                ref checks);

            var createdKey = await keyManager.CreateKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                TimeSpan.FromDays(7),
                cancellationToken).ConfigureAwait(false);
            Require(createdKey.KeyId != Guid.Empty, "Redis data protection runtime should create keys through the Redis-backed repository.", ref checks);
            Require(
                await publisherConnection.GetDatabase().KeyExistsAsync(key).ConfigureAwait(false),
                "Redis data protection runtime should persist keys in Redis.",
                ref checks);

            var protector = tenantProvider.CreateProtector(tenantId, "redis-runtime");
            var protectedPayload = protector.Protect(Encoding.UTF8.GetBytes("redis-payload"));
            var unprotectedPayload = protector.Unprotect(protectedPayload);
            Require(
                Encoding.UTF8.GetString(unprotectedPayload) == "redis-payload",
                "Redis data protection runtime should round-trip tenant-scoped payloads.",
                ref checks);

            var stringServices = new ServiceCollection();
            stringServices.AddLogging();
            var stringBuilder = stringServices.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = $"{applicationName}-string";
            });
            stringBuilder.AddKyrolusDataProtectionRedis(redisConnectionString, stringKey);
            using var stringProvider = stringServices.BuildServiceProvider();
            var stringKeyManager = stringProvider.GetRequiredService<IKyrolusDataProtectionKeyManager>();
            var rotatedKey = await stringKeyManager.RotateKeyAsync(TimeSpan.FromDays(2), cancellationToken).ConfigureAwait(false);
            Require(rotatedKey.KeyId != Guid.Empty, "Redis data protection runtime should support the connection-string Redis registration overload.", ref checks);
            Require(
                await publisherConnection.GetDatabase().KeyExistsAsync(stringKey).ConfigureAwait(false),
                "Redis data protection runtime should persist keys for the connection-string registration path.",
                ref checks);
            var listenerServices = new ServiceCollection();
            listenerServices.AddLogging();
            var listenerBuilder = listenerServices.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = applicationName;
            });
            listenerBuilder
                .AddKyrolusDataProtectionRedis(listenerConnection, key)
                .AddKyrolusDataProtectionRedisKeyRingRefreshNotifications(options =>
                {
                    options.Channel = channel;
                    options.IncludeApplicationNameInChannel = true;
                });

            using var listenerProvider = listenerServices.BuildServiceProvider();
            var listenerNotifier = listenerProvider.GetRequiredService<IKyrolusKeyRingRefreshNotifier>();

            var validSignal = await ListenForKeyRingSignalAsync(
                listenerNotifier,
                async () =>
                {
                    await notifier.PublishAsync(
                        new KyrolusKeyRingRefreshSignal(
                            applicationName,
                            "publisher-instance",
                            DateTimeOffset.UtcNow,
                            KyrolusKeyRingRefreshReason.KeyRotated),
                        cancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            Require(
                validSignal.InstanceId == "publisher-instance" &&
                validSignal.Reason == KyrolusKeyRingRefreshReason.KeyRotated,
                "Redis data protection runtime should publish and receive key-ring refresh notifications.",
                ref checks);

            var rawChannel = RedisChannel.Literal(BuildKeyRingChannel(channel, applicationName));
            var unknownSignal = await ListenForKeyRingSignalAsync(
                listenerNotifier,
                async () =>
                {
                    await publisherConnection.GetSubscriber().PublishAsync(rawChannel, "bad-payload").ConfigureAwait(false);
                    var payload = string.Join(
                        '|',
                        Uri.EscapeDataString(applicationName),
                        Uri.EscapeDataString("raw-instance"),
                        DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture),
                        "999");
                    await publisherConnection.GetSubscriber().PublishAsync(rawChannel, payload).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            Require(
                unknownSignal.InstanceId == "raw-instance" &&
                unknownSignal.Reason == KyrolusKeyRingRefreshReason.Unknown,
                "Redis data protection runtime should ignore malformed messages and map unknown reasons.",
                ref checks);

            ExpectThrows<ArgumentNullException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(null!, publisherConnection),
                "Redis data protection runtime should reject null builders for the connection overload.",
                ref checks);
            ExpectThrows<ArgumentNullException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(publisherBuilder, (IConnectionMultiplexer)null!, key),
                "Redis data protection runtime should reject null connections.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(publisherBuilder, publisherConnection, " "),
                "Redis data protection runtime should reject blank Redis key names.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(publisherBuilder, " ", key),
                "Redis data protection runtime should reject blank connection strings.",
                ref checks);
            await ExpectThrowsAsync<ArgumentNullException>(
                () => notifier.ListenAsync(null!, cancellationToken),
                "Redis data protection runtime should reject null listeners.").ConfigureAwait(false);
            checks++;

            return new RepositoryRuntimeDiagnosticsResponse(
                Mode: "data-protection-redis-runtime",
                DataProtectionRedisChecks: checks);
        }
        finally
        {
            await publisherConnection.GetDatabase().KeyDeleteAsync((RedisKey)key).ConfigureAwait(false);
            await publisherConnection.GetDatabase().KeyDeleteAsync((RedisKey)stringKey).ConfigureAwait(false);
        }
    }

}
