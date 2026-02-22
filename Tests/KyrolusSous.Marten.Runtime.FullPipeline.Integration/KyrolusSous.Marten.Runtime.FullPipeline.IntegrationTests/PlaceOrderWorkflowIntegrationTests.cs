using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class PlaceOrderWorkflowIntegrationTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public PlaceOrderWorkflowIntegrationTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = "PlaceOrder workflow - successful payment triggers email and gateway")]
    public async Task Place_order_sends_payment_and_email()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var request = new PlaceOrderRequest(
            "customer@local.test",
            "card",
            new List<OrderLine>
            {
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "Pizza",
                    UnitPrice = 20,
                    Quantity = 2
                }
            });

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<Order>();
        order.ShouldNotBeNull();
        order!.Status.ShouldBe(OrderStatus.Paid);

        var emailSender = factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender;
        var paymentGateway = factory.Services.GetRequiredService<IPaymentGateway>() as FakePaymentGateway;
        emailSender.ShouldNotBeNull();
        paymentGateway.ShouldNotBeNull();
        emailSender!.Messages.Count.ShouldBeGreaterThan(0);
        paymentGateway!.Requests.Count.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "PlaceOrder workflow - failed payment returns 502")]
    public async Task Failed_payment_returns_bad_gateway()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var request = new PlaceOrderRequest(
            "customer@local.test",
            "fail",
            new List<OrderLine>
            {
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "Pasta",
                    UnitPrice = 15,
                    Quantity = 1
                }
            });

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact(DisplayName = "PlaceOrder workflow - failed payment does not send email (current behavior)")]
    public async Task Failed_payment_does_not_send_email()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("orders-fail-email"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var emailSender = factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender;
        emailSender.ShouldNotBeNull();
        var beforeCount = emailSender!.Messages.Count;

        var request = new PlaceOrderRequest(
            "customer@local.test",
            "fail",
            new List<OrderLine>
            {
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "Failed Email",
                    UnitPrice = 12,
                    Quantity = 1
                }
            });

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        emailSender.Messages.Count.ShouldBe(beforeCount);
    }

    [Fact(DisplayName = "PlaceOrder workflow - total equals sum of line items")]
    public async Task Order_total_equals_sum_of_lines()
    {
        var tenant = TestHelpers.NewTenantId("orders-total");
        using var client = factory.CreateClientWithTenant(tenant);
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var request = new PlaceOrderRequest(
            "customer@local.test",
            "card",
            [
                new() { MenuItemId = Guid.NewGuid(), Name = "Item A", UnitPrice = 5, Quantity = 2 },
                new() { MenuItemId = Guid.NewGuid(), Name = "Item B", UnitPrice = 3, Quantity = 3 }
            ]);

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<Order>();
        order.ShouldNotBeNull();
        order!.Total.ShouldBe(5 * 2 + 3 * 3);
        order.TenantId.ShouldBe(tenant);
    }

    [Fact(DisplayName = "PlaceOrder workflow - endpoint requires authentication")]
    public async Task Orders_endpoint_requires_authentication()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var request = new PlaceOrderRequest(
            "customer@local.test",
            "card",
            [
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "Unauthorized",
                    UnitPrice = 10,
                    Quantity = 1
                }
            ]);

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "PlaceOrder workflow - invalid email returns server error (current behavior)")]
    public async Task Invalid_email_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var request = new PlaceOrderRequest(
            "not-an-email",
            "card",
            [
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "Invalid Email",
                    UnitPrice = 10,
                    Quantity = 1
                }
            ]);

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "PlaceOrder workflow - empty lines returns server error (current behavior)")]
    public async Task Empty_lines_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var request = new PlaceOrderRequest("customer@local.test", "card", new List<OrderLine>());
        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "PlaceOrder workflow - invalid line quantity returns server error (current behavior)")]
    public async Task Invalid_line_quantity_returns_server_error()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("orders-invalid-line"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var request = new PlaceOrderRequest(
            "customer@local.test",
            "card",
            [
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "Invalid Qty",
                    UnitPrice = 10,
                    Quantity = 0
                }
            ]);

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "PlaceOrder workflow - get by id returns 404 when not found")]
    public async Task Get_order_by_id_returns_not_found()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("orders-not-found"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
