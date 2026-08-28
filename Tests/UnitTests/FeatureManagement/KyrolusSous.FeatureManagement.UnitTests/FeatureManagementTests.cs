using KyrolusSous.FeatureManagement.Abstractions;
using KyrolusSous.FeatureManagement.Core;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.FeatureManagement.UnitTests;

public sealed class FeatureManagementTests
{
    [Fact(DisplayName = "Feature Manager Evaluates Static Flags Correctly")]
    public async Task FeatureManager_EvaluatesStaticFlags_Correctly()
    {
        var options = new KyrolusFeatureOptions
        {
            Features =
            {
                ["NewBillingUI"] = new KyrolusFeatureDefinition { Enabled = true },
                ["LegacyExport"] = new KyrolusFeatureDefinition { Enabled = false }
            }
        };

        var manager = new KyrolusFeatureManager(
            Options.Create(options),
            new IKyrolusFeatureFilter[] { new KyrolusPercentageFeatureFilter(), new KyrolusTenantFeatureFilter() });

        (await manager.IsEnabledAsync("NewBillingUI")).ShouldBeTrue();
        (await manager.IsEnabledAsync("LegacyExport")).ShouldBeFalse();
        (await manager.IsEnabledAsync("NonExistent")).ShouldBeFalse();
    }

    [Fact(DisplayName = "Tenant Feature Filter Isolates Allowed Tenants")]
    public async Task TenantFilter_IsolatesAllowedTenants()
    {
        var options = new KyrolusFeatureOptions
        {
            Features =
            {
                ["EnterpriseAnalytics"] = new KyrolusFeatureDefinition
                {
                    Enabled = true,
                    FilterName = "Tenant",
                    Parameters = { ["AllowedTenants"] = "tenant-alpha, tenant-beta" }
                }
            }
        };

        var manager = new KyrolusFeatureManager(
            Options.Create(options),
            new IKyrolusFeatureFilter[] { new KyrolusTenantFeatureFilter() });

        var contextAllowed = new KyrolusFeatureContext { TenantId = "tenant-alpha" };
        (await manager.IsEnabledAsync("EnterpriseAnalytics", contextAllowed)).ShouldBeTrue();

        var contextDenied = new KyrolusFeatureContext { TenantId = "tenant-gamma" };
        (await manager.IsEnabledAsync("EnterpriseAnalytics", contextDenied)).ShouldBeFalse();
    }

    [Fact(DisplayName = "Time Window Feature Filter Respects UTC Dates")]
    public async Task TimeWindowFilter_RespectsUtcDates()
    {
        var options = new KyrolusFeatureOptions
        {
            Features =
            {
                ["BlackFridaySale"] = new KyrolusFeatureDefinition
                {
                    Enabled = true,
                    FilterName = "TimeWindow",
                    Parameters =
                    {
                        ["Start"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O"),
                        ["End"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O")
                    }
                },
                ["ExpiredSale"] = new KyrolusFeatureDefinition
                {
                    Enabled = true,
                    FilterName = "TimeWindow",
                    Parameters =
                    {
                        ["Start"] = DateTimeOffset.UtcNow.AddDays(-2).ToString("O"),
                        ["End"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O")
                    }
                }
            }
        };

        var manager = new KyrolusFeatureManager(
            Options.Create(options),
            new IKyrolusFeatureFilter[] { new KyrolusTimeWindowFeatureFilter() });

        (await manager.IsEnabledAsync("BlackFridaySale")).ShouldBeTrue();
        (await manager.IsEnabledAsync("ExpiredSale")).ShouldBeFalse();
    }
}
