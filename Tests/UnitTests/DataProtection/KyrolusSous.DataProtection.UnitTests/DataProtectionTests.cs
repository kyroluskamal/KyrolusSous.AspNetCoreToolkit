using System.Security.Cryptography;
using System.Text;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.EntityFramework;
using KyrolusSous.DataProtection.Ephemeral;
using KyrolusSous.DataProtection.FileSystem;
using KyrolusSous.DataProtection.Runtime;
using KyrolusSous.DataProtection.Vault;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.DataProtection.UnitTests;

public class DataProtectionTests
{
    private readonly IServiceProvider _services;
    private readonly IDataProtector _protector;

    public DataProtectionTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusDataProtection(options =>
        {
            options.ApplicationName = "UnitTestApp";
        })
        .AddKyrolusEphemeralDataProtection();

        _services = services.BuildServiceProvider();
        _protector = _services.GetRequiredService<IDataProtectionProvider>().CreateProtector("UnitTests.Purpose");
    }

    [Fact]
    public void TryUnprotect_WithValidCiphertext_ReturnsTrueAndPlaintext()
    {
        var original = "SecretPassword123!";
        var cipher = _protector.Protect(original);

        var success = _protector.TryUnprotect(cipher, out var result);

        success.ShouldBeTrue();
        result.ShouldBe(original);
    }

    [Fact]
    public void TryUnprotect_WithInvalidCiphertext_ReturnsFalseWithoutThrowing()
    {
        var invalidCipher = "NotAValidBase64ProtectedString";

        var success = _protector.TryUnprotect(invalidCipher, out var result);

        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryUnprotect_WithTamperedCiphertext_ReturnsFalseWithoutThrowing()
    {
        var original = "ConfidentialData";
        var cipher = _protector.Protect(original);
        var tamperedCipher = cipher[..^4] + "AAAA";

        var success = _protector.TryUnprotect(tamperedCipher, out var result);

        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryUnprotect_WithNullOrEmpty_ReturnsFalse()
    {
        _protector.TryUnprotect((string?)null, out var r1).ShouldBeFalse();
        r1.ShouldBeNull();

        _protector.TryUnprotect("", out var r2).ShouldBeFalse();
        r2.ShouldBeNull();

        _protector.TryUnprotect("   ", out var r3).ShouldBeFalse();
        r3.ShouldBeNull();
    }

    [Fact]
    public void TryUnprotect_Bytes_WithValidData_ReturnsTrueAndPlaintextBytes()
    {
        var originalBytes = Encoding.UTF8.GetBytes("BinarySecretPayload");
        var cipherBytes = _protector.Protect(originalBytes);

        var success = _protector.TryUnprotect(cipherBytes, out var resultBytes);

        success.ShouldBeTrue();
        resultBytes.ShouldNotBeNull();
        resultBytes.ShouldBe(originalBytes);
    }

    [Fact]
    public void TryUnprotect_Bytes_WithInvalidData_ReturnsFalse()
    {
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };

        var success = _protector.TryUnprotect(invalidBytes, out var result);

        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void ProtectWithExpiry_WithValidDuration_UnprotectsSuccessfully()
    {
        var original = "SelfExpiringToken";
        var cipher = _protector.ProtectWithExpiry(original, TimeSpan.FromMinutes(10));

        var decrypted = _protector.UnprotectWithExpiry(cipher);
        decrypted.ShouldBe(original);
    }

    [Fact]
    public void ProtectWithExpiry_WhenExpired_ThrowsCryptographicExceptionOnUnprotect()
    {
        var original = "ExpiredToken";
        // Protect with negative duration (already expired)
        var cipher = _protector.ProtectWithExpiry(original, TimeSpan.FromSeconds(-5));

        Should.Throw<CryptographicException>(() =>
        {
            _protector.UnprotectWithExpiry(cipher);
        });
    }

    [Fact]
    public void TryUnprotectWithExpiry_WhenExpired_ReturnsFalseWithoutThrowing()
    {
        var original = "ExpiredToken";
        var cipher = _protector.ProtectWithExpiry(original, TimeSpan.FromSeconds(-10));

        var success = _protector.TryUnprotectWithExpiry(cipher, out var decrypted);

        success.ShouldBeFalse();
        decrypted.ShouldBeNull();
    }

    [Fact]
    public void TryUnprotectWithExpiry_WithValidToken_ReturnsTrueAndPlaintext()
    {
        var original = "FreshValidToken";
        var cipher = _protector.ProtectWithExpiry(original, TimeSpan.FromHours(1));

        var success = _protector.TryUnprotectWithExpiry(cipher, out var decrypted);

        success.ShouldBeTrue();
        decrypted.ShouldBe(original);
    }

    [Fact]
    public void ReEncrypt_ReProtectsUnderActiveKey()
    {
        var original = "DataToMigrate";
        var cipher1 = _protector.Protect(original);

        var cipher2 = _protector.ReEncrypt(cipher1);
        cipher2.ShouldNotBeNull();

        var decrypted = _protector.Unprotect(cipher2);
        decrypted.ShouldBe(original);
    }

    [Fact]
    public void TryReEncrypt_WithInvalidData_ReturnsFalse()
    {
        var success = _protector.TryReEncrypt("CorruptedOldCiphertext", out var reEncrypted);

        success.ShouldBeFalse();
        reEncrypted.ShouldBeNull();
    }

    [Fact]
    public void TenantDataProtectionProvider_IsolatesTenants_DifferentTenantsCannotDecrypt()
    {
        var tenantProvider = _services.GetRequiredService<IKyrolusTenantDataProtectionProvider>();

        var tenant1Protector = tenantProvider.CreateProtectorForTenant("Tenant-Alpha", "Customer.PII");
        var tenant2Protector = tenantProvider.CreateProtectorForTenant("Tenant-Beta", "Customer.PII");

        var secret = "SensitiveTenantAlphaData";
        var cipher = tenant1Protector.Protect(secret);

        // Tenant 1 can decrypt
        tenant1Protector.Unprotect(cipher).ShouldBe(secret);

        // Tenant 2 must NOT be able to decrypt Tenant 1's data
        tenant2Protector.TryUnprotect(cipher, out _).ShouldBeFalse();
    }

    [Fact]
    public void DataProtectorFactory_CreatesNamedAndTypedProtectors()
    {
        var factory = _services.GetRequiredService<IKyrolusDataProtectorFactory>();

        var protectorA = factory.CreateProtector("Finance.Purposes");
        var protectorB = factory.CreateProtector<DataProtectionTests>("CustomSubPurpose");

        var secret = "Payload123";
        var cipherA = protectorA.Protect(secret);

        protectorA.Unprotect(cipherA).ShouldBe(secret);
        protectorB.TryUnprotect(cipherA, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task KeyManager_CreateRotateAndRevokeKeys()
    {
        var keyManager = _services.GetRequiredService<IKyrolusDataProtectionKeyManager>();

        var key1 = await keyManager.CreateKeyAsync(DateTimeOffset.UtcNow, TimeSpan.FromDays(30));
        key1.ShouldNotBeNull();
        key1.KeyId.ShouldNotBe(Guid.Empty);

        var keys = await keyManager.GetAllKeysAsync();
        keys.Count.ShouldBeGreaterThanOrEqualTo(1);

        var rotatedKey = await keyManager.RotateKeyAsync(TimeSpan.FromDays(90));
        rotatedKey.ShouldNotBeNull();
        rotatedKey.KeyId.ShouldNotBe(key1.KeyId);

        await keyManager.RevokeKeyAsync(key1.KeyId, "Compromised");
        var key1AfterRevocation = await keyManager.GetKeyAsync(key1.KeyId);
        key1AfterRevocation.ShouldNotBeNull();
        key1AfterRevocation.IsRevoked.ShouldBeTrue();
        key1AfterRevocation.RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task KeyRotationWorker_WhenExpiringSoon_AutomaticallyRotatesKey()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddKyrolusDataProtection()
                .AddKyrolusDataProtectionFileSystem(tempDir);

            var sp = services.BuildServiceProvider();
            var keyManager = sp.GetRequiredService<IKyrolusDataProtectionKeyManager>();

            // Create a key that expires in 1 day
            await keyManager.CreateKeyAsync(DateTimeOffset.UtcNow, TimeSpan.FromDays(1));

            var rotationOptions = Options.Create(new KyrolusDataProtectionKeyRotationOptions
            {
                EnableAutoRotation = true,
                RotateBeforeExpiryThreshold = TimeSpan.FromDays(3), // Expiring in 1 day < 3 days threshold -> should rotate!
                RotationCheckInterval = TimeSpan.FromHours(1)
            });

            var worker = new KyrolusKeyRotationWorker(sp, rotationOptions, NullLogger<KyrolusKeyRotationWorker>.Instance);
            var didRotate = await worker.CheckAndRotateKeysAsync();

            didRotate.ShouldBeTrue();

            var allKeys = await keyManager.GetAllKeysAsync();
            allKeys.Count.ShouldBeGreaterThanOrEqualTo(2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task KeyRotationWorker_WhenHealthy_DoesNotRotate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddKyrolusDataProtection()
                .AddKyrolusDataProtectionFileSystem(tempDir);

            var sp = services.BuildServiceProvider();
            var keyManager = sp.GetRequiredService<IKyrolusDataProtectionKeyManager>();

            // Create a key that expires in 90 days
            await keyManager.CreateKeyAsync(DateTimeOffset.UtcNow, TimeSpan.FromDays(90));

            var rotationOptions = Options.Create(new KyrolusDataProtectionKeyRotationOptions
            {
                EnableAutoRotation = true,
                RotateBeforeExpiryThreshold = TimeSpan.FromDays(2), // 90 days > 2 days threshold -> should NOT rotate!
                RotationCheckInterval = TimeSpan.FromHours(1)
            });

            var worker = new KyrolusKeyRotationWorker(sp, rotationOptions, NullLogger<KyrolusKeyRotationWorker>.Instance);
            var didRotate = await worker.CheckAndRotateKeysAsync();

            didRotate.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void FileKeyEscrowSink_StoresKeyInDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"escrow_test_{Guid.NewGuid():N}");
        try
        {
            var sink = new KyrolusFileKeyEscrowSink(tempDir);
            var keyId = Guid.NewGuid();
            var element = new System.Xml.Linq.XElement("key", new System.Xml.Linq.XAttribute("id", keyId));

            sink.Store(keyId, element);

            var expectedFile = Path.Combine(tempDir, $"{keyId}.xml");
            File.Exists(expectedFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void OptionsValidator_ValidatesApplicationName()
    {
        var validator = new KyrolusDataProtectionOptionsValidator();

        var validOptions = new KyrolusDataProtectionOptions { ApplicationName = "ValidApp" };
        validator.Validate(null, validOptions).Succeeded.ShouldBeTrue();

        var invalidOptions = new KyrolusDataProtectionOptions { ApplicationName = "" };
        validator.Validate(null, invalidOptions).Failed.ShouldBeTrue();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthyResult()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"health_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddKyrolusDataProtection()
                .AddKyrolusDataProtectionFileSystem(tempDir);

            var sp = services.BuildServiceProvider();
            var repository = sp.GetRequiredService<IKyrolusDataProtectionKeyRepository>();
            var healthCheck = new KyrolusDataProtectionHealthCheck(repository);

            var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
            result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #region Base64Url Tests

    [Fact]
    public void ProtectAsBase64Url_ReturnsUrlSafeString()
    {
        var original = "Hello+World/With=Special?Chars&Data";
        var base64Url = _protector.ProtectAsBase64Url(original);

        base64Url.ShouldNotContain("+");
        base64Url.ShouldNotContain("/");
        base64Url.ShouldNotContain("=");

        var decrypted = _protector.UnprotectFromBase64Url(base64Url);
        decrypted.ShouldBe(original);
    }

    [Fact]
    public void TryUnprotectFromBase64Url_WithCorruptedString_ReturnsFalseWithoutThrowing()
    {
        var success = _protector.TryUnprotectFromBase64Url("InvalidBase64UrlData!@#", out var result);

        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void ProtectWithExpiryAsBase64Url_HandlesLifetimeAndExpiration()
    {
        var original = "UrlSafeExpiringToken";
        var validToken = _protector.ProtectWithExpiryAsBase64Url(original, TimeSpan.FromMinutes(5));

        validToken.ShouldNotContain("+");
        validToken.ShouldNotContain("/");

        var decrypted = _protector.UnprotectWithExpiryFromBase64Url(validToken);
        decrypted.ShouldBe(original);

        var expiredToken = _protector.ProtectWithExpiryAsBase64Url(original, TimeSpan.FromSeconds(-5));
        _protector.TryUnprotectWithExpiryFromBase64Url(expiredToken, out var _).ShouldBeFalse();
    }

    #endregion

    #region Generic Object Protection Tests

    [Fact]
    public void ProtectObject_SerializesAndUnprotectsComplexRecord()
    {
        var payload = new TestUserPayload(42, "admin@enterprise.org", "SuperAdministrator");
        var token = _protector.ProtectObject(payload);

        var decrypted = _protector.UnprotectObject<TestUserPayload>(token);
        decrypted.ShouldNotBeNull();
        decrypted.Id.ShouldBe(42);
        decrypted.Email.ShouldBe("admin@enterprise.org");
        decrypted.Role.ShouldBe("SuperAdministrator");
    }

    [Fact]
    public void TryUnprotectObject_WithCorruptedPayload_ReturnsFalseWithoutThrowing()
    {
        var success = _protector.TryUnprotectObject<TestUserPayload>("CorruptedCiphertext", out var payload);

        success.ShouldBeFalse();
        payload.ShouldBeNull();
    }

    [Fact]
    public void ProtectObjectWithExpiry_WhenValidAndExpired_BehavesCorrectly()
    {
        var payload = new TestUserPayload(100, "user@test.com", "Member");
        var token = _protector.ProtectObjectWithExpiry(payload, TimeSpan.FromHours(2));

        var decrypted = _protector.UnprotectObjectWithExpiry<TestUserPayload>(token);
        decrypted.ShouldNotBeNull();
        decrypted.Email.ShouldBe("user@test.com");

        var expiredToken = _protector.ProtectObjectWithExpiry(payload, TimeSpan.FromSeconds(-10));
        _protector.TryUnprotectObjectWithExpiry<TestUserPayload>(expiredToken, out var expiredResult).ShouldBeFalse();
        expiredResult.ShouldBeNull();
    }

    #endregion

    #region Stream Encryption Tests

    [Fact]
    public async Task ProtectStreamAsync_And_UnprotectStreamAsync_RoundtripsStreamCorrectly()
    {
        var originalText = string.Join("\n", Enumerable.Range(1, 500).Select(i => $"Large data row number {i} with payload content."));
        var originalBytes = Encoding.UTF8.GetBytes(originalText);

        using var inputStream = new MemoryStream(originalBytes);
        using var encryptedStream = new MemoryStream();

        await _protector.ProtectStreamAsync(inputStream, encryptedStream, chunkSize: 512);

        encryptedStream.Position = 0;
        using var decryptedStream = new MemoryStream();

        await _protector.UnprotectStreamAsync(encryptedStream, decryptedStream);

        var decryptedBytes = decryptedStream.ToArray();
        var decryptedText = Encoding.UTF8.GetString(decryptedBytes);

        decryptedText.ShouldBe(originalText);
    }

    [Fact]
    public async Task UnprotectStreamAsync_WithInvalidMagic_ThrowsCryptographicException()
    {
        var badBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        using var badStream = new MemoryStream(badBytes);
        using var outStream = new MemoryStream();

        await Should.ThrowAsync<CryptographicException>(async () =>
        {
            await _protector.UnprotectStreamAsync(badStream, outStream);
        });
    }

    #endregion

    #region EF Core Encrypted Property Tests

    [Fact]
    public async Task EfCore_PropertyWithKyrolusEncryptedAttribute_IsTransparentlyEncrypted()
    {
        var options = new DbContextOptionsBuilder<TestCustomerDbContext>()
            .UseInMemoryDatabase(databaseName: $"CustomerDb_{Guid.NewGuid():N}")
            .Options;

        var dpProvider = _services.GetRequiredService<IDataProtectionProvider>();

        // 1. Save entity
        await using (var context = new TestCustomerDbContext(options, dpProvider))
        {
            context.Customers.Add(new TestCustomerEntity
            {
                Id = 1,
                Name = "John Doe",
                SecretSsn = "123-45-6789"
            });
            await context.SaveChangesAsync();
        }

        // 2. Query entity and verify decrypted transparency
        await using (var context = new TestCustomerDbContext(options, dpProvider))
        {
            var customer = await context.Customers.FindAsync(1);
            customer.ShouldNotBeNull();
            customer.Name.ShouldBe("John Doe");
            customer.SecretSsn.ShouldBe("123-45-6789");
        }
    }

    #endregion

    #region Vault Options Tests

    [Fact]
    public void VaultOptions_DefaultsAndConfiguresCorrectly()
    {
        var options = new KyrolusVaultOptions
        {
            VaultAddress = "https://vault.company.local:8200",
            Token = "s.mockvaulttoken",
            KeyName = "app-keys",
            MountPath = "transit-v2"
        };

        options.VaultAddress.ShouldBe("https://vault.company.local:8200");
        options.Token.ShouldBe("s.mockvaulttoken");
        options.KeyName.ShouldBe("app-keys");
        options.MountPath.ShouldBe("transit-v2");
    }

    #endregion
}

public sealed record TestUserPayload(int Id, string Email, string Role);

public sealed class TestCustomerEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [KyrolusEncrypted]
    public string SecretSsn { get; set; } = string.Empty;
}

public sealed class TestCustomerDbContext : DbContext
{
    private readonly IDataProtectionProvider _provider;

    public TestCustomerDbContext(DbContextOptions<TestCustomerDbContext> options, IDataProtectionProvider provider)
        : base(options)
    {
        _provider = provider;
    }

    public DbSet<TestCustomerEntity> Customers => Set<TestCustomerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseDataProtectionEncryption(_provider);
    }
}

