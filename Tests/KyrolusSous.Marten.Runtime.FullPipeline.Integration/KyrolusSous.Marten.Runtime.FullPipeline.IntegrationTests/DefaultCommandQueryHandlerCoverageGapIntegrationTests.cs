using System.Net;
using System.Net.Http.Json;
using System.Linq.Expressions;
using KyrolusSous.CQRS.Marten.Command.SoftDelete;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Authorization;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.MenuItems;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class DefaultCommandQueryHandlerCoverageGapIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact(DisplayName = "DefaultCommandQueryHandler marten batch - unknown operation type is rejected by allowlist validation")]
    public async Task Batch_unknown_operation_type_is_rejected_by_allowlist_validation()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("default-handler-batch-unknown-op"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-unknown",
                    Operation = (KyrolusBatchOperationType)999,
                    Id = Guid.NewGuid(),
                    Data = null
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        if (response.StatusCode != HttpStatusCode.InternalServerError)
        {
            body.ShouldContain("OPERATION_NOT_ALLOWED");
        }
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten batch - exception mapping matrix covers concurrency and generic failures")]
    [MemberData(nameof(BatchUpdateExceptionCases))]
    public async Task Batch_update_exception_mapping_matrix_covers_concurrency_and_generic_failures(
        bool throwConcurrency,
        string expectedMarker)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<ThrowingBatchUpdateCommand, MenuItem>, ThrowingBatchUpdateCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.UpdateCommand = new ThrowingBatchUpdateCommand
                {
                    ThrowConcurrency = throwConcurrency
                };
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-batch-update-ex-{throwConcurrency}"));
        var seeded = await CreateMenuItemAsync(client, "BatchFailure-Seed", "Main", 12);

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-update-throw",
                    Operation = KyrolusBatchOperationType.Update,
                    Id = seeded.Id,
                    Data = new MenuItem
                    {
                        Id = seeded.Id,
                        Name = "ShouldNotPersist",
                        Category = "Main",
                        Price = 200
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        if (response.StatusCode != HttpStatusCode.InternalServerError)
        {
            body.ShouldContain(expectedMarker);
        }

        var verify = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{seeded.Id}");
        verify.ShouldNotBeNull();
        verify!.Name.ShouldNotBe("ShouldNotPersist");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten by-keys - parser failure matrix covers throw, empty and key-count mismatch branches")]
    [MemberData(nameof(CompositeKeyFailureCases))]
    public async Task By_keys_parser_failure_matrix_covers_throw_empty_and_key_count_mismatch_branches(
        string mode,
        string expectedMarker)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                switch (mode)
                {
                    case "parser-throws":
                        config.CompositeKeyParser = static _ => throw new InvalidOperationException("parser-failure");
                        config.CompositeKeyPropertyNames = ["Id"];
                        break;
                    case "parser-empty":
                        config.CompositeKeyParser = static _ => [];
                        config.CompositeKeyPropertyNames = ["Id"];
                        break;
                    default:
                        config.CompositeKeyParser = null;
                        config.CompositeKeyTypes = [typeof(Guid), typeof(Guid)];
                        config.CompositeKeyPropertyNames = ["Id", "Name"];
                        break;
                }
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-parser-fail-{mode}"));

        var response = await client.GetAsync($"/api/menu-items/by-keys?keys={Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain(expectedMarker);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - object composite key type executes Convert.ChangeType fallback success path")]
    public async Task By_keys_put_with_object_composite_key_type_executes_convert_change_type_fallback_success_path()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.CompositeKeyTypes = [typeof(object)];
                config.CompositeKeyPropertyNames = ["Id"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-object-key-type"));
        var item = await CreateMenuItemAsync(client, "ObjectKeyType-Before", "Main", 18);

        var response = await client.PutAsJsonAsync(
            $"/api/menu-items/by-keys?keys={item.Id}",
            new MenuItem
            {
                Id = item.Id,
                Name = "ObjectKeyType-After",
                Category = "Main",
                Price = 19
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var verify = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{item.Id}");
        verify.ShouldNotBeNull();
        verify!.Name.ShouldBe("ObjectKeyType-After");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - unconvertible key type returns bad request")]
    public async Task By_keys_get_with_unconvertible_key_type_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.CompositeKeyTypes = [typeof(Tuple<int, int>)];
                config.CompositeKeyPropertyNames = ["Id"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-unconvertible-key-type"));

        var response = await client.GetAsync("/api/menu-items/by-keys?keys=not-a-valid-value");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Invalid key value");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - set composite key delegate branch is preferred when configured")]
    public async Task By_keys_put_uses_set_composite_key_delegate_branch_when_configured()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.SetCompositeKey = static (entity, keyValues) =>
                {
                    entity.Id = keyValues.Count > 0 && keyValues[0] is Guid id ? id : Guid.Empty;
                };
                config.CompositeKeyPropertyNames = ["MissingCompositeProperty"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-set-composite-delegate"));
        var item = await CreateMenuItemAsync(client, "CompositeDelegate-Before", "Main", 31);

        var response = await client.PutAsJsonAsync(
            $"/api/menu-items/by-keys?keys={item.Id}",
            new MenuItem
            {
                Id = Guid.Empty,
                Name = "CompositeDelegate-After",
                Category = "Main",
                Price = 32
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var verify = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{item.Id}");
        verify.ShouldNotBeNull();
        verify!.Name.ShouldBe("CompositeDelegate-After");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - mismatched composite key arity returns bad request")]
    public async Task By_keys_put_with_mismatched_composite_key_arity_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.SetCompositeKey = null;
                config.CompositeKeyPropertyNames = ["Id", "Name"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-composite-arity"));
        var item = await CreateMenuItemAsync(client, "CompositeArity", "Main", 22);

        var response = await client.PutAsJsonAsync(
            $"/api/menu-items/by-keys?keys={item.Id}",
            new MenuItem
            {
                Name = "CompositeArity-Update",
                Category = "Main",
                Price = 25
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Composite key expects 2 values.");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten selection - strict fields matrix rejects disallowed and unknown fields")]
    [MemberData(nameof(StrictSelectFieldCases))]
    public async Task Get_all_strict_fields_matrix_rejects_disallowed_and_unknown_fields(
        IReadOnlyCollection<string>? allowedFields,
        string requestedField,
        string expectedMarker)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictSelectValidation = true;
                config.AllowedSelectProperties = allowedFields;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-strict-fields-{requestedField}"));
        await CreateMenuItemAsync(client, "StrictFields-Seed", "Main", 4);

        var response = await client.GetAsync($"/api/menu-items?fields={Uri.EscapeDataString(requestedField)}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain(expectedMarker);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten context - patch by id matrix covers missing property conversion and mismatch guards")]
    [MemberData(nameof(ContextGuardCases))]
    public async Task Patch_by_id_context_guard_matrix_covers_missing_property_conversion_and_mismatch_guards(
        string mode,
        HttpStatusCode expectedStatus,
        string? expectedMarker)
    {
        var tenantBase = TestHelpers.NewTenantId($"default-handler-context-{mode}");
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
                config.TenantPropertyName = mode switch
                {
                    "missing-property" => "MissingTenantProperty",
                    "invalid-value" => nameof(MenuItem.TenantId),
                    _ => nameof(MenuItem.Category)
                };
                config.ScopePropertyName = string.Equals(mode, "invalid-value", StringComparison.Ordinal)
                    ? nameof(MenuItem.Price)
                    : null;
            });
        });

        Guid targetId;
        if (string.Equals(mode, "missing-property", StringComparison.Ordinal))
        {
            using var seedClient = factory.CreateClientWithTenant(tenantBase);
            targetId = (await CreateMenuItemAsync(seedClient, "ContextMissingProperty", "Main", 10)).Id;
        }
        else if (string.Equals(mode, "invalid-value", StringComparison.Ordinal))
        {
            using var seedClient = customFactory.CreateClient();
            seedClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantBase);
            targetId = (await CreateMenuItemAsync(seedClient, "ContextInvalidValue", "Main", 10)).Id;
        }
        else
        {
            using var seedClient = customFactory.CreateClient();
            seedClient.DefaultRequestHeaders.Add("X-Tenant-Id", "category-a");
            targetId = (await CreateMenuItemAsync(seedClient, "ContextMismatch", "Main", 10)).Id;
        }

        using var client = customFactory.CreateClient();
        var requestTenant = mode switch
        {
            "invalid-value" => tenantBase,
            "mismatch" => "category-b",
            _ => tenantBase
        };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", requestTenant);
        if (string.Equals(mode, "invalid-value", StringComparison.Ordinal))
        {
            client.DefaultRequestHeaders.Add("X-Scope", "not-a-number");
        }

        var updates = string.Equals(mode, "invalid-value", StringComparison.Ordinal)
            ? new Dictionary<string, object> { ["Category"] = "Main-Updated" }
            : new Dictionary<string, object> { ["Price"] = 99m };

        var response = await client.PatchAsJsonAsync(
            $"/api/menu-items/{targetId}",
            updates);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
        if (!string.IsNullOrWhiteSpace(expectedMarker))
        {
            body.ShouldContain(expectedMarker);
        }
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query - tenant filter conversion failure returns bad request")]
    public async Task Get_all_with_invalid_tenant_filter_conversion_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
                config.TenantPropertyName = nameof(MenuItem.Price);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "invalid-price-value");

        var response = await client.GetAsync("/api/menu-items");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Invalid tenant value.");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query-by-id - custom query type uses property setter fallback path")]
    public async Task Query_by_id_custom_query_type_uses_property_setter_fallback_path()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusQueryHandler<FallbackMenuItemByIdQuery, MenuItem?>, FallbackMenuItemByIdQueryHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.QueryById = new FallbackMenuItemByIdQuery();
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-fallback-query-by-id"));
        var item = await CreateMenuItemAsync(client, "FallbackById", "Main", 14);

        var response = await client.GetAsync($"/api/menu-items/{item.Id}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<MenuItem>();
        payload.ShouldNotBeNull();
        payload!.Id.ShouldBe(item.Id);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query-by-keys - custom query type uses property setter fallback path")]
    public async Task Query_by_keys_custom_query_type_uses_property_setter_fallback_path()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusQueryHandler<FallbackMenuItemByKeysQuery, MenuItem?>, FallbackMenuItemByKeysQueryHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.QueryByKeyValues = new FallbackMenuItemByKeysQuery();
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-fallback-query-by-keys"));
        var item = await CreateMenuItemAsync(client, "FallbackByKeys", "Main", 16);

        var response = await client.GetAsync($"/api/menu-items/by-keys?keys={item.Id}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<MenuItem>();
        payload.ShouldNotBeNull();
        payload!.Id.ShouldBe(item.Id);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten concurrency - endpoint matrix returns conflict when handlers throw ConcurrencyException")]
    [MemberData(nameof(EndpointConcurrencyCases))]
    public async Task Concurrency_endpoint_matrix_returns_conflict_when_handlers_throw_concurrency_exception(string mode)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>, ThrowingUpdateMenuItemCommandHandler>();
                services.AddScoped<IKyrolusCommandHandler<UpdateRangeCommand<MenuItem>, IEnumerable<MenuItem>>, ThrowingUpdateRangeMenuItemCommandHandler>();
                services.AddScoped<IKyrolusCommandHandler<MenuItemPatchCommand, MenuItem>, ThrowingPatchMenuItemCommandHandler>();
                services.AddScoped<IKyrolusCommandHandler<SoftDeleteByIdCommand<MenuItem, Guid>, bool>, ThrowingSoftDeleteMenuItemCommandHandler>();
                services.AddScoped<IKyrolusCommandHandler<RestoreByIdCommand<MenuItem, Guid>, bool>, ThrowingRestoreMenuItemCommandHandler>();
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-concurrency-{mode}"));
        var item = await CreateMenuItemAsync(client, "Concurrency-Seed", "Main", 10);

        var response = await SendConcurrencyRequestAsync(client, mode, item);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, body);
        body.ShouldContain("Concurrency conflict");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten concurrency - wrapped exception matrix returns conflict when concurrency is nested")]
    [MemberData(nameof(WrappedConcurrencyCases))]
    public async Task Update_by_id_wrapped_concurrency_exception_matrix_returns_conflict_when_concurrency_is_nested(string mode)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>>(_ => new WrappedUpdateMenuItemCommandHandler(mode));
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-wrapped-concurrency-{mode}"));
        var item = await CreateMenuItemAsync(client, $"Wrapped-{mode}", "Main", 10);

        var response = await client.PutAsJsonAsync(
            $"/api/menu-items/{item.Id}",
            new MenuItem
            {
                Id = item.Id,
                Name = $"Wrapped-{mode}-Updated",
                Category = item.Category,
                Price = item.Price + 1
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, body);
        body.ShouldContain("Concurrency conflict");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - includeDeleted=true returns soft-deleted item")]
    public async Task By_keys_get_with_include_deleted_true_returns_soft_deleted_item()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("default-handler-bykeys-include-deleted"));
        var item = await CreateMenuItemAsync(client, "ByKeys-IncludeDeleted", "Main", 14);

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{item.Id}");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deleteBody);

        var response = await client.GetAsync($"/api/menu-items/by-keys?keys={item.Id}&includeDeleted=true");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys patch - returns bad request when no patch fields remain after allowlist filtering")]
    public async Task By_keys_patch_returns_bad_request_when_no_patch_fields_remain_after_allowlist_filtering()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.AllowedPatchProperties = ["Price"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-patch-bykeys-no-allowed-fields"));
        var item = await CreateMenuItemAsync(client, "PatchByKeys-NoAllowedFields", "Main", 18);

        var response = await client.PatchAsJsonAsync(
            $"/api/menu-items/by-keys?keys={item.Id}",
            new Dictionary<string, object> { ["Name"] = "Ignored" });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("No patch fields are allowed.");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten batch - guard matrix covers required data and validation branches")]
    [MemberData(nameof(BatchOperationGuardCases))]
    public async Task Batch_guard_matrix_covers_required_data_and_validation_branches(string mode, string expectedMarker)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"default-handler-batch-guard-{mode}"));
        var existing = await CreateMenuItemAsync(client, "BatchGuard-Existing", "Main", 10);

        KyrolusBatchOperation<MenuItem, Guid> operation = mode switch
        {
            "update-missing-data" => new KyrolusBatchOperation<MenuItem, Guid>
            {
                OperationId = "op-update-missing-data",
                Operation = KyrolusBatchOperationType.Update,
                Id = existing.Id,
                Data = null
            },
            "update-validation" => new KyrolusBatchOperation<MenuItem, Guid>
            {
                OperationId = "op-update-validation",
                Operation = KyrolusBatchOperationType.Update,
                Id = existing.Id,
                Data = new MenuItem { Name = "", Category = "Main", Price = 12 }
            },
            "patch-missing-data" => new KyrolusBatchOperation<MenuItem, Guid>
            {
                OperationId = "op-patch-missing-data",
                Operation = KyrolusBatchOperationType.Patch,
                Id = existing.Id,
                Data = null
            },
            "upsert-missing-data" => new KyrolusBatchOperation<MenuItem, Guid>
            {
                OperationId = "op-upsert-missing-data",
                Operation = KyrolusBatchOperationType.Upsert,
                Id = existing.Id,
                Data = null
            },
            "create-missing-data" => new KyrolusBatchOperation<MenuItem, Guid>
            {
                OperationId = "op-create-missing-data",
                Operation = KyrolusBatchOperationType.Create,
                Id = Guid.NewGuid(),
                Data = null
            },
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations = [operation]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldContain(expectedMarker);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query - missing required tenant context returns bad request")]
    public async Task Get_all_without_required_tenant_context_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
                config.TenantPropertyName = nameof(MenuItem.TenantId);
            });
        });

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/menu-items");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("tenant is required");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query - missing tenant property mapping returns bad request")]
    public async Task Get_all_with_missing_tenant_property_mapping_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
                config.TenantPropertyName = "MissingTenantProperty";
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a");
        var response = await client.GetAsync("/api/menu-items");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("was not found");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query - invalid scope conversion in context filter returns bad request")]
    public async Task Get_all_with_invalid_scope_context_filter_conversion_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.ScopePropertyName = nameof(MenuItem.Price);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-invalid-scope-query"));
        client.DefaultRequestHeaders.Add("X-Scope", "invalid-number");
        var response = await client.GetAsync("/api/menu-items");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Invalid scope value.");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten create - invalid scope conversion in context assignment returns bad request")]
    public async Task Create_with_invalid_scope_context_assignment_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.ScopePropertyName = nameof(MenuItem.Price);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-invalid-scope-create"));
        client.DefaultRequestHeaders.Add("X-Scope", "invalid-number");

        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "InvalidScopeCreate",
            Category = "Main",
            Price = 5
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Cannot set scope.");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten patch - updating configured scope property is rejected")]
    public async Task Patch_rejects_updates_to_configured_scope_property()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.ScopePropertyName = nameof(MenuItem.Category);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-patch-scope-guard"));
        client.DefaultRequestHeaders.Add("X-Scope", "Main");
        var item = await CreateMenuItemAsync(client, "ScopeGuard", "Main", 15);

        var response = await client.PatchAsJsonAsync(
            $"/api/menu-items/{item.Id}",
            new Dictionary<string, object> { ["Category"] = "Changed" });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Scope cannot be updated.");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten authorization - row filter evaluation exception returns not-found when authorization masks resource")]
    public async Task Get_by_id_with_throwing_row_filter_returns_not_found_when_authorization_masks_resource()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusMartenAuthorizationProvider<MenuItem>, ThrowingRowFilterAuthorizationProvider>();
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-throwing-row-filter"));
        var item = await CreateMenuItemAsync(client, "ThrowingRowFilter", "Main", 13);

        var response = await client.GetAsync($"/api/menu-items/{item.Id}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys update - context-aware access returns not found when entity is missing")]
    public async Task Update_by_keys_with_context_filter_returns_not_found_when_entity_is_missing()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
                config.TenantPropertyName = nameof(MenuItem.TenantId);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-bykeys-missing-with-context"));

        var response = await client.PutAsJsonAsync(
            $"/api/menu-items/by-keys?keys={Guid.NewGuid()}",
            new MenuItem
            {
                Name = "MissingByKeys",
                Category = "Main",
                Price = 8
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, body);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten projection - selector matrix covers paged seek and query-seek paths")]
    [MemberData(nameof(ProjectionSelectorCases))]
    public async Task Projection_selector_matrix_covers_paged_seek_and_query_seek_paths(string mode)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"default-handler-projection-{mode}"));
        await CreateMenuItemAsync(client, "Projection-A", "Main", 10);
        await CreateMenuItemAsync(client, "Projection-B", "Main", 11);
        await CreateMenuItemAsync(client, "Projection-C", "Main", 12);

        HttpResponseMessage response = mode switch
        {
            "paged" => await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=2&fields=Name"),
            "seek" => await client.GetAsync("/api/menu-items/seek?pageSize=2&includeTotalCount=true&fields=Name"),
            "query-seek" => await client.PostAsJsonAsync(
                "/api/menu-items/query/seek",
                new
                {
                    pageSize = 2,
                    includeTotalCount = true,
                    request = new
                    {
                        fields = new[] { "Name" }
                    }
                }),
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten filter - strict validation returns bad request for invalid property")]
    public async Task Get_all_with_strict_filter_validation_invalid_property_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictFilterValidation = true;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-strict-filter-invalid"));
        var response = await client.GetAsync("/api/menu-items?filter=UnknownProperty==x");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("UnknownProperty");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten ordering - invalid order matrix returns bad request")]
    [InlineData("paged-query-string")]
    [InlineData("query-body")]
    public async Task Ordering_invalid_order_matrix_returns_bad_request(string mode)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictFilterValidation = true;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-order-invalid-{mode}"));
        await CreateMenuItemAsync(client, "OrderInvalid-A", "Main", 10);
        await CreateMenuItemAsync(client, "OrderInvalid-B", "Main", 20);

        HttpResponseMessage response = mode switch
        {
            "paged-query-string" => await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=2&orderBy=Unknown:asc"),
            "query-body" => await client.PostAsJsonAsync("/api/menu-items/query", new TestQueryRequest(OrderBy: [new TestOrderClause("Unknown", Desc: false)])),
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Unknown");
    }


    [Theory(DisplayName = "DefaultCommandQueryHandler marten include graph - enabled validation matrix covers allowed disallowed and depth branches")]
    [MemberData(nameof(IncludeGraphValidationCases))]
    public async Task Include_graph_enabled_validation_matrix_covers_allowed_disallowed_and_depth_branches(
        string caseName,
        string includeGraph,
        bool strict,
        string[]? allowedIncludes,
        int maxDepth,
        HttpStatusCode expectedStatus,
        string? expectedFragment)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.MaxIncludeGraphDepth = maxDepth;
                config.StrictIncludeValidation = strict;
                config.AllowedIncludeProperties = allowedIncludes;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"default-handler-include-graph-{caseName}"));
        var item = await CreateMenuItemAsync(client, $"IncludeGraph-{caseName}", "Main", 10);

        var response = await client.GetAsync($"/api/menu-items/{item.Id}?includeGraph={Uri.EscapeDataString(includeGraph)}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);

        if (expectedFragment is not null)
        {
            body.ShouldContain(expectedFragment);
        }
    }
    [Fact(DisplayName = "DefaultCommandQueryHandler marten include - strict include validation rejects unknown include")]
    public async Task Get_all_with_strict_include_validation_rejects_unknown_include()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictIncludeValidation = true;
                config.AllowedIncludeProperties = ["Category"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-strict-include-invalid"));
        await CreateMenuItemAsync(client, "StrictInclude-Seed", "Main", 10);

        var response = await client.GetAsync("/api/menu-items?includedProps=UnknownInclude");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("UnknownInclude");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten seek - missing key configuration returns bad request")]
    public async Task Seek_without_key_configuration_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.KeyPropertyName = null;
                config.CompositeKeyPropertyNames = [];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-seek-no-key-config"));
        await CreateMenuItemAsync(client, "Seek-NoKey-A", "Main", 10);

        var response = await client.GetAsync("/api/menu-items/seek?pageSize=2");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Seek properties are not configured.");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten response - enriched envelope matrix covers non-null and null success payloads")]
    [InlineData("success")]
    [InlineData("null-success")]
    public async Task Enriched_response_envelope_matrix_covers_success_payload_shapes(string mode)
    {
        var tenantId = TestHelpers.NewTenantId($"default-handler-enriched-{mode}");
        using var seedClient = factory.CreateClientWithTenant(tenantId);
        var seeded = await CreateMenuItemAsync(seedClient, "Enriched-Seed", "Main", 10);

        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.UseEnrichedCustomResponse = true;

                if (string.Equals(mode, "null-success", StringComparison.Ordinal))
                {
                    services.AddScoped<IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>, NullReturningUpdateMenuItemCommandHandler>();
                }
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        HttpResponseMessage response = mode switch
        {
            "success" => await client.GetAsync("/api/menu-items"),
            "null-success" => await client.PutAsJsonAsync(
                $"/api/menu-items/{seeded.Id}",
                new MenuItem
                {
                    Id = seeded.Id,
                    Name = "Enriched-Update",
                    Category = "Main",
                    Price = 11
                }),
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldContain("\"isSuccess\":true");
        if (string.Equals(mode, "null-success", StringComparison.Ordinal))
        {
            body.ShouldContain("\"data\":null");
        }
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten etag - numeric row version uses conversion parsing path")]
    public async Task Update_with_numeric_row_version_etag_uses_conversion_parsing_path()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.EnableEtags = true;
                config.RowVersionPropertyName = nameof(MenuItem.Price);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("default-handler-etag-numeric-rowversion"));
        var item = await CreateMenuItemAsync(client, "NumericEtag", "Main", 10);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "\"10\"");

        var response = await client.PutAsJsonAsync(
            $"/api/menu-items/{item.Id}",
            new MenuItem
            {
                Id = item.Id,
                Name = "NumericEtag-Updated",
                Category = "Main",
                Price = 11
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }


    public static IEnumerable<object[]> IncludeGraphValidationCases()
    {
        yield return ["valid-scalar-path", "Category", true, new[] { "Category" }, 1, HttpStatusCode.OK, null];
        yield return ["strict-disallowed", "Price", true, new[] { "Category" }, 1, HttpStatusCode.BadRequest, "not allowed"];
        yield return ["nonstrict-disallowed", "Price", false, new[] { "Category" }, 1, HttpStatusCode.OK, null];
        yield return ["strict-missing", "Missing", true, null, 1, HttpStatusCode.BadRequest, "does not exist"];
        yield return ["strict-depth", "Category.Length", true, null, 1, HttpStatusCode.BadRequest, "exceeds max depth"];
    }
    public static IEnumerable<object[]> BatchUpdateExceptionCases()
    {
        yield return [true, "CONCURRENCY_CONFLICT"];
        yield return [false, "INTERNAL_ERROR"];
    }

    public static IEnumerable<object[]> CompositeKeyFailureCases()
    {
        yield return ["parser-throws", "parser-failure"];
        yield return ["parser-empty", "Composite key is required."];
        yield return ["type-count-mismatch", "Composite key expects 2 values."];
    }

    public static IEnumerable<object[]> StrictSelectFieldCases()
    {
        yield return [new[] { "Name" }, "Price", "is not allowed"];
        yield return [null, "UnknownField", "does not exist"];
    }

    public static IEnumerable<object[]> ContextGuardCases()
    {
        yield return ["missing-property", HttpStatusCode.BadRequest, "was not found"];
        yield return ["invalid-value", HttpStatusCode.BadRequest, "Invalid scope value."];
        yield return ["mismatch", HttpStatusCode.NotFound, null];
    }

    public static IEnumerable<object[]> EndpointConcurrencyCases()
    {
        yield return ["update-id"];
        yield return ["update-by-keys"];
        yield return ["update-range"];
        yield return ["patch-id"];
        yield return ["patch-by-keys"];
        yield return ["delete-id"];
        yield return ["delete-by-keys"];
        yield return ["restore-id"];
        yield return ["restore-by-keys"];
    }

    public static IEnumerable<object[]> WrappedConcurrencyCases()
    {
        yield return ["target-invocation"];
        yield return ["aggregate"];
    }

    public static IEnumerable<object[]> BatchOperationGuardCases()
    {
        yield return ["update-missing-data", "MISSING_DATA"];
        yield return ["update-validation", "VALIDATION_ERROR"];
        yield return ["patch-missing-data", "MISSING_DATA"];
        yield return ["upsert-missing-data", "MISSING_DATA"];
        yield return ["create-missing-data", "MISSING_DATA"];
    }

    public static IEnumerable<object[]> ProjectionSelectorCases()
    {
        yield return ["paged"];
        yield return ["seek"];
        yield return ["query-seek"];
    }

    private sealed class FallbackMenuItemByIdQuery : IKyrolusQuery<MenuItem?>
    {
        public Guid Id { get; set; }
        public List<string>? IncludeProperties { get; set; }
        public Expression<Func<MenuItem, object?>>[]? IncludeExpressions { get; set; }
        public bool? AsNoTracking { get; set; }
        public bool? UseSplitQuery { get; set; }
        public string? TenantId { get; set; }
        public bool Cacheable { get; set; }
    }

    private sealed class FallbackMenuItemByIdQueryHandler(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        ITenantResolver tenantResolver)
        : IKyrolusQueryHandler<FallbackMenuItemByIdQuery, MenuItem?>
    {
        public async Task<MenuItem?> Handle(FallbackMenuItemByIdQuery query, CancellationToken cancellationToken)
        {
            var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            var options = new MartenQueryOptions<MenuItem>(
                IncludeProperties: query.IncludeProperties,
                IncludeExpressions: query.IncludeExpressions,
                TenantId: query.TenantId ?? tenantResolver.ResolveTenantId());
            var result = await repo.GetByIdAsync(query.Id, options, cancellationToken).ConfigureAwait(false);
            return result?.Entity;
        }
    }

    private sealed class FallbackMenuItemByKeysQuery : IKyrolusQuery<MenuItem?>
    {
        public object?[]? KeyValues { get; set; }
        public List<string>? IncludeProperties { get; set; }
        public Expression<Func<MenuItem, object?>>[]? IncludeExpressions { get; set; }
        public bool? AsNoTracking { get; set; }
        public bool? UseSplitQuery { get; set; }
        public string? TenantId { get; set; }
        public bool Cacheable { get; set; }
    }

    private sealed class FallbackMenuItemByKeysQueryHandler(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        ITenantResolver tenantResolver)
        : IKyrolusQueryHandler<FallbackMenuItemByKeysQuery, MenuItem?>
    {
        public async Task<MenuItem?> Handle(FallbackMenuItemByKeysQuery query, CancellationToken cancellationToken)
        {
            if (query.KeyValues is null || query.KeyValues.Length == 0)
            {
                return null;
            }

            Guid id;
            if (query.KeyValues[0] is Guid parsedGuid)
            {
                id = parsedGuid;
            }
            else if (query.KeyValues[0] is string raw && Guid.TryParse(raw, out var guidFromString))
            {
                id = guidFromString;
            }
            else
            {
                return null;
            }

            var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            var options = new MartenQueryOptions<MenuItem>(
                IncludeProperties: query.IncludeProperties,
                IncludeExpressions: query.IncludeExpressions,
                TenantId: query.TenantId ?? tenantResolver.ResolveTenantId());

            var result = await repo.GetByIdAsync(id, options, cancellationToken).ConfigureAwait(false);
            return result?.Entity;
        }
    }

    private sealed class ThrowingBatchUpdateCommand : IKyrolusCommand<MenuItem>
    {
        public MenuItem Entity { get; set; } = null!;
        public bool Cacheable { get; set; }
        public bool ThrowConcurrency { get; set; }
    }

    private sealed class ThrowingBatchUpdateCommandHandler : IKyrolusCommandHandler<ThrowingBatchUpdateCommand, MenuItem>
    {
        public Task<MenuItem> Handle(ThrowingBatchUpdateCommand command, CancellationToken cancellationToken)
        {
            if (command.ThrowConcurrency)
            {
                throw CreateMartenConcurrencyException();
            }

            throw new InvalidOperationException("Simulated batch failure.");
        }
    }

    private sealed class ThrowingUpdateMenuItemCommandHandler : IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>
    {
        public Task<MenuItem> Handle(UpdateCommand<MenuItem> command, CancellationToken cancellationToken)
            => throw CreateMartenConcurrencyException();
    }

    private sealed class WrappedUpdateMenuItemCommandHandler(string mode) : IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>
    {
        public Task<MenuItem> Handle(UpdateCommand<MenuItem> command, CancellationToken cancellationToken)
            => throw mode switch
            {
                "target-invocation" => new System.Reflection.TargetInvocationException(CreateMartenConcurrencyException()),
                "aggregate" => new AggregateException(CreateMartenConcurrencyException()),
                _ => new InvalidOperationException($"Unknown wrapped concurrency mode '{mode}'.")
            };
    }

    private sealed class NullReturningUpdateMenuItemCommandHandler : IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>
    {
        public Task<MenuItem> Handle(UpdateCommand<MenuItem> command, CancellationToken cancellationToken)
            => Task.FromResult<MenuItem>(null!);
    }

    private sealed class ThrowingUpdateRangeMenuItemCommandHandler : IKyrolusCommandHandler<UpdateRangeCommand<MenuItem>, IEnumerable<MenuItem>>
    {
        public Task<IEnumerable<MenuItem>> Handle(UpdateRangeCommand<MenuItem> command, CancellationToken cancellationToken)
            => throw CreateMartenConcurrencyException();
    }

    private sealed class ThrowingPatchMenuItemCommandHandler : IKyrolusCommandHandler<MenuItemPatchCommand, MenuItem>
    {
        public Task<MenuItem> Handle(MenuItemPatchCommand command, CancellationToken cancellationToken)
            => throw CreateMartenConcurrencyException();
    }

    private sealed class ThrowingSoftDeleteMenuItemCommandHandler : IKyrolusCommandHandler<SoftDeleteByIdCommand<MenuItem, Guid>, bool>
    {
        public Task<bool> Handle(SoftDeleteByIdCommand<MenuItem, Guid> command, CancellationToken cancellationToken)
            => throw CreateMartenConcurrencyException();
    }

    private sealed class ThrowingRestoreMenuItemCommandHandler : IKyrolusCommandHandler<RestoreByIdCommand<MenuItem, Guid>, bool>
    {
        public Task<bool> Handle(RestoreByIdCommand<MenuItem, Guid> command, CancellationToken cancellationToken)
            => throw CreateMartenConcurrencyException();
    }

    private sealed class ThrowingRowFilterAuthorizationProvider : IKyrolusMartenAuthorizationProvider<MenuItem>
    {
        public ValueTask<KyrolusMartenAuthorizationResult<MenuItem>> AuthorizeAsync(
            KyrolusMartenAuthorizationContext<MenuItem> context,
            CancellationToken cancellationToken = default)
        {
            if (context.Endpoint == EndpointNames.GetById)
            {
                return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>(
                    IsAuthorized: true,
                    RowFilter: entity => ThrowOnRowFilter(entity)));
            }

            return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>());
        }
    }

    private static bool ThrowOnRowFilter(MenuItem _)
        => throw new InvalidOperationException("Simulated row filter evaluation failure.");

    private static Exception CreateMartenConcurrencyException()
    {
        return new ConcurrencyException("Simulated concurrency.");
    }

    private static async Task<MenuItem> CreateMenuItemAsync(HttpClient client, string name, string category, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = name,
            Category = category,
            Price = price
        });

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<MenuItem>();
        item.ShouldNotBeNull();
        return item!;
    }

    private static async Task<HttpResponseMessage> SendConcurrencyRequestAsync(HttpClient client, string mode, MenuItem seed)
    {
        return mode switch
        {
            "update-id" => await client.PutAsJsonAsync(
                $"/api/menu-items/{seed.Id}",
                new MenuItem
                {
                    Id = seed.Id,
                    Name = "Concurrency-UpdateId",
                    Category = seed.Category,
                    Price = seed.Price + 1
                }),
            "update-by-keys" => await client.PutAsJsonAsync(
                $"/api/menu-items/by-keys?keys={seed.Id}",
                new MenuItem
                {
                    Id = seed.Id,
                    Name = "Concurrency-UpdateByKeys",
                    Category = seed.Category,
                    Price = seed.Price + 2
                }),
            "update-range" => await client.PutAsJsonAsync(
                "/api/menu-items/range",
                new[]
                {
                    new MenuItem
                    {
                        Id = seed.Id,
                        Name = "Concurrency-UpdateRange",
                        Category = seed.Category,
                        Price = seed.Price + 3
                    }
                }),
            "patch-id" => await client.PatchAsJsonAsync(
                $"/api/menu-items/{seed.Id}",
                new Dictionary<string, object> { ["Price"] = seed.Price + 4 }),
            "patch-by-keys" => await client.PatchAsJsonAsync(
                $"/api/menu-items/by-keys?keys={seed.Id}",
                new Dictionary<string, object> { ["Price"] = seed.Price + 5 }),
            "delete-id" => await client.DeleteAsync($"/api/menu-items/{seed.Id}"),
            "delete-by-keys" => await client.DeleteAsync($"/api/menu-items/by-keys?keys={seed.Id}"),
            "restore-id" => await client.PostAsync($"/api/menu-items/{seed.Id}/restore", content: null),
            "restore-by-keys" => await client.PostAsync($"/api/menu-items/by-keys/restore?keys={seed.Id}", content: null),
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };
    }

    private static IKyrolusMartenApiConfig<MenuItem> ResolveMenuItemMartenConfig(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces.IKyrolusApiConfig<MenuItem>));
        if (descriptor?.ImplementationInstance is IKyrolusMartenApiConfig<MenuItem> config)
        {
            return config;
        }

        throw new InvalidOperationException("MenuItem IKyrolusMartenApiConfig is not registered.");
    }

    private static void AssertBatchResponseStatus(HttpStatusCode statusCode)
    {
        new[] { HttpStatusCode.OK, HttpStatusCode.MultiStatus, HttpStatusCode.InternalServerError }
            .ShouldContain(statusCode);
    }
}


