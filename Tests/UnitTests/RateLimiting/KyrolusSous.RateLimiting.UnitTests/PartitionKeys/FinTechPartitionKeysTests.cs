using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class FinTechPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Payment Processor, Transaction Amount Tier, Card Network, and Payment Rail resolve accurately")]
    public void FinTech_Keys_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Payment Processor
        KyrolusRateLimitPartitionKeys.GetPaymentProcessorKey(context).ShouldBe("fin_proc_unknown");
        context.Request.Headers["X-Payment-Processor"] = "Stripe";
        KyrolusRateLimitPartitionKeys.GetPaymentProcessorKey(context).ShouldBe("fin_proc_stripe");
        KyrolusRateLimitPartitionKeys.GetPaymentProcessorAndClientKey(context).ShouldBe("fin_proc_stripe:198.51.100.42");

        // Transaction Amount Tier
        var amtContext = new DefaultHttpContext();
        amtContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetTransactionAmountTierKey(amtContext).ShouldBe("fin_amount_unspecified");

        amtContext.Request.Headers["X-Transaction-Amount"] = "2.50";
        KyrolusRateLimitPartitionKeys.GetTransactionAmountTierKey(amtContext).ShouldBe("fin_amount_micropayment");
        KyrolusRateLimitPartitionKeys.GetTransactionAmountTierAndClientKey(amtContext).ShouldBe("fin_amount_micropayment:198.51.100.42");

        amtContext.Request.Headers["X-Transaction-Amount"] = "250.00";
        KyrolusRateLimitPartitionKeys.GetTransactionAmountTierKey(amtContext).ShouldBe("fin_amount_standard");

        amtContext.Request.Headers["X-Transaction-Amount"] = "50000.00";
        KyrolusRateLimitPartitionKeys.GetTransactionAmountTierKey(amtContext).ShouldBe("fin_amount_high_value");

        // Card Network
        var cardContext = new DefaultHttpContext();
        cardContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetCardNetworkKey(cardContext).ShouldBe("card_unknown");

        cardContext.Request.Headers["X-Card-Network"] = "Visa Signature";
        KyrolusRateLimitPartitionKeys.GetCardNetworkKey(cardContext).ShouldBe("card_visa");
        KyrolusRateLimitPartitionKeys.GetCardNetworkAndClientKey(cardContext).ShouldBe("card_visa:198.51.100.42");

        cardContext.Request.Headers["X-Card-Network"] = "Mastercard Debit";
        KyrolusRateLimitPartitionKeys.GetCardNetworkKey(cardContext).ShouldBe("card_mastercard");

        // Payment Rail
        var railContext = new DefaultHttpContext();
        railContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetPaymentRailKey(railContext).ShouldBe("fin_rail_standard");

        railContext.Request.Headers["X-Payment-Rail"] = "SEPA_Instant";
        KyrolusRateLimitPartitionKeys.GetPaymentRailKey(railContext).ShouldBe("fin_rail_instant");
        KyrolusRateLimitPartitionKeys.GetPaymentRailAndClientKey(railContext).ShouldBe("fin_rail_instant:198.51.100.42");

        railContext.Request.Headers["X-Payment-Rail"] = "ACH_Batch";
        KyrolusRateLimitPartitionKeys.GetPaymentRailKey(railContext).ShouldBe("fin_rail_batch");
    }
}
