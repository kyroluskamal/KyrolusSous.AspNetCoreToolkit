using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class RepositoryRuntimeDiagnosticsIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "Marten repository runtime diagnostics - mode matrix returns expected status")]
    [MemberData(nameof(ModeStatusCases))]
    public async Task Repository_runtime_diagnostics_mode_matrix_returns_expected_status(string mode, HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("repository-runtime-mode-status"));

        var response = await client.PostAsJsonAsync(
            "/api/menu-items/diagnostics/repository-runtime",
            new RepositoryRuntimeDiagnosticsRequest(mode));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Theory(DisplayName = "Marten repository runtime diagnostics - successful modes return consistent contracts")]
    [MemberData(nameof(SuccessfulModeCases))]
    public async Task Repository_runtime_diagnostics_successful_modes_return_consistent_contracts(string mode)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("repository-runtime-success"));

        var response = await client.PostAsJsonAsync(
            "/api/menu-items/diagnostics/repository-runtime",
            new RepositoryRuntimeDiagnosticsRequest(mode));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RepositoryRuntimeDiagnosticsResponse>();
        payload.ShouldNotBeNull();
        payload!.Mode.ShouldBe(mode, body);

        if (mode == "order-includes-runtime")
        {
            payload.AllFirstCount.ShouldNotBeNull();
            payload.AllFirstCount!.Value.ShouldBeGreaterThanOrEqualTo(2);
            payload.QueryCount.ShouldNotBeNull();
            payload.QueryCount!.Value.ShouldBeGreaterThanOrEqualTo(2);
            payload.QueryPageCount.ShouldNotBeNull();
            payload.QueryPageCount!.Value.ShouldBeGreaterThan(0);
            payload.PageCount.ShouldNotBeNull();
            payload.PageCount!.Value.ShouldBeGreaterThan(0);
            payload.StreamCount.ShouldNotBeNull();
            payload.StreamCount!.Value.ShouldBeGreaterThanOrEqualTo(2);
            payload.IncludedPayment.ShouldNotBeNull();
            payload.IncludedPaymentsCount.ShouldNotBeNull();
            payload.IncludedPaymentsCount!.Value.ShouldBeGreaterThanOrEqualTo(0);
            payload.IncludedPaymentArrayCount.ShouldNotBeNull();
            payload.IncludedPaymentArrayCount!.Value.ShouldBeGreaterThanOrEqualTo(0);
            payload.IncludedPaymentSetCount.ShouldNotBeNull();
            payload.IncludedPaymentSetCount!.Value.ShouldBeGreaterThanOrEqualTo(0);
            payload.NullIncludeHandled.ShouldBe(true, body);
            return;
        }

        if (mode == "menu-soft-delete-runtime")
        {
            payload.SoftDeleteActiveCountBefore.ShouldBe(2, body);
            payload.SoftDeleteIncludingDeletedCountBefore.ShouldBe(3, body);
            payload.SoftDeleteDeletedOnlyCountBefore.ShouldBe(1, body);
            payload.SoftDeleteByIdDeletedFilteredOut.ShouldBe(true, body);
            payload.SoftDeleteByIdIncludingDeletedFound.ShouldBe(true, body);
            payload.RemovedEntity.ShouldBe(true, body);
            payload.RemovedById.ShouldBe(true, body);
            payload.SoftDeleteDeleteWhereResult.ShouldBe(0, body);
            payload.SoftDeleteRestoreById.ShouldBe(true, body);
            payload.SoftDeleteRestoreRange.ShouldBe(true, body);
            payload.SoftDeleteRestoreWhereResult.ShouldBe(0, body);
            payload.SoftDeleteActiveCountAfterRestore.ShouldNotBeNull();
            payload.SoftDeleteActiveCountAfterRestore!.Value.ShouldBeGreaterThanOrEqualTo(2);
            payload.SoftDeleteDisabledPolicyReturnsEmpty.ShouldBe(true, body);
            payload.SoftDeleteInvalidPolicyReturnsEmpty.ShouldBe(true, body);
            return;
        }

        if (mode == "abstractions-runtime")
        {
            payload.AuthorizationChecks.ShouldNotBeNull();
            payload.AuthorizationChecks!.Value.ShouldBeGreaterThan(10);
            payload.ValidationChecks.ShouldNotBeNull();
            payload.ValidationChecks!.Value.ShouldBeGreaterThan(10);
            payload.TracingChecks.ShouldNotBeNull();
            payload.TracingChecks!.Value.ShouldBeGreaterThan(5);
            payload.ObserverChecks.ShouldNotBeNull();
            payload.ObserverChecks!.Value.ShouldBeGreaterThan(5);
            payload.ResilienceChecks.ShouldNotBeNull();
            payload.ResilienceChecks!.Value.ShouldBeGreaterThan(5);
            payload.SpecificationChecks.ShouldNotBeNull();
            payload.SpecificationChecks!.Value.ShouldBeGreaterThan(10);
            payload.QueryPrimitiveChecks.ShouldNotBeNull();
            payload.QueryPrimitiveChecks!.Value.ShouldBeGreaterThan(5);
            payload.DbProbeCount.ShouldNotBeNull();
            payload.DbProbeCount!.Value.ShouldBeGreaterThanOrEqualTo(1);
            return;
        }

        if (mode == "runtime-infrastructure")
        {
            payload.SagaChecks.ShouldNotBeNull();
            payload.SagaChecks!.Value.ShouldBeGreaterThan(5);
            payload.EventStoreChecks.ShouldNotBeNull();
            payload.EventStoreChecks!.Value.ShouldBeGreaterThanOrEqualTo(0);
            payload.ProjectionManagerChecks.ShouldNotBeNull();
            payload.ProjectionManagerChecks!.Value.ShouldBeGreaterThan(10);
            payload.ProjectionOrchestratorChecks.ShouldNotBeNull();
            payload.ProjectionOrchestratorChecks!.Value.ShouldBeGreaterThan(10);
            payload.RuntimeRegistrationChecks.ShouldNotBeNull();
            payload.RuntimeRegistrationChecks!.Value.ShouldBeGreaterThan(10);
            payload.DbProbeCount.ShouldNotBeNull();
            payload.DbProbeCount!.Value.ShouldBeGreaterThanOrEqualTo(1);
            return;
        }

        if (mode == "cqrs-handlers-runtime")
        {
            payload.CqrsHandlerChecks.ShouldNotBeNull();
            payload.CqrsHandlerChecks!.Value.ShouldBeGreaterThan(10);
            payload.DbProbeCount.ShouldNotBeNull();
            payload.DbProbeCount!.Value.ShouldBeGreaterThanOrEqualTo(1);
            return;
        }

        if (mode == "endpointkit-core-runtime")
        {
            payload.EndpointKitCoreChecks.ShouldNotBeNull();
            payload.EndpointKitCoreChecks!.Value.ShouldBeGreaterThan(10);
            return;
        }

        if (mode == "endpointkit-marten-runtime")
        {
            payload.EndpointKitMartenChecks.ShouldNotBeNull();
            payload.EndpointKitMartenChecks!.Value.ShouldBeGreaterThan(20);
            return;
        }

        if (mode == "validation-runtime")
        {
            payload.ValidationRuntimeChecks.ShouldNotBeNull();
            payload.ValidationRuntimeChecks!.Value.ShouldBeGreaterThan(20);
            return;
        }

        if (mode == "exception-handling-runtime")
        {
            payload.ExceptionHandlingChecks.ShouldNotBeNull();
            payload.ExceptionHandlingChecks!.Value.ShouldBeGreaterThan(10);
            return;
        }

        if (mode == "cache-abstractions-runtime")
        {
            payload.CacheAbstractionsChecks.ShouldNotBeNull();
            payload.CacheAbstractionsChecks!.Value.ShouldBeGreaterThan(15);
            return;
        }

        if (mode == "data-protection-runtime")
        {
            payload.DataProtectionChecks.ShouldNotBeNull();
            payload.DataProtectionChecks!.Value.ShouldBeGreaterThan(40);
            return;
        }

        if (mode == "redis-cache-runtime")
        {
            payload.RedisCacheChecks.ShouldNotBeNull();
            payload.RedisCacheChecks!.Value.ShouldBeGreaterThan(30);
            return;
        }

        if (mode == "redis-fallback-runtime")
        {
            payload.RedisFallbackChecks.ShouldNotBeNull();
            payload.RedisFallbackChecks!.Value.ShouldBeGreaterThan(20);
            return;
        }

        if (mode == "data-protection-redis-runtime")
        {
            payload.DataProtectionRedisChecks.ShouldNotBeNull();
            payload.DataProtectionRedisChecks!.Value.ShouldBeGreaterThan(10);
            return;
        }

        if (mode == "exception-abstractions-runtime")
        {
            payload.ExceptionAbstractionsChecks.ShouldNotBeNull();
            payload.ExceptionAbstractionsChecks!.Value.ShouldBeGreaterThan(15);
            return;
        }

        if (mode == "mediator-runtime")
        {
            payload.MediatorChecks.ShouldNotBeNull();
            payload.MediatorChecks!.Value.ShouldBeGreaterThan(20);
            return;
        }

        if (mode == "logging-runtime")
        {
            payload.LoggingChecks.ShouldNotBeNull();
            payload.LoggingChecks!.Value.ShouldBeGreaterThan(15);
            return;
        }

        payload.AllFirstCount.ShouldNotBeNull();
        payload.AllFirstCount!.Value.ShouldBeGreaterThanOrEqualTo(2);
        payload.AllSecondCount.ShouldNotBeNull();
        payload.AllSecondCount!.Value.ShouldBeGreaterThanOrEqualTo(payload.AllFirstCount.Value);
        payload.ByIdFirstFound.ShouldBe(true, body);
        payload.ByIdSecondFound.ShouldBe(true, body);
        payload.CrossTenantCount.ShouldNotBeNull();
        payload.CrossTenantCount!.Value.ShouldBeGreaterThanOrEqualTo(0);
        payload.ExistsAny.ShouldBe(true, body);
        payload.StreamCount.ShouldNotBeNull();
        payload.StreamCount!.Value.ShouldBeGreaterThanOrEqualTo(2);
        payload.QueryCount.ShouldNotBeNull();
        payload.QueryCount!.Value.ShouldBeGreaterThanOrEqualTo(2);
        payload.QueryPageCount.ShouldNotBeNull();
        payload.QueryPageCount!.Value.ShouldBeGreaterThan(0);
        payload.PageCount.ShouldNotBeNull();
        payload.PageCount!.Value.ShouldBeGreaterThan(0);
        payload.CompiledCountFirst.ShouldNotBeNull();
        payload.CompiledCountSecond.ShouldNotBeNull();
        payload.CompiledCountFirst!.Value.ShouldBe(payload.CompiledCountSecond!.Value, body);
        payload.WithSessionValue.ShouldBe(7, body);
        payload.TransformResult.ShouldBe(0, body);
        payload.RemovedEntity.ShouldBe(true, body);
        payload.RemovedById.ShouldBe(true, body);
        payload.RemovedRange.ShouldNotBeNull();
        payload.PatchResultFound.ShouldBe(true, body);
        payload.ResolvedFromResolver.ShouldNotBeNull();
        payload.ResolvedFromResolver!.Contains("-resolver").ShouldBeTrue(body);
        payload.ResolvedFromNullResolver.ShouldBeNull(body);
    }

    [Fact(DisplayName = "Marten repository runtime diagnostics - null body falls back to default mode")]
    public async Task Repository_runtime_diagnostics_null_body_falls_back_to_default_mode()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("repository-runtime-null-body"));

        var response = await client.PostAsJsonAsync(
            "/api/menu-items/diagnostics/repository-runtime",
            (RepositoryRuntimeDiagnosticsRequest?)null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RepositoryRuntimeDiagnosticsResponse>();
        payload.ShouldNotBeNull();
        payload!.Mode.ShouldBe("menu-runtime", body);
    }

    [Theory(DisplayName = "Marten repository runtime diagnostics - tenant isolation stays intact across modes")]
    [MemberData(nameof(TenantIsolationCases))]
    public async Task Repository_runtime_diagnostics_tenant_isolation_stays_intact_across_modes(string mode)
    {
        using var tenantAClient = factory.CreateClientWithTenant(TestHelpers.NewTenantId("repository-runtime-tenant-a"));
        using var tenantBClient = factory.CreateClientWithTenant(TestHelpers.NewTenantId("repository-runtime-tenant-b"));

        var tenantAResponse = await tenantAClient.PostAsJsonAsync(
            "/api/menu-items/diagnostics/repository-runtime",
            new RepositoryRuntimeDiagnosticsRequest(mode));
        var tenantABody = await tenantAResponse.Content.ReadAsStringAsync();
        tenantAResponse.StatusCode.ShouldBe(HttpStatusCode.OK, tenantABody);

        var tenantBResponse = await tenantBClient.PostAsJsonAsync(
            "/api/menu-items/diagnostics/repository-runtime",
            new RepositoryRuntimeDiagnosticsRequest(mode));
        var tenantBBody = await tenantBResponse.Content.ReadAsStringAsync();
        tenantBResponse.StatusCode.ShouldBe(HttpStatusCode.OK, tenantBBody);

        var tenantAPayload = await tenantAResponse.Content.ReadFromJsonAsync<RepositoryRuntimeDiagnosticsResponse>();
        var tenantBPayload = await tenantBResponse.Content.ReadFromJsonAsync<RepositoryRuntimeDiagnosticsResponse>();
        tenantAPayload.ShouldNotBeNull();
        tenantBPayload.ShouldNotBeNull();

        if (mode is not ("order-includes-runtime" or "abstractions-runtime" or "runtime-infrastructure" or "cqrs-handlers-runtime" or "endpointkit-core-runtime" or "endpointkit-marten-runtime" or "validation-runtime" or "exception-handling-runtime" or "data-protection-runtime" or "redis-cache-runtime" or "redis-fallback-runtime" or "data-protection-redis-runtime" or "exception-abstractions-runtime" or "mediator-runtime" or "logging-runtime"))
        {
            tenantAPayload!.CrossTenantCount.ShouldNotBeNull();
            tenantBPayload!.CrossTenantCount.ShouldNotBeNull();
        }
    }

    public static IEnumerable<object[]> ModeStatusCases()
    {
        yield return ["menu-runtime", HttpStatusCode.OK];
        yield return ["menu-runtime-cache-disabled", HttpStatusCode.OK];
        yield return ["menu-runtime-no-cache-provider", HttpStatusCode.OK];
        yield return ["menu-soft-delete-runtime", HttpStatusCode.OK];
        yield return ["order-includes-runtime", HttpStatusCode.OK];
        yield return ["abstractions-runtime", HttpStatusCode.OK];
        yield return ["runtime-infrastructure", HttpStatusCode.OK];
        yield return ["cqrs-handlers-runtime", HttpStatusCode.OK];
        yield return ["endpointkit-core-runtime", HttpStatusCode.OK];
        yield return ["endpointkit-marten-runtime", HttpStatusCode.OK];
        yield return ["validation-runtime", HttpStatusCode.OK];
        yield return ["exception-handling-runtime", HttpStatusCode.OK];
        yield return ["cache-abstractions-runtime", HttpStatusCode.OK];
        yield return ["data-protection-runtime", HttpStatusCode.OK];
        yield return ["redis-cache-runtime", HttpStatusCode.OK];
        yield return ["redis-fallback-runtime", HttpStatusCode.OK];
        yield return ["data-protection-redis-runtime", HttpStatusCode.OK];
        yield return ["exception-abstractions-runtime", HttpStatusCode.OK];
        yield return ["mediator-runtime", HttpStatusCode.OK];
        yield return ["logging-runtime", HttpStatusCode.OK];
        yield return ["unknown-mode", HttpStatusCode.BadRequest];
        yield return [string.Empty, HttpStatusCode.BadRequest];
    }

    public static IEnumerable<object[]> SuccessfulModeCases()
    {
        yield return ["menu-runtime"];
        yield return ["menu-runtime-cache-disabled"];
        yield return ["menu-runtime-no-cache-provider"];
        yield return ["menu-soft-delete-runtime"];
        yield return ["order-includes-runtime"];
        yield return ["abstractions-runtime"];
        yield return ["runtime-infrastructure"];
        yield return ["cqrs-handlers-runtime"];
        yield return ["endpointkit-core-runtime"];
        yield return ["endpointkit-marten-runtime"];
        yield return ["validation-runtime"];
        yield return ["exception-handling-runtime"];
        yield return ["data-protection-runtime"];
        yield return ["redis-cache-runtime"];
        yield return ["redis-fallback-runtime"];
        yield return ["data-protection-redis-runtime"];
        yield return ["exception-abstractions-runtime"];
        yield return ["mediator-runtime"];
        yield return ["logging-runtime"];
    }

    public static IEnumerable<object[]> TenantIsolationCases()
    {
        yield return ["menu-runtime"];
        yield return ["menu-runtime-cache-disabled"];
        yield return ["menu-runtime-no-cache-provider"];
        yield return ["menu-soft-delete-runtime"];
        yield return ["order-includes-runtime"];
        yield return ["abstractions-runtime"];
        yield return ["runtime-infrastructure"];
        yield return ["cqrs-handlers-runtime"];
        yield return ["endpointkit-core-runtime"];
        yield return ["endpointkit-marten-runtime"];
        yield return ["validation-runtime"];
        yield return ["exception-handling-runtime"];
        yield return ["data-protection-runtime"];
        yield return ["redis-cache-runtime"];
        yield return ["redis-fallback-runtime"];
        yield return ["data-protection-redis-runtime"];
        yield return ["exception-abstractions-runtime"];
        yield return ["mediator-runtime"];
        yield return ["logging-runtime"];
    }
}



