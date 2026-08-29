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
}
