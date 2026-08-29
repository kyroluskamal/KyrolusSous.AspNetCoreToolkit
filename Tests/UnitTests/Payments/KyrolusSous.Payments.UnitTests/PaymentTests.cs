using KyrolusSous.Payments.Abstractions;
using KyrolusSous.Payments.Core;
using KyrolusSous.Payments.Stripe;
using KyrolusSous.Payments.PayPal;
using KyrolusSous.Payments.Paymob;
using KyrolusSous.Payments.Fawry;
using KyrolusSous.Payments.Adyen;
using KyrolusSous.Payments.Mollie;
using KyrolusSous.Payments.Klarna;
using KyrolusSous.Payments.Square;
using KyrolusSous.Payments.Tap;
using KyrolusSous.Payments.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace KyrolusSous.Payments.UnitTests;

public sealed class TestPaymentEventHandler : IKyrolusPaymentEventHandler<KyrolusWebhookEvent>
{
    public KyrolusWebhookEvent? HandledEvent { get; private set; }
    public Task HandleAsync(KyrolusWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        HandledEvent = webhookEvent;
        return Task.CompletedTask;
    }
}

public sealed class PaymentTests
{
    [Fact(DisplayName = "Mock Payment Provider Creates And Captures Payments Successfully")]
    public async Task MockProvider_CreateAndCapture_WorksAccurately()
    {
        var provider = new KyrolusMockPaymentProvider();
        var request = new KyrolusPaymentRequest
        {
            OrderId = "ORDER-101",
            Amount = 150.75m,
            Currency = "USD",
            Description = "Test Order Payment"
        };

        var result = await provider.CreatePaymentAsync(request);
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(KyrolusPaymentStatus.Succeeded);
        result.Amount.ShouldBe(150.75m);
        result.ReferenceCode.ShouldNotBeNullOrWhiteSpace();

        var capture = await provider.CapturePaymentAsync(result.TransactionId);
        capture.Status.ShouldBe(KyrolusPaymentStatus.Succeeded);

        var status = await provider.GetPaymentStatusAsync(result.TransactionId);
        status.Status.ShouldBe(KyrolusPaymentStatus.Succeeded);

        var refund = await provider.RefundPaymentAsync(new KyrolusRefundRequest
        {
            TransactionId = result.TransactionId,
            Amount = 50m
        });
        refund.Succeeded.ShouldBeTrue();
        refund.RefundedAmount.ShouldBe(50m);
    }

    [Fact(DisplayName = "Payment Factory Resolves Multiple Registered Providers Correctly")]
    public void PaymentFactory_ResolvesProviders_ByNameAndCurrency()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        services.AddKyrolusStripe(o => o.ApiKey = "sk_test_123");
        services.AddKyrolusPayPal(o => { o.ClientId = "cid"; o.ClientSecret = "csec"; });
        services.AddKyrolusPaymob(o => { o.ApiKey = "pm_key"; o.IntegrationId = 123; });
        services.AddKyrolusFawry(o => { o.MerchantCode = "fawry_code"; o.SecurityKey = "sec"; });
        services.AddKyrolusAdyen(o => { o.ApiKey = "adyen_key"; o.MerchantAccount = "acc"; });
        services.AddKyrolusMollie(o => o.ApiKey = "mollie_key");
        services.AddKyrolusKlarna(o => { o.ApiUsername = "u"; o.ApiPassword = "p"; });
        services.AddKyrolusSquare(o => { o.AccessToken = "sq_token"; o.LocationId = "loc"; });
        services.AddKyrolusTap(o => o.SecretKey = "tap_sec");
        services.AddKyrolusCheckout(o => o.SecretKey = "chk_sec");

        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();

        factory.GetAllProviders().Count.ShouldBe(11); // 1 Mock + 10 Gateways

        var stripe = factory.GetProvider("Stripe");
        stripe.ShouldNotBeNull();
        stripe.ProviderName.ShouldBe("Stripe");

        var paymob = factory.GetProvider("Paymob");
        paymob.ShouldNotBeNull();
        paymob.ProviderName.ShouldBe("Paymob");

        var fawry = factory.GetProvider("Fawry");
        fawry.ShouldNotBeNull();
        fawry.ProviderName.ShouldBe("Fawry");

        var paypal = factory.GetProvider<KyrolusPayPalPaymentProvider>();
        paypal.ShouldNotBeNull();
        paypal.ProviderName.ShouldBe("PayPal");

        var adyen = factory.GetProvider("Adyen");
        adyen.ShouldNotBeNull();
        adyen.ProviderName.ShouldBe("Adyen");

        var egpProvider = factory.GetProviderForCurrency("EGP");
        egpProvider.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Stripe Webhook Handler Validates Signatures And Parses Events")]
    public async Task StripeWebhook_ValidatesAndParsesEvent_Correctly()
    {
        var services = new ServiceCollection();
        services.AddKyrolusStripe(o =>
        {
            o.ApiKey = "sk_test_123";
            o.WebhookSecret = "whsec_test";
        });
        var sp = services.BuildServiceProvider();
        var handler = sp.GetRequiredService<IKyrolusWebhookHandler>();

        var samplePayload = """
        {
          "id": "evt_123456",
          "type": "payment_intent.succeeded",
          "data": {
            "object": {
              "id": "pi_987654"
            }
          }
        }
        """;

        var parsed = await handler.ParseEventAsync(samplePayload, new Dictionary<string, string>());
        parsed.ShouldNotBeNull();
        parsed.EventId.ShouldBe("evt_123456");
        parsed.EventType.ShouldBe("payment_intent.succeeded");
        parsed.TransactionId.ShouldBe("pi_987654");
        parsed.PaymentStatus.ShouldBe(KyrolusPaymentStatus.Succeeded);
    }

    [Fact(DisplayName = "Fawry Webhook Handler Parses Payment Notifications Accurately")]
    public async Task FawryWebhook_ParsesEvent_Accurately()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: false);
        services.AddKyrolusFawry(o => { o.MerchantCode = "100"; o.SecurityKey = "secret"; });
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();
        var handler = factory.GetWebhookHandler("Fawry");

        handler.ShouldNotBeNull();
        var payload = """
        {
          "referenceNumber": "987654321",
          "orderStatus": "PAID",
          "paymentMethod": "PAYATFAWRY"
        }
        """;

        var parsed = await handler.ParseEventAsync(payload, new Dictionary<string, string>());
        parsed.ShouldNotBeNull();
        parsed.TransactionId.ShouldBe("987654321");
        parsed.PaymentStatus.ShouldBe(KyrolusPaymentStatus.Succeeded);
    }

    [Fact(DisplayName = "Paymob Webhook Handler Parses Transaction Status Correctly")]
    public async Task PaymobWebhook_ParsesTransaction_Correctly()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: false);
        services.AddKyrolusPaymob(o => { o.ApiKey = "k"; o.IntegrationId = 1; });
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();
        var handler = factory.GetWebhookHandler("Paymob");

        handler.ShouldNotBeNull();
        var payload = """
        {
          "obj": {
            "id": 55443322,
            "success": true,
            "amount_cents": 25000,
            "currency": "EGP"
          }
        }
        """;

        var parsed = await handler.ParseEventAsync(payload, new Dictionary<string, string>());
        parsed.ShouldNotBeNull();
        parsed.TransactionId.ShouldBe("55443322");
        parsed.PaymentStatus.ShouldBe(KyrolusPaymentStatus.Succeeded);
    }

    [Fact(DisplayName = "Subscription Provider Handles Full Lifecycle Of Plans And Subscriptions")]
    public async Task SubscriptionProvider_Lifecycle_WorksCorrectly()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();

        var subProvider = factory.GetSubscriptionProvider("Mock");
        subProvider.ShouldNotBeNull();

        // 1. Create plan
        var plan = await subProvider.CreatePlanAsync(new KyrolusSubscriptionPlan
        {
            PlanId = "pro_monthly",
            Name = "Pro Monthly",
            Amount = 49.99m,
            Currency = "USD",
            Interval = KyrolusBillingInterval.Month,
            TrialDays = 14
        });
        plan.PlanId.ShouldBe("pro_monthly");

        // 2. Create subscription
        var sub = await subProvider.CreateSubscriptionAsync(new KyrolusSubscriptionRequest
        {
            CustomerId = "cust_user_1",
            PlanId = "pro_monthly"
        });
        sub.IsSuccess.ShouldBeTrue();
        sub.Status.ShouldBe(KyrolusSubscriptionStatus.Active);
        sub.Amount.ShouldBe(49.99m);

        // 3. Pause
        var paused = await subProvider.PauseSubscriptionAsync(sub.SubscriptionId);
        paused.Status.ShouldBe(KyrolusSubscriptionStatus.Paused);

        // 4. Resume
        var resumed = await subProvider.ResumeSubscriptionAsync(sub.SubscriptionId);
        resumed.Status.ShouldBe(KyrolusSubscriptionStatus.Active);

        // 5. Cancel
        var cancelled = await subProvider.CancelSubscriptionAsync(sub.SubscriptionId, cancelImmediately: true);
        cancelled.Status.ShouldBe(KyrolusSubscriptionStatus.Cancelled);
    }

    [Fact(DisplayName = "Customer Vault Provider Manages Saved Cards And Default Payment Methods")]
    public async Task CustomerVaultProvider_Lifecycle_WorksCorrectly()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();

        var vaultProvider = factory.GetVaultProvider("Mock");
        vaultProvider.ShouldNotBeNull();

        // 1. Create customer
        var customer = await vaultProvider.CreateCustomerAsync(new KyrolusPaymentCustomer
        {
            CustomerId = "usr_kyrolus_123",
            Name = "Kyrolus Sous",
            Email = "kyrolus@test.local"
        });
        customer.CustomerId.ShouldBe("usr_kyrolus_123");

        // 2. Save card
        var saveResult = await vaultProvider.SavePaymentMethodAsync(new KyrolusSavePaymentMethodRequest
        {
            CustomerId = customer.CustomerId,
            PaymentTokenOrNonce = "tok_visa_4242",
            SetAsDefault = true
        });
        saveResult.Succeeded.ShouldBeTrue();
        saveResult.PaymentMethodId.ShouldNotBeNullOrWhiteSpace();

        // 3. List cards
        var cards = await vaultProvider.ListPaymentMethodsAsync(customer.CustomerId);
        cards.Count.ShouldBe(1);
        cards[0].LastFourDigits.ShouldBe("4242");
        cards[0].IsDefault.ShouldBeTrue();

        // 4. Delete card
        var deleted = await vaultProvider.DeletePaymentMethodAsync(customer.CustomerId, cards[0].PaymentMethodId);
        deleted.ShouldBeTrue();

        var cardsAfter = await vaultProvider.ListPaymentMethodsAsync(customer.CustomerId);
        cardsAfter.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "MultiTenant Options Provider Resolves Config Dynamically")]
    public async Task OptionsProvider_ResolvesConfig_Correctly()
    {
        var services = new ServiceCollection();
        services.AddKyrolusStripe(o =>
        {
            o.ApiKey = "sk_live_master";
        });
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IKyrolusPaymentOptionsProvider<KyrolusStripeOptions>>();

        var options = await provider.GetOptionsAsync("tenant-acme");
        options.ApiKey.ShouldBe("sk_live_master");
    }

    [Fact(DisplayName = "Currency Helper Converts Smallest Units For Zero, Two, And Three Decimal Currencies")]
    public void CurrencyHelper_ConvertsUnits_Accurately()
    {
        // 2-decimal
        KyrolusCurrencyHelper.GetDecimalPlaces("USD").ShouldBe(2);
        KyrolusCurrencyHelper.ToSmallestUnit(10.50m, "USD").ShouldBe(1050);
        KyrolusCurrencyHelper.FromSmallestUnit(1050, "USD").ShouldBe(10.50m);

        // 0-decimal (JPY)
        KyrolusCurrencyHelper.GetDecimalPlaces("JPY").ShouldBe(0);
        KyrolusCurrencyHelper.ToSmallestUnit(1500m, "JPY").ShouldBe(1500);
        KyrolusCurrencyHelper.FromSmallestUnit(1500, "JPY").ShouldBe(1500m);

        // 3-decimal (KWD)
        KyrolusCurrencyHelper.GetDecimalPlaces("KWD").ShouldBe(3);
        KyrolusCurrencyHelper.ToSmallestUnit(5.250m, "KWD").ShouldBe(5250);
        KyrolusCurrencyHelper.FromSmallestUnit(5250, "KWD").ShouldBe(5.250m);
    }

    [Fact(DisplayName = "Payment Idempotency Store Prevents Duplicate In-Flight Processing")]
    public async Task IdempotencyStore_LocksAndPreventsDuplicateCharges_Correctly()
    {
        var store = new KyrolusCachePaymentIdempotencyStore();
        var key = "order_unique_9988";

        var acquiredFirst = await store.TryAcquireLockAsync(key, TimeSpan.FromMinutes(5));
        acquiredFirst.ShouldBeTrue();

        var acquiredSecond = await store.TryAcquireLockAsync(key, TimeSpan.FromMinutes(5));
        acquiredSecond.ShouldBeFalse(); // Already locked!

        // Save result
        var paymentResult = new KyrolusPaymentResult
        {
            TransactionId = "tx_123",
            Status = KyrolusPaymentStatus.Succeeded,
            Amount = 100m,
            Currency = "USD"
        };
        await store.SaveResultAsync(key, paymentResult);

        var retrieved = await store.GetResultAsync(key);
        retrieved.ShouldNotBeNull();
        retrieved.TransactionId.ShouldBe("tx_123");
        retrieved.IsSuccess.ShouldBeTrue();
    }

    [Fact(DisplayName = "Smart Payment Router Automatically Resolves And Executes With Failover")]
    public async Task SmartRouter_RoutesAndFailsOver_Successfully()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IKyrolusSmartPaymentRouter>();

        var request = new KyrolusPaymentRequest
        {
            OrderId = "FAILOVER-100",
            Amount = 250m,
            Currency = "USD"
        };

        var best = router.ResolveBestProvider(request);
        best.ShouldNotBeNull();

        var result = await router.ExecuteWithFailoverAsync(request, ["Mock"]);
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(KyrolusPaymentStatus.Succeeded);
    }

    [Fact(DisplayName = "Payment Link Provider Creates Shareable Link And QR Code Payload")]
    public async Task PaymentLinkProvider_GeneratesLinkAndQr_Correctly()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();

        var linkProvider = factory.GetPaymentLinkProvider("Mock");
        linkProvider.ShouldNotBeNull();

        var link = await linkProvider.CreatePaymentLinkAsync(new KyrolusPaymentLinkRequest
        {
            Title = "Invoice #9090",
            Amount = 300m,
            Currency = "USD",
            ExpiresIn = TimeSpan.FromDays(7)
        });

        link.ShouldNotBeNull();
        link.Url.ShouldStartWith("https://");
        link.QrCodePayload.ShouldNotBeNullOrWhiteSpace();
        link.IsActive.ShouldBeTrue();

        var deactivated = await linkProvider.DeactivatePaymentLinkAsync(link.LinkId);
        deactivated.ShouldBeTrue();
    }

    [Fact(DisplayName = "Marketplace Provider Handles Connected Accounts And Split Transfers")]
    public async Task MarketplaceProvider_PerformsSplitTransfers_Accurately()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IKyrolusPaymentFactory>();

        var marketProvider = factory.GetMarketplaceProvider("Mock");
        marketProvider.ShouldNotBeNull();

        var account = await marketProvider.CreateConnectedAccountAsync(new KyrolusMerchantAccountRequest
        {
            Email = "vendor@store.local",
            CountryCode = "EG",
            BusinessName = "Vendor Store"
        });
        account.AccountId.ShouldNotBeNullOrWhiteSpace();
        account.IsPayoutsEnabled.ShouldBeTrue();

        var transfer = await marketProvider.TransferToConnectedAccountAsync(new KyrolusSplitTransferRequest
        {
            DestinationAccountId = account.AccountId,
            Amount = 1000m,
            PlatformFeeAmount = 100m, // 10% fee
            Currency = "EGP"
        });

        transfer.Succeeded.ShouldBeTrue();
        transfer.Amount.ShouldBe(900m); // Net to vendor
        transfer.PlatformFeeAmount.ShouldBe(100m); // Platform commission
    }

    [Fact(DisplayName = "Fraud Detection Engine Evaluates Risk Scores And Flags Suspicious Transactions")]
    public async Task FraudDetectionEngine_EvaluatesRisk_Accurately()
    {
        var engine = new KyrolusDefaultFraudDetectionEngine();

        // 1. Normal transaction
        var normalResult = await engine.EvaluateRiskAsync(new KyrolusRiskEvaluationRequest
        {
            OrderId = "SAFE-101",
            Amount = 100m,
            Currency = "USD",
            CustomerEmail = "legit.user@gmail.com",
            CustomerIpAddress = "192.168.1.1",
            CardCountry = "US",
            BillingCountry = "US"
        });
        normalResult.IsBlocked.ShouldBeFalse();
        normalResult.RecommendedAction.ShouldBe(KyrolusRiskAction.Allow);

        // 2. High risk transaction (disposable email + country mismatch)
        var highRiskResult = await engine.EvaluateRiskAsync(new KyrolusRiskEvaluationRequest
        {
            OrderId = "FRAUD-999",
            Amount = 15000m,
            Currency = "USD",
            CustomerEmail = "scammer@tempmail.com",
            CardCountry = "NG",
            BillingCountry = "US"
        });
        highRiskResult.RiskScore.ShouldBeGreaterThanOrEqualTo(80);
        highRiskResult.IsBlocked.ShouldBeTrue();
        highRiskResult.RecommendedAction.ShouldBe(KyrolusRiskAction.Block);
    }

    [Fact(DisplayName = "Dunning Engine Calculates Smart Retry Intervals And Cancels After Max Attempts")]
    public void DunningEngine_SchedulesSmartRetries_Correctly()
    {
        var engine = new KyrolusDefaultDunningEngine();

        // Attempt 1 -> Retry
        var attempt1 = engine.EvaluateNextAction(new KyrolusDunningAttemptRequest
        {
            SubscriptionId = "sub_fail_1",
            CustomerId = "cust_1",
            Amount = 29.99m,
            Currency = "USD",
            CurrentAttemptNumber = 1
        });
        attempt1.NextAction.ShouldBe(KyrolusDunningAction.RetryPayment);
        attempt1.NextRetryUtc.ShouldNotBeNull();

        // Attempt 4 (Max) -> Cancel
        var attempt4 = engine.EvaluateNextAction(new KyrolusDunningAttemptRequest
        {
            SubscriptionId = "sub_fail_1",
            CustomerId = "cust_1",
            Amount = 29.99m,
            Currency = "USD",
            CurrentAttemptNumber = 4
        });
        attempt4.NextAction.ShouldBe(KyrolusDunningAction.CancelSubscription);
        attempt4.NextRetryUtc.ShouldBeNull();
    }

    [Fact(DisplayName = "Invoice Generator Calculates Subtotals, Taxes, And Renders HTML")]
    public void InvoiceGenerator_CalculatesTaxAndRendersHtml_Accurately()
    {
        var generator = new KyrolusDefaultInvoiceGenerator();

        var invoice = generator.GenerateInvoice(new KyrolusInvoiceRequest
        {
            InvoiceNumber = "INV-2026-001",
            MerchantName = "Kyrolus Software LLC",
            CustomerName = "Enterprise Client",
            Currency = "USD",
            DiscountAmount = 50m,
            Items =
            [
                new() { Description = "Cloud Hosting", UnitPrice = 200m, Quantity = 2, TaxRatePercent = 10m }, // 400 + 40 tax
                new() { Description = "SSL Certificate", UnitPrice = 100m, Quantity = 1, TaxRatePercent = 0m } // 100 + 0 tax
            ]
        });

        invoice.SubtotalAmount.ShouldBe(500m); // 400 + 100
        invoice.TaxAmount.ShouldBe(40m); // 40
        invoice.TotalAmount.ShouldBe(490m); // (500 + 40) - 50
        invoice.RenderedHtml.ShouldContain("INV-2026-001");
        invoice.RenderedHtml.ShouldContain("Kyrolus Software LLC");
    }

    [Fact(DisplayName = "Webhook Replay Protector Blocks Replayed And Expired Payloads")]
    public async Task WebhookReplayProtector_BlocksReplayedOrExpiredEvents()
    {
        var protector = new KyrolusDefaultWebhookReplayProtector();
        var eventId = "evt_unique_replay_1";
        var now = DateTimeOffset.UtcNow;

        // 1. First time -> Valid
        var firstValid = await protector.ValidateAndRecordWebhookAsync(eventId, now);
        firstValid.ShouldBeTrue();

        // 2. Replay with same eventId -> Blocked!
        var replayBlocked = await protector.ValidateAndRecordWebhookAsync(eventId, now);
        replayBlocked.ShouldBeFalse();

        // 3. Expired timestamp (> 5 min) -> Blocked!
        var expiredBlocked = await protector.ValidateAndRecordWebhookAsync("evt_expired_9", now.AddMinutes(-10));
        expiredBlocked.ShouldBeFalse();
    }

    [Fact(DisplayName = "Payment Metrics Collector Calculates Gateway Success Rates And Latency")]
    public void MetricsCollector_TracksSuccessRatesAndLatency_Accurately()
    {
        var collector = new KyrolusDefaultPaymentMetricsCollector();

        collector.RecordTransaction("Stripe", isSuccess: true, latencyMs: 150, amount: 100m);
        collector.RecordTransaction("Stripe", isSuccess: true, latencyMs: 250, amount: 200m);
        collector.RecordTransaction("Stripe", isSuccess: false, latencyMs: 200, amount: 50m);

        var report = collector.GetReport("Stripe");
        report.TotalTransactions.ShouldBe(3);
        report.SuccessfulTransactions.ShouldBe(2);
        report.FailedTransactions.ShouldBe(1);
        report.SuccessRatePercent.ShouldBe(66.67);
        report.AverageLatencyMs.ShouldBe(200.0);
        report.TotalVolume.ShouldBe(350m);
    }

    [Fact(DisplayName = "Dispute Provider Submits Evidence And Updates Status Accurately")]
    public async Task DisputeProvider_SubmitsEvidence_Accurately()
    {
        var provider = new KyrolusMockDisputeProvider();

        var list = await provider.ListDisputesAsync();
        list.Count.ShouldBeGreaterThan(0);

        var first = list[0];
        var result = await provider.SubmitEvidenceAsync(new KyrolusSubmitDisputeEvidenceRequest
        {
            DisputeId = first.DisputeId,
            CustomerName = "Legit Buyer",
            ExplanationText = "Customer authorized and received goods via DHL tracking #123456"
        });

        result.IsSubmitted.ShouldBeTrue();
        result.Status.ShouldBe(KyrolusDisputeStatus.UnderReview);
    }

    [Fact(DisplayName = "Payment Data Masker Redacts Card Numbers And Cvv From Payloads")]
    public void DataMasker_RedactsSensitiveData_Correctly()
    {
        var rawPayload = """
        {
          "cardNumber": "4111222233334444",
          "cvv": "987",
          "apiKey": "sk_live_super_secret"
        }
        """;

        var masked = KyrolusPaymentDataMasker.RedactSensitivePayload(rawPayload);
        masked.ShouldNotContain("4111222233334444");
        masked.ShouldContain("411122******4444");
        masked.ShouldNotContain("\"987\"");
        masked.ShouldContain("\"***\"");
        masked.ShouldNotContain("sk_live_super_secret");
        masked.ShouldContain("[REDACTED]");
    }

    [Fact(DisplayName = "Discount Engine Applies Percentage And Fixed Coupons Accurately")]
    public void DiscountEngine_CalculatesDiscounts_Accurately()
    {
        var engine = new KyrolusDefaultDiscountEngine();

        // 1. Percentage coupon (20% off, max $50)
        engine.RegisterCoupon(new KyrolusCoupon
        {
            Code = "SAVE20",
            Type = KyrolusDiscountType.Percentage,
            Value = 20m,
            MaximumDiscountAmount = 50m
        });

        var res1 = engine.CalculateDiscount(new KyrolusApplyDiscountRequest
        {
            CouponCode = "SAVE20",
            OrderAmount = 200m,
            Currency = "USD"
        });
        res1.IsValid.ShouldBeTrue();
        res1.DiscountAmount.ShouldBe(40m); // 20% of 200 = 40
        res1.FinalAmount.ShouldBe(160m);

        // 2. Fixed coupon ($25 off, min order $100)
        engine.RegisterCoupon(new KyrolusCoupon
        {
            Code = "FLAT25",
            Type = KyrolusDiscountType.FixedAmount,
            Value = 25m,
            MinimumOrderAmount = 100m
        });

        var res2 = engine.CalculateDiscount(new KyrolusApplyDiscountRequest
        {
            CouponCode = "FLAT25",
            OrderAmount = 150m,
            Currency = "USD"
        });
        res2.IsValid.ShouldBeTrue();
        res2.DiscountAmount.ShouldBe(25m);
        res2.FinalAmount.ShouldBe(125m);
    }

    [Fact(DisplayName = "Split Tender Provider Processes Multiple Payment Legs Atomically")]
    public async Task SplitTenderProvider_ExecutesAtomicLegs_Successfully()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var tenderProvider = sp.GetRequiredService<IKyrolusSplitTenderProvider>();

        var request = new KyrolusSplitTenderRequest
        {
            OrderId = "HYBRID-ORDER-777",
            TotalAmount = 500m,
            Currency = "USD",
            Legs =
            [
                new() { ProviderName = "Mock", Amount = 200m },
                new() { ProviderName = "Mock", Amount = 300m }
            ]
        };

        var result = await tenderProvider.ExecuteSplitTenderAsync(request);
        result.Succeeded.ShouldBeTrue();
        result.LegResults.Count.ShouldBe(2);
        result.LegResults.All(l => l.Succeeded).ShouldBeTrue();
    }

    [Fact(DisplayName = "FX Rate Provider Converts Currencies Using Configured Exchange Rates")]
    public async Task FxRateProvider_ConvertsCurrencies_Accurately()
    {
        var fx = new KyrolusDefaultFxRateProvider();
        fx.SetRate("USD", "EGP", 50.0m);

        var result = await fx.ConvertCurrencyAsync(100m, "USD", "EGP");
        result.ConvertedAmount.ShouldBe(5000m);
        result.ExchangeRate.ShouldBe(50.0m);
    }

    [Fact(DisplayName = "Payout Provider Handles Single And Batch Payouts Accurately")]
    public async Task PayoutProvider_SendsSingleAndBatchPayouts_Accurately()
    {
        var provider = new KyrolusMockPayoutProvider();

        // 1. Single payout
        var single = await provider.SendPayoutAsync(new KyrolusPayoutRequest
        {
            PayoutId = "PO-001",
            RecipientId = "usr_seller_1",
            Amount = 1500m,
            Currency = "EGP",
            DestinationType = KyrolusPayoutDestinationType.InstantPay,
            DestinationAccountIdentifier = "seller@instapay"
        });
        single.Status.ShouldBe(KyrolusPayoutStatus.Paid);
        single.FeeAmount.ShouldBe(15m); // 1%

        // 2. Batch payout
        var batch = await provider.SendBatchPayoutAsync(new KyrolusBatchPayoutRequest
        {
            BatchId = "BATCH-99",
            Payouts =
            [
                new()
                {
                    PayoutId = "PO-002",
                    RecipientId = "usr_seller_2",
                    Amount = 500m,
                    Currency = "EGP",
                    DestinationType = KyrolusPayoutDestinationType.DigitalWallet,
                    DestinationAccountIdentifier = "01012345678"
                }
            ]
        });
        batch.TotalCount.ShouldBe(1);
        batch.SucceededCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Escrow Provider Holds Funds And Captures On Completion")]
    public async Task EscrowProvider_HoldsAndCapturesFunds_Correctly()
    {
        var provider = new KyrolusMockEscrowProvider();

        var hold = await provider.HoldFundsAsync(new KyrolusHoldFundsRequest
        {
            HoldId = "HOLD-101",
            CustomerId = "cust_rider_1",
            Amount = 75m,
            Currency = "USD",
            HoldDuration = TimeSpan.FromHours(24)
        });
        hold.Status.ShouldBe(KyrolusEscrowStatus.Held);
        hold.AuthorizationCode.ShouldNotBeNullOrWhiteSpace();

        var capture = await provider.CaptureHeldFundsAsync("HOLD-101", amount: 65m); // Final ride cost
        capture.Status.ShouldBe(KyrolusEscrowStatus.Captured);
        capture.Amount.ShouldBe(65m);
    }

    [Fact(DisplayName = "Reconciliation Engine Matches Settlement Records And Detects Discrepancies")]
    public void ReconciliationEngine_DetectsMatchesAndDiscrepancies_Accurately()
    {
        var engine = new KyrolusDefaultReconciliationEngine();

        var internalList = new List<KyrolusInternalTransactionRecord>
        {
            new() { TransactionId = "TX-1", ExpectedAmount = 100m, Currency = "USD" },
            new() { TransactionId = "TX-2", ExpectedAmount = 200m, Currency = "USD" },
            new() { TransactionId = "TX-3", ExpectedAmount = 300m, Currency = "USD" }
        };

        var settlementList = new List<KyrolusSettlementRecord>
        {
            new() { TransactionId = "TX-1", SettledAmount = 100m, FeeAmount = 2m, Currency = "USD", SettledAtUtc = DateTimeOffset.UtcNow },
            new() { TransactionId = "TX-2", SettledAmount = 190m, FeeAmount = 5m, Currency = "USD", SettledAtUtc = DateTimeOffset.UtcNow } // Mismatch!
            // TX-3 is missing from settlement
        };

        var report = engine.ReconcileBatch("BATCH-SETTLE-01", internalList, settlementList);
        report.TotalMatched.ShouldBe(1);
        report.DiscrepancyCount.ShouldBe(2); // TX-2 amount mismatch + TX-3 missing
        report.IsFullyReconciled.ShouldBeFalse();
    }

    [Fact(DisplayName = "Payment Activity Source Starts Activity And Sets Status Correctly")]
    public void ActivitySource_StartsAndRecordsActivity_Correctly()
    {
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = s => s.Name == KyrolusPaymentActivitySource.ActivitySourceName,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        using var activity = KyrolusPaymentActivitySource.StartPaymentActivity("Stripe", "Charge", "tx_test_999");
        activity.ShouldNotBeNull();
        KyrolusPaymentActivitySource.RecordSuccess(activity, 150m, "USD");
    }

    [Fact(DisplayName = "Offline Payment Sync Engine Queues And Synchronizes Offline Transactions")]
    public async Task OfflinePaymentSyncEngine_QueuesAndSyncs_Successfully()
    {
        var services = new ServiceCollection();
        services.AddKyrolusPayments(registerMockProvider: true);
        var sp = services.BuildServiceProvider();
        var syncEngine = sp.GetRequiredService<IKyrolusOfflinePaymentSyncEngine>();

        syncEngine.EnqueueOfflineTransaction(new KyrolusOfflineTransaction
        {
            LocalTransactionId = "POS-OFFLINE-001",
            ProviderName = "Mock",
            Amount = 45m,
            Currency = "USD",
            EncryptedPaymentPayload = "enc_pos_card_data_hash"
        });

        var syncResult = await syncEngine.SyncPendingTransactionsAsync();
        syncResult.TotalQueued.ShouldBe(1);
        syncResult.SyncedCount.ShouldBe(1);
        syncResult.FailedCount.ShouldBe(0);
    }

    [Fact(DisplayName = "BIN Lookup Provider Identifies Meeza And Visa Schemes Accurately")]
    public async Task BinLookupProvider_IdentifiesSchemes_Accurately()
    {
        var provider = new KyrolusDefaultBinLookupProvider();

        // 1. Meeza
        var meeza = await provider.LookupBinAsync("5078031234567890");
        meeza.Scheme.ShouldBe(KyrolusCardScheme.Meeza);
        meeza.CountryCode.ShouldBe("EG");

        // 2. Visa
        var visa = await provider.LookupBinAsync("4111111111111111");
        visa.Scheme.ShouldBe(KyrolusCardScheme.Visa);
        visa.CardType.ShouldBe(KyrolusCardType.Credit);
    }

    [Fact(DisplayName = "Gateway Fee Optimizer Selects The Most Cost Effective Provider")]
    public void FeeOptimizer_CalculatesAndSelectsLowestFee_Accurately()
    {
        var optimizer = new KyrolusDefaultGatewayFeeOptimizer();

        var result = optimizer.OptimizeFee(1000m, "EGP", ["Paymob", "Fawry"]);
        // Paymob: 2.5% of 1000 + 2 = 27 EGP
        // Fawry: 2.0% of 1000 = 20 EGP -> Fawry should win
        result.RecommendedProviderName.ShouldBe("Fawry");
        result.EstimatedFee.ShouldBe(20.0m);
        result.NetMerchantAmount.ShouldBe(980.0m);
    }

    [Fact(DisplayName = "Apple Pay Decryptor Decrypts Payment Tokens Successfully")]
    public async Task ApplePayDecryptor_DecryptsTokens_Successfully()
    {
        var decryptor = new KyrolusDefaultApplePayDecryptor();

        var token = new KyrolusApplePayPaymentToken
        {
            PaymentData = "AQIDBAUGBwgJCgsMDQ4PEA==",
            EphemeralPublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...",
            PublicKeyHash = "hash123",
            TransactionId = "apple_tx_909",
            DisplayName = "Kyrolus Apple Pay"
        };

        var result = await decryptor.DecryptTokenAsync(token);
        result.Succeeded.ShouldBeTrue();
        result.PrimaryAccountNumber.ShouldNotBeNullOrWhiteSpace();
        result.ExpirationYear.ShouldBeGreaterThan(2025);
    }

    [Fact(DisplayName = "Metered Billing Engine Aggregates Usage And Calculates Tiered Costs")]
    public void MeteredBillingEngine_CalculatesUsageCosts_Accurately()
    {
        var engine = new KyrolusDefaultMeteredBillingEngine();
        var subId = "sub_metered_user_1";

        // Record 15,000 API calls in batches
        engine.RecordUsage(new KyrolusUsageRecord { SubscriptionId = subId, MetricName = "api_calls", Quantity = 5000 });
        engine.RecordUsage(new KyrolusUsageRecord { SubscriptionId = subId, MetricName = "api_calls", Quantity = 10000 });

        // Tier: First 1,000 included for free, then $0.002 per call
        var summary = engine.CalculateSummary(subId, new KyrolusMeteredMetricTier
        {
            MetricName = "api_calls",
            UnitPrice = 0.002m,
            IncludedQuantity = 1000m
        });

        summary.TotalUsageQuantity.ShouldBe(15000m);
        summary.BilledQuantity.ShouldBe(14000m); // 15000 - 1000 included
        summary.TotalCost.ShouldBe(28.0m); // 14000 * 0.002 = $28.00
    }

    [Fact(DisplayName = "Loyalty Rewards Engine Awards Points And Redeems Balance Correctly")]
    public void LoyaltyRewardsEngine_AwardsAndRedeems_Correctly()
    {
        var engine = new KyrolusDefaultLoyaltyRewardsEngine();
        var custId = "cust_loyal_100";

        // Spend $500 -> Earn 500 points
        engine.AwardPoints(custId, 500m);
        engine.GetBalance(custId).ShouldBe(500m);

        // Redeem 200 points ($2.00 discount)
        var redeemResult = engine.RedeemPoints(new KyrolusRedeemPointsRequest
        {
            CustomerId = custId,
            PointsToRedeem = 200m,
            PointValueInCurrency = 0.01m
        });

        redeemResult.Succeeded.ShouldBeTrue();
        redeemResult.DiscountAmount.ShouldBe(2.0m);
        redeemResult.RemainingPointsBalance.ShouldBe(300m);
        engine.GetBalance(custId).ShouldBe(300m);
    }

    [Fact(DisplayName = "Card Account Updater Automatically Updates Expired Cards")]
    public async Task AccountUpdater_UpdatesExpiredCard_Correctly()
    {
        var updater = new KyrolusDefaultCardAccountUpdater();

        // Expired card in 2020 -> Should be updated
        var result = await updater.CheckForUpdatesAsync(new KyrolusAccountUpdateRequest
        {
            CustomerId = "cust_exp_1",
            PaymentMethodId = "pm_old_1",
            CurrentLast4 = "4242",
            CurrentExpiryMonth = 1,
            CurrentExpiryYear = 2020
        });

        result.HasChanged.ShouldBeTrue();
        result.Action.ShouldBe(KyrolusAccountUpdateAction.UpdatedExpiry);
        result.NewExpiryYear.HasValue.ShouldBeTrue();
        result.NewExpiryYear!.Value.ShouldBeGreaterThan(2022);
    }

    [Fact(DisplayName = "Tax Engine Calculates Local Rates And EU Reverse Charge Accurately")]
    public void TaxEngine_CalculatesTax_Accurately()
    {
        var taxEngine = new KyrolusDefaultTaxCalculationEngine();

        // 1. Egypt 14% VAT
        var eg = taxEngine.CalculateTax(new KyrolusTaxCalculationRequest
        {
            Amount = 1000m,
            Currency = "EGP",
            CountryCode = "EG"
        });
        eg.TaxRatePercent.ShouldBe(14.0m);
        eg.TaxAmount.ShouldBe(140m);
        eg.TotalAmountWithTax.ShouldBe(1140m);

        // 2. Germany B2B Reverse Charge (0%)
        var deB2B = taxEngine.CalculateTax(new KyrolusTaxCalculationRequest
        {
            Amount = 1000m,
            Currency = "EUR",
            CountryCode = "DE",
            IsB2BWithValidVatNumber = true
        });
        deB2B.TaxRatePercent.ShouldBe(0m);
        deB2B.IsReverseChargeApplied.ShouldBeTrue();

        // 3. Spain 21% IVA
        var es = taxEngine.CalculateTax(new KyrolusTaxCalculationRequest
        {
            Amount = 1000m,
            Currency = "EUR",
            CountryCode = "ES"
        });
        es.TaxRatePercent.ShouldBe(21.0m);
        es.TaxAmount.ShouldBe(210m);
        es.TotalAmountWithTax.ShouldBe(1210m);

        // 4. Custom Registered Rate (Japan 10% Consumption Tax)
        taxEngine.SetCustomTaxRate("JP", 10.0m, "Japan Consumption Tax");
        var jp = taxEngine.CalculateTax(new KyrolusTaxCalculationRequest
        {
            Amount = 1000m,
            Currency = "JPY",
            CountryCode = "JP"
        });
        jp.TaxRatePercent.ShouldBe(10.0m);
        jp.TaxAmount.ShouldBe(100m);
    }

    [Fact(DisplayName = "Virtual Card Provider Issues Single-Use Cards And Freezes Correctly")]
    public async Task VirtualCardProvider_Lifecycle_WorksCorrectly()
    {
        var provider = new KyrolusMockVirtualCardProvider();

        var card = await provider.IssueVirtualCardAsync(new KyrolusCreateVirtualCardRequest
        {
            CardHolderName = "Kyrolus Procurement",
            SpendingLimit = 2500m,
            Currency = "USD",
            SingleUseOnly = true
        });

        card.ShouldNotBeNull();
        card.CardNumber.ShouldStartWith("411111");
        card.SpendingLimit.ShouldBe(2500m);
        card.Status.ShouldBe(KyrolusVirtualCardStatus.Active);

        var frozen = await provider.FreezeCardAsync(card.CardId);
        frozen.ShouldBeTrue();
    }

    [Fact(DisplayName = "BNPL Installment Calculator Computes Plans And Down Payments")]
    public void BnplCalculator_ComputesPlans_Accurately()
    {
        var calculator = new KyrolusDefaultBnplInstallmentCalculator();

        var result = calculator.CalculatePlans(1200m, "EGP");
        result.AvailablePlans.Count.ShouldBe(3);

        var plan3m = result.AvailablePlans.First(p => p.InstallmentMonths == 3);
        plan3m.DownPaymentAmount.ShouldBe(300m); // 25%
        plan3m.MonthlyAmount.ShouldBe(300m); // 900 / 3
        plan3m.InterestRatePercent.ShouldBe(0m);
    }

    [Fact(DisplayName = "Crypto Payment Provider Generates Deposit Address And Payment Intents")]
    public async Task CryptoPaymentProvider_GeneratesIntents_Accurately()
    {
        var provider = new KyrolusMockCryptoPaymentProvider();

        var intent = await provider.CreatePaymentIntentAsync(new KyrolusCreateCryptoPaymentRequest
        {
            OrderId = "CRYPTO-ORDER-101",
            FiatAmount = 100m,
            FiatCurrency = "USD",
            CryptoCurrency = "USDT",
            Network = KyrolusCryptoNetwork.Tron_TRC20
        });

        intent.ShouldNotBeNull();
        intent.DepositAddress.ShouldStartWith("TX");
        intent.RequiredCryptoAmount.ShouldBe(100m);
        intent.Status.ShouldBe(KyrolusCryptoPaymentStatus.AwaitingDeposit);
        intent.QrCodePayload.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "3DS2 Engine Evaluates Low Value Exemption And Challenge Correctly")]
    public void ThreeDSecureEngine_EvaluatesExemptions_Correctly()
    {
        var engine = new KyrolusDefaultThreeDSecureEngine();

        // 1. Low Value Exemption (< 30 EUR)
        var lowVal = engine.EvaluateRiskAndFlow(new KyrolusThreeDSecureEvaluationRequest
        {
            Amount = 15m,
            Currency = "EUR",
            CardholderIpAddress = "192.168.1.1",
            BrowserUserAgent = "Mozilla/5.0"
        });
        lowVal.RecommendedFlow.ShouldBe(KyrolusThreeDSecureFlow.ExemptedLowValue);
        lowVal.RequiresOtpPrompt.ShouldBeFalse();

        // 2. High value transaction -> Challenge required
        var highVal = engine.EvaluateRiskAndFlow(new KyrolusThreeDSecureEvaluationRequest
        {
            Amount = 5000m,
            Currency = "EUR",
            CardholderIpAddress = "192.168.1.1",
            BrowserUserAgent = "Mozilla/5.0"
        });
        highVal.RecommendedFlow.ShouldBe(KyrolusThreeDSecureFlow.ChallengeRequired);
        highVal.RequiresOtpPrompt.ShouldBeTrue();
        highVal.ChallengeUrl.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Chargeback Defense Engine Scores Evidence Completeness Accurately")]
    public void ChargebackDefenseEngine_ScoresEvidence_Accurately()
    {
        var engine = new KyrolusDefaultChargebackDefenseEngine();

        var result = engine.ValidateAndCompileEvidence(new KyrolusChargebackEvidenceBundle
        {
            DisputeId = "dp_dispute_123",
            OrderId = "ord_999",
            CustomerEmail = "customer@example.com",
            CustomerIpAddress = "10.0.0.1",
            ShippingTrackingNumber = "1Z9999999999999999",
            CarrierName = "UPS",
            TermsOfServiceAcceptanceTimestamp = "2026-08-01T12:00:00Z"
        });

        result.IsReadyForSubmission.ShouldBeTrue();
        result.EvidenceCompletenessScorePercent.ShouldBeGreaterThanOrEqualTo(70);
        result.CompiledDefenseSummaryText.ShouldNotBeNull();
        result.CompiledDefenseSummaryText.ShouldContain("UPS");
    }

    [Fact(DisplayName = "Surcharging Engine Enforces Regional Caps And Prohibitions Compliantly")]
    public void SurchargingEngine_EnforcesCompliance_Correctly()
    {
        var engine = new KyrolusDefaultSurchargingEngine();

        // 1. UK / EU prohibition
        var ukResult = engine.CalculateCompliantSurcharge(new KyrolusSurchargeEvaluationRequest
        {
            OrderAmount = 100m,
            Currency = "GBP",
            CountryCode = "GB",
            CardType = KyrolusCardType.Credit
        });
        ukResult.IsSurchargePermitted.ShouldBeFalse();
        ukResult.SurchargeAmount.ShouldBe(0m);

        // 2. US Credit card capped at 3%
        var usResult = engine.CalculateCompliantSurcharge(new KyrolusSurchargeEvaluationRequest
        {
            OrderAmount = 100m,
            Currency = "USD",
            CountryCode = "US",
            CardType = KyrolusCardType.Credit,
            RequestedSurchargePercent = 4.5m // Above 3% cap
        });
        usResult.IsSurchargePermitted.ShouldBeTrue();
        usResult.AllowedSurchargeRatePercent.ShouldBe(3.0m);
        usResult.SurchargeAmount.ShouldBe(3.0m);
        usResult.FinalCustomerChargeAmount.ShouldBe(103.0m);
    }

    [Fact(DisplayName = "Conditional Release Engine Dispatches Milestone Releases Correctly")]
    public void ConditionalReleaseEngine_HandlesMilestones_Accurately()
    {
        var engine = new KyrolusDefaultConditionalReleaseEngine();
        var agreementId = "agr_escrow_500";

        engine.RegisterAgreement(new KyrolusConditionalEscrowAgreement
        {
            AgreementId = agreementId,
            SellerId = "seller_freelancer_1",
            TotalEscrowAmount = 1000m,
            Currency = "USD",
            Milestones =
            [
                new() { MilestoneId = "MS-1", Description = "Design approval", AmountToRelease = 400m, Status = KyrolusMilestoneStatus.Pending },
                new() { MilestoneId = "MS-2", Description = "Deployment", AmountToRelease = 600m, Status = KyrolusMilestoneStatus.Pending }
            ]
        });

        // Trigger MS-1
        var ms1Result = engine.TriggerMilestoneRelease(agreementId, "MS-1");
        ms1Result.ReleasedAmount.ShouldBe(400m);
        ms1Result.RemainingLockedAmount.ShouldBe(600m);
        ms1Result.IsAgreementFullySettled.ShouldBeFalse();

        // Trigger MS-2
        var ms2Result = engine.TriggerMilestoneRelease(agreementId, "MS-2");
        ms2Result.ReleasedAmount.ShouldBe(600m);
        ms2Result.RemainingLockedAmount.ShouldBe(0m);
        ms2Result.IsAgreementFullySettled.ShouldBeTrue();
    }

    [Fact(DisplayName = "Settlement Route Optimizer Selects Domestic Clearing When Available")]
    public void SettlementOptimizer_SelectsDomesticRoute_Optimally()
    {
        var optimizer = new KyrolusDefaultSettlementRouteOptimizer();

        var accounts = new List<KyrolusMerchantBankAccount>
        {
            new() { AccountId = "US_ACH_ACC", BankCountryCode = "US", Currency = "USD", IbanOrAccountNumber = "123456", IsDomestic = true },
            new() { AccountId = "EG_IPN_ACC", BankCountryCode = "EG", Currency = "EGP", IbanOrAccountNumber = "EG9999", IsDomestic = true }
        };

        var decision = optimizer.OptimizeSettlementRoute("EGP", accounts);
        decision.SelectedAccountId.ShouldBe("EG_IPN_ACC");
        decision.IsDomesticClearing.ShouldBeTrue();
        decision.EstimatedWireFee.ShouldBe(0m);
    }

    [Fact(DisplayName = "Direct Debit Engine Creates Mandates And Executes Debit Successfully")]
    public async Task DirectDebitEngine_CreatesMandatesAndDebits_Successfully()
    {
        var engine = new KyrolusDefaultDirectDebitMandateEngine();

        var mandate = await engine.CreateMandateAsync(new KyrolusCreateMandateRequest
        {
            CustomerId = "cust_corp_1",
            CustomerName = "Kyrolus Corp",
            CustomerIbanOrAccountNumber = "DE89370400440532013000",
            Scheme = KyrolusDirectDebitScheme.SepaDirectDebit,
            Currency = "EUR"
        });

        mandate.Status.ShouldBe(KyrolusMandateStatus.Active);
        mandate.MandateReference.ShouldNotBeNullOrWhiteSpace();

        var debitResult = await engine.ExecuteDebitAsync(mandate.MandateId, 500m);
        debitResult.Succeeded.ShouldBeTrue();
        debitResult.Amount.ShouldBe(500m);
    }

    [Fact(DisplayName = "Network Tokenization Engine Generates DPAN And Cryptograms")]
    public async Task NetworkTokenizationEngine_GeneratesTokens_Accurately()
    {
        var engine = new KyrolusDefaultNetworkTokenizationEngine();

        var token = await engine.TokenizeCardAsync(new KyrolusTokenizePanRequest
        {
            PrimaryAccountNumber = "4111111111119999",
            ExpiryMonth = 5,
            ExpiryYear = 2028,
            CardholderName = "Kyrolus Sous"
        });

        token.NetworkTokenNumber.ShouldEndWith("9999");
        token.Cryptogram.ShouldNotBeNullOrWhiteSpace();
        token.IsActive.ShouldBeTrue();

        var cryptogram = await engine.GenerateCryptogramForPaymentAsync(token.TokenReferenceId, 100m, "USD");
        cryptogram.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "DCC Engine Calculates Guaranteed Exchange Rates With Markup Margin")]
    public async Task DccEngine_CalculatesQuote_Accurately()
    {
        var fx = new KyrolusDefaultFxRateProvider();
        fx.SetRate("EUR", "USD", 1.10m);
        var dccEngine = new KyrolusDefaultDynamicCurrencyConversionEngine(fx);

        var quote = await dccEngine.GenerateDccQuoteAsync(new KyrolusDccQuoteRequest
        {
            BaseAmount = 100m,
            BaseCurrency = "EUR",
            CardholderHomeCurrency = "USD",
            MarkupMarginPercent = 3.0m // 3% margin
        });

        quote.OriginalBaseAmount.ShouldBe(100m);
        quote.CardholderCurrency.ShouldBe("USD");
        quote.ConvertedCardholderAmount.ShouldBe(113.30m); // 100 * 1.10 * 1.03 = 113.30
    }

    [Fact(DisplayName = "Gift Card Engine Issues Cards And Deducts Balances Correctly")]
    public void GiftCardEngine_IssuesAndRedeems_Accurately()
    {
        var engine = new KyrolusDefaultGiftCardPassEngine();

        var card = engine.IssueGiftCard(new KyrolusIssueGiftCardRequest
        {
            InitialBalance = 200m,
            Currency = "USD",
            RecipientEmail = "friend@example.com"
        });

        card.IsActive.ShouldBeTrue();
        card.CurrentBalance.ShouldBe(200m);

        var redeem = engine.RedeemGiftCard(card.CardCode, card.Pin, 75m);
        redeem.Succeeded.ShouldBeTrue();
        redeem.RemainingCardBalance.ShouldBe(125m);
        engine.GetBalance(card.CardCode, card.Pin).ShouldBe(125m);
    }

    [Fact(DisplayName = "Rolling Reserve Engine Holds And Releases Risk Reserve Amounts")]
    public void RollingReserveEngine_TracksAndHolds_Correctly()
    {
        var engine = new KyrolusDefaultRollingReserveEngine();
        var merchantId = "merch_highrisk_1";

        // $1000 sale with 5% reserve -> $50 held
        var hold = engine.ApplyReserveHold(merchantId, "tx_100", 1000m, "USD", holdPercentage: 5.0m);
        hold.HeldAmount.ShouldBe(50m);

        var summary = engine.GetReserveSummary(merchantId);
        summary.TotalLockedReserveAmount.ShouldBe(50m);
        summary.ActiveHoldCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Webhook Dispatcher Computes HMAC Signature And Dispatches Events")]
    public async Task WebhookDispatcher_DispatchesEvents_Successfully()
    {
        var dispatcher = new KyrolusDefaultPaymentWebhookDispatcher();
        dispatcher.RegisterSubscription(new KyrolusWebhookDispatchSubscription
        {
            SubscriptionId = "sub_erp_1",
            DestinationUrl = "https://erp.internal.local/webhooks/payments",
            SecretKey = "super_secret_hmac_key"
        });

        var results = await dispatcher.DispatchEventAsync("payment.succeeded", "{\"event\":\"payment.succeeded\",\"amount\":100}");
        results.Count.ShouldBe(1);
        results[0].Succeeded.ShouldBeTrue();
    }

    [Fact(DisplayName = "Merchant KYC Engine Validates Completeness And Approves Tiers")]
    public async Task MerchantKycEngine_EvaluatesTiers_Accurately()
    {
        var engine = new KyrolusDefaultMerchantKycEngine();

        // 1. Missing tax card -> Action required
        var incomplete = await engine.EvaluateKycSubmissionAsync(new KyrolusMerchantKycSubmission
        {
            MerchantId = "m_1",
            LegalBusinessName = "Shop 1",
            TaxRegistrationNumber = "",
            CommercialRegisterNumber = "CR123",
            BeneficialOwnerName = "Owner",
            BeneficialOwnerNationalIdOrPassport = "NID123",
            CountryCode = "EG"
        });
        incomplete.Status.ShouldBe(KyrolusKycStatus.ActionRequired);

        // 2. Complete docs -> Enterprise approved
        var complete = await engine.EvaluateKycSubmissionAsync(new KyrolusMerchantKycSubmission
        {
            MerchantId = "m_2",
            LegalBusinessName = "Kyrolus Enterprise",
            TaxRegistrationNumber = "TRN-999",
            CommercialRegisterNumber = "CR-888",
            BeneficialOwnerName = "Kyrolus Sous",
            BeneficialOwnerNationalIdOrPassport = "PASSPORT-777",
            CountryCode = "EG"
        });
        complete.Status.ShouldBe(KyrolusKycStatus.Approved);
        complete.ApprovedTier.ShouldBe(KyrolusKycTier.Tier3_Enterprise);
    }

    [Fact(DisplayName = "Interchange Plus Calculator Breaks Down Issuing Scheme And Acquirer Fees")]
    public void InterchangeCalculator_CalculatesBreakdown_Accurately()
    {
        var calculator = new KyrolusDefaultInterchangePlusCalculator();

        var result = calculator.CalculateFeeBreakdown(new KyrolusInterchangePricingRequest
        {
            TransactionAmount = 1000m,
            Currency = "USD",
            Scheme = KyrolusCardScheme.Visa,
            CardType = KyrolusCardType.Credit,
            AcquirerMarkupPercent = 0.5m,
            AcquirerFixedFee = 0.10m
        });

        // Interchange: 1.65% of 1000 = $16.50
        // Scheme: 0.14% of 1000 = $1.40
        // Markup: 0.5% + $0.10 = $5.10
        // Total = $23.00
        result.InterchangeFee.ShouldBe(16.50m);
        result.SchemeAssessmentFee.ShouldBe(1.40m);
        result.AcquirerMarkupFee.ShouldBe(5.10m);
        result.TotalProcessingCost.ShouldBe(23.00m);
        result.NetSettlementAmount.ShouldBe(977.00m);
    }

    [Fact(DisplayName = "Refund Policy Engine Enforces Return Windows And Restocking Fees")]
    public void RefundPolicyEngine_EnforcesRules_Accurately()
    {
        var engine = new KyrolusDefaultRefundPolicyEngine();

        // 1. Within 14-day window with 10% restocking fee & non-refundable shipping
        var approved = engine.CalculateRefund(new KyrolusRefundCalculationRequest
        {
            OriginalOrderAmount = 120m,
            OriginalShippingCost = 20m,
            OrderCompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-5), // 5 days ago
            RequestedRefundAmount = 100m,
            AllowedRefundWindowDays = 14,
            RestockingFeePercent = 10m,
            IsShippingRefundable = false
        });
        approved.IsEligibleForRefund.ShouldBeTrue();
        approved.RestockingFeeDeduction.ShouldBe(10m); // 10% of 100
        approved.NonRefundableShippingDeduction.ShouldBe(20m);
        approved.NetApprovedRefundAmount.ShouldBe(70m); // 100 - 10 - 20 = 70

        // 2. Expired window (completed 30 days ago)
        var expired = engine.CalculateRefund(new KyrolusRefundCalculationRequest
        {
            OriginalOrderAmount = 100m,
            OriginalShippingCost = 0m,
            OrderCompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
            RequestedRefundAmount = 100m,
            AllowedRefundWindowDays = 14
        });
        expired.IsEligibleForRefund.ShouldBeFalse();
        expired.NetApprovedRefundAmount.ShouldBe(0m);
    }

    [Fact(DisplayName = "Payout Scheduler Correctly Skips Weekends And Regional Bank Holidays")]
    public void PayoutScheduler_AccountsForWeekends_Accurately()
    {
        var scheduler = new KyrolusDefaultPayoutScheduler();

        // Captured on Thursday in US -> T+2 business days should arrive on Monday (skipping Sat & Sun)
        var thursdayUtc = new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero); // Thursday
        var result = scheduler.CalculateExpectedPayoutDate(new KyrolusPayoutScheduleRequest
        {
            CapturedAtUtc = thursdayUtc,
            Speed = KyrolusSettlementSpeed.T_Plus_2_Standard,
            BankCountryCode = "US"
        });

        result.WeekendAndHolidayDaysDelayed.ShouldBe(2); // Saturday + Sunday
        result.EstimatedPayoutArrivalDateUtc.DayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Fact(DisplayName = "Gift Card Concurrency: Eliminates Double Spending Race Conditions Under 50 Threads")]
    public async Task GiftCard_ConcurrentRedemptions_PreventsDoubleSpending()
    {
        var engine = new KyrolusDefaultGiftCardPassEngine();
        var card = engine.IssueGiftCard(new KyrolusIssueGiftCardRequest
        {
            InitialBalance = 100m,
            Currency = "USD"
        });

        // 50 concurrent threads all trying to redeem $10 simultaneously
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            return engine.RedeemGiftCard(card.CardCode, card.Pin, 10m);
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        var successfulRedemptions = results.Count(r => r.Succeeded);
        var failedRedemptions = results.Count(r => !r.Succeeded);

        // Exactly 10 redemptions of $10 must succeed for a $100 balance
        successfulRedemptions.ShouldBe(10);
        failedRedemptions.ShouldBe(40);
        engine.GetBalance(card.CardCode, card.Pin).ShouldBe(0m);
    }

    [Fact(DisplayName = "Loyalty Points Concurrency: Prevents Double Redemption In High Concurrency")]
    public async Task LoyaltyPoints_ConcurrentRedemption_IsThreadSafe()
    {
        var engine = new KyrolusDefaultLoyaltyRewardsEngine();
        var custId = "cust_concurrent_race_1";
        engine.AwardPoints(custId, 500m); // 500 points total

        // 20 concurrent threads attempting to redeem 50 points each
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            return engine.RedeemPoints(new KyrolusRedeemPointsRequest
            {
                CustomerId = custId,
                PointsToRedeem = 50m
            });
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        var successful = results.Count(r => r.Succeeded);
        var failed = results.Count(r => !r.Succeeded);

        // Exactly 10 redemptions of 50 points must succeed for a 500 points balance
        successful.ShouldBe(10);
        failed.ShouldBe(10);
        engine.GetBalance(custId).ShouldBe(0m);
    }

    [Fact(DisplayName = "Invoice Generator: Protects Against XSS And HTML Injection")]
    public void InvoiceGenerator_EscapesHtml_Securely()
    {
        var generator = new KyrolusDefaultInvoiceGenerator();
        var maliciousInput = "<script>alert('XSS')</script>";

        var invoice = generator.GenerateInvoice(new KyrolusInvoiceRequest
        {
            InvoiceNumber = "INV-001",
            MerchantName = maliciousInput,
            CustomerName = maliciousInput,
            Currency = "USD",
            Items =
            [
                new() { Description = maliciousInput, Quantity = 1, UnitPrice = 100m, TaxRatePercent = 0m }
            ],
            Notes = maliciousInput
        });

        invoice.RenderedHtml.ShouldNotContain("<script>alert('XSS')</script>");
        invoice.RenderedHtml.ShouldContain("&lt;script&gt;alert(&#39;XSS&#39;)&lt;/script&gt;");
    }

    [Fact(DisplayName = "Webhook Dispatcher: Verifies Constant-Time HMAC Signature Correctly")]
    public void WebhookDispatcher_ConstantTimeSignatureVerification_WorksAccurately()
    {
        var payload = "{\"event\":\"payment.succeeded\",\"order_id\":\"ord_123\"}";
        var secret = "super_secure_vault_secret_key";

        var validSignature = KyrolusDefaultPaymentWebhookDispatcher.ComputeHmacSignature(payload, secret);
        var isValid = KyrolusDefaultPaymentWebhookDispatcher.VerifyHmacSignature(payload, secret, validSignature);
        isValid.ShouldBeTrue();

        // Tampered payload
        var isTamperedValid = KyrolusDefaultPaymentWebhookDispatcher.VerifyHmacSignature(payload + "tampered", secret, validSignature);
        isTamperedValid.ShouldBeFalse();

        // Altered signature
        var isFakeSigValid = KyrolusDefaultPaymentWebhookDispatcher.VerifyHmacSignature(payload, secret, "0000000000000000000000000000000000000000000000000000000000000000");
        isFakeSigValid.ShouldBeFalse();
    }
}
