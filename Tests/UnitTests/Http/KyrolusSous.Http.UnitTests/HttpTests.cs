using System.Net;
using KyrolusSous.Http.Abstractions;
using KyrolusSous.Http.Core;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Http.UnitTests;

public sealed class HttpTests
{
    private sealed class TestInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact(DisplayName = "Auth Delegating Handler Attaches Bearer Token Correctly")]
    public async Task AuthHandler_AttachesBearerToken_Correctly()
    {
        var tokenPropagator = Substitute.For<IKyrolusTokenPropagator>();
        tokenPropagator.GetTokenAsync().Returns(ValueTask.FromResult<string?>("jwt-test-token-xyz"));

        var inner = new TestInnerHandler();
        var handler = new KyrolusAuthDelegatingHandler(tokenPropagator)
        {
            InnerHandler = inner
        };

        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kyrolus.local/orders");

        await invoker.SendAsync(request, CancellationToken.None);

        inner.LastRequest.ShouldNotBeNull();
        inner.LastRequest.Headers.Authorization.ShouldNotBeNull();
        inner.LastRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        inner.LastRequest.Headers.Authorization.Parameter.ShouldBe("jwt-test-token-xyz");
    }

    [Fact(DisplayName = "Correlation Delegating Handler Injects Correlation Id Header")]
    public async Task CorrelationHandler_InjectsCorrelationId_Header()
    {
        var correlationPropagator = Substitute.For<IKyrolusCorrelationPropagator>();
        correlationPropagator.GetCorrelationId().Returns("corr-987654321");

        var inner = new TestInnerHandler();
        var handler = new KyrolusCorrelationDelegatingHandler(correlationPropagator)
        {
            InnerHandler = inner
        };

        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.kyrolus.local/payments");

        await invoker.SendAsync(request, CancellationToken.None);

        inner.LastRequest.ShouldNotBeNull();
        inner.LastRequest.Headers.Contains("X-Correlation-ID").ShouldBeTrue();
        inner.LastRequest.Headers.GetValues("X-Correlation-ID").First().ShouldBe("corr-987654321");
    }

    [Fact(DisplayName = "Hmac Signer Generates And Verifies Signatures Accurately")]
    public void HmacSigner_GeneratesAndVerifies_Accurately()
    {
        var signer = new KyrolusHmacSigner();
        var secret = "super-secret-enterprise-key-12345";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var body = System.Text.Encoding.UTF8.GetBytes("{\"amount\": 100}");

        var signature = signer.ComputeSignature(secret, timestamp, "POST", "/api/v1/payments", body);
        signature.ShouldNotBeNullOrWhiteSpace();

        var verified = signer.VerifySignature(secret, signature, timestamp, "POST", "/api/v1/payments", body);
        verified.ShouldBeTrue();

        var forged = signer.VerifySignature(secret, "forged-signature-abc", timestamp, "POST", "/api/v1/payments", body);
        forged.ShouldBeFalse();
    }
}
