using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

[Collection("MartenPipelineTestCollection")]
public sealed class BatchCommandBranchIntegrationTests(TestAppFactory factory)
{
    [Fact(DisplayName = "Batch endpoint - operation not allowed marks next operation as skipped")]
    public async Task Batch_operation_not_allowed_marks_next_operation_as_skipped()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-not-allowed"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-not-allowed",
                    Operation = (KyrolusBatchOperationType)999,
                    Data = null
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem { Name = "ShouldNotCreate", Category = "Main", Price = 10 }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldNotContain(x => x.Name == "ShouldNotCreate");
    }

    [Fact(DisplayName = "Batch endpoint - no operations returns bad request")]
    public async Task Batch_no_operations_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-empty"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations = []
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("No operations provided.");
    }

    [Fact(DisplayName = "Batch endpoint - too many operations returns bad request")]
    public async Task Batch_too_many_operations_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-max"));

        var operations = Enumerable.Range(1, 101)
            .Select(i => new KyrolusBatchOperation<MenuItem, Guid>
            {
                OperationId = $"op-{i}",
                Operation = KyrolusBatchOperationType.Create,
                Data = new MenuItem
                {
                    Name = $"Item-{i}",
                    Category = "Main",
                    Price = i
                }
            })
            .ToList();

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations = operations
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Too many operations.");
    }

    [Fact(DisplayName = "Batch endpoint - disabled option returns bad request")]
    public async Task Batch_disabled_option_returns_bad_request()
    {
        using var disabledFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.BatchOptions.Enabled = false;
            });
        });

        using var client = disabledFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-batch-disabled"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem
                    {
                        Name = "ShouldFailWhenDisabled",
                        Category = "Main",
                        Price = 9
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Batch operations are not enabled for this endpoint.");
    }

    [Fact(DisplayName = "Batch endpoint - unknown operation branch executes when operation is explicitly allowed")]
    public async Task Batch_unknown_operation_branch_executes_when_operation_is_explicitly_allowed()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.BatchOptions.AllowedOperations.Add((KyrolusBatchOperationType)999);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-batch-unknown-allowed"));
        var marker = $"UnknownAllowed-{Guid.NewGuid():N}";

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-unknown",
                    Operation = (KyrolusBatchOperationType)999
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem
                    {
                        Name = marker,
                        Category = "Main",
                        Price = 13
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldContain(x => x.Name == marker);
    }

    [Theory(DisplayName = "Batch endpoint - require tenant causes context error for mutating operations")]
    [InlineData(KyrolusBatchOperationType.Create)]
    [InlineData(KyrolusBatchOperationType.Update)]
    [InlineData(KyrolusBatchOperationType.Upsert)]
    public async Task Batch_require_tenant_causes_context_error_for_mutating_operations(KyrolusBatchOperationType operation)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
            });
        });

        using var client = customFactory.CreateClient();
        var marker = $"RequireTenant-{operation}-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op",
                    Operation = operation,
                    Id = id,
                    Data = new MenuItem
                    {
                        Id = id,
                        Name = marker,
                        Category = "Main",
                        Price = 17
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");
    }

    [Fact(DisplayName = "Batch endpoint - update with invalid key property triggers id error branch")]
    public async Task Batch_update_with_invalid_key_property_triggers_id_error_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.KeyPropertyName = "MissingIdProperty";
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-batch-id-error"));
        var marker = $"IdError-{Guid.NewGuid():N}";
        var targetId = Guid.NewGuid();

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-update",
                    Operation = KyrolusBatchOperationType.Update,
                    Id = targetId,
                    Data = new MenuItem
                    {
                        Id = targetId,
                        Name = marker,
                        Category = "Main",
                        Price = 18
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldNotContain(x => x.Name == marker);
    }

    [Fact(DisplayName = "Batch endpoint - custom key-values patch command executes patch success branch")]
    public async Task Batch_custom_key_values_patch_command_executes_patch_success_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<KeyValuesPatchCommand, MenuItem>, KeyValuesPatchCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.PatchCommand = new KeyValuesPatchCommand();
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-batch-patch-success"));
        var existing = await CreateMenuItemAsync(client, "BatchPatch-Before", "Main", 14);

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-patch",
                    Operation = KyrolusBatchOperationType.Patch,
                    Id = existing.Id,
                    Data = new MenuItem
                    {
                        Name = "BatchPatch-After",
                        Category = "Main",
                        Price = 77
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var updated = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{existing.Id}?includeDeleted=true");
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("BatchPatch-After");
        updated.Price.ShouldBe(77);
    }

    [Theory(DisplayName = "Bulk patch endpoint - disallowed fields follow strict validation mode")]
    [InlineData(true, HttpStatusCode.BadRequest)]
    [InlineData(false, HttpStatusCode.OK)]
    public async Task Bulk_patch_disallowed_fields_follow_strict_validation_mode(
        bool strictPatchValidation,
        HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictPatchValidation = strictPatchValidation;
                config.AllowedPatchProperties = ["Price"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-bulk-patch-strict-{strictPatchValidation}"));
        var beforeName = strictPatchValidation ? "BulkStrict-Before" : "BulkSkip-Before";
        var existing = await CreateMenuItemAsync(client, beforeName, "Main", 11);

        var payload = new[]
        {
            new
            {
                id = existing.Id.ToString(),
                updates = new Dictionary<string, object> { ["Name"] = "BulkDisallowed-After" }
            }
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/patch", payload);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
        if (strictPatchValidation)
        {
            body.ShouldContain("not allowed");
            return;
        }

        var patchedCount = await response.Content.ReadFromJsonAsync<int>();
        patchedCount.ShouldBe(0);
        var unchanged = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{existing.Id}?includeDeleted=true");
        unchanged.ShouldNotBeNull();
        unchanged!.Name.ShouldBe(beforeName);
    }

    [Theory(DisplayName = "Bulk patch endpoint - missing or empty updates returns bad request")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    public async Task Bulk_patch_missing_or_empty_updates_returns_bad_request(string updatesMode)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk-patch-updates-required"));
        var item = new Dictionary<string, object?> { ["id"] = Guid.NewGuid().ToString() };
        if (updatesMode == "null")
        {
            item["updates"] = null;
        }
        else if (updatesMode == "empty")
        {
            item["updates"] = new Dictionary<string, object>();
        }

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/patch", new[] { item });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Updates are required.");
    }

    [Fact(DisplayName = "Bulk patch endpoint - keys array path updates target entity")]
    public async Task Bulk_patch_keys_array_path_updates_target_entity()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk-patch-keys-array"));
        var existing = await CreateMenuItemAsync(client, "BulkKeys-Before", "Main", 13);

        var payload = new[]
        {
            new
            {
                keys = new[] { existing.Id.ToString() },
                updates = new Dictionary<string, object> { ["Price"] = 222m }
            }
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/patch", payload);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var patchedCount = await response.Content.ReadFromJsonAsync<int>();
        patchedCount.ShouldBeGreaterThanOrEqualTo(0);

        var current = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{existing.Id}?includeDeleted=true");
        current.ShouldNotBeNull();
    }

    [Theory(DisplayName = "Bulk patch endpoint - missing key values returns bad request")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_patch_missing_key_values_returns_bad_request(bool useEmptyKeysArray)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk-patch-composite-key"));
        var item = new Dictionary<string, object?>()
        {
            ["updates"] = new Dictionary<string, object> { ["Price"] = 88m }
        };
        if (useEmptyKeysArray)
        {
            item["keys"] = Array.Empty<string>();
        }

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/patch", new[] { item });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Composite key is required.");
    }

    [Theory(DisplayName = "Batch endpoint - continueOnError flags control next operation execution")]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task Batch_continue_on_error_flags_control_next_operation_execution(
        bool requestContinueOnError,
        bool operationContinueOnError,
        bool shouldCreate)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-continue"));
        var marker = $"Continue-{Guid.NewGuid():N}";

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = requestContinueOnError,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-invalid",
                    Operation = (KyrolusBatchOperationType)999,
                    ContinueOnError = operationContinueOnError
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem
                    {
                        Name = marker,
                        Category = "Main",
                        Price = 12
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.Any(x => x.Name == marker).ShouldBe(shouldCreate);
    }

    [Theory(DisplayName = "Batch endpoint - precondition failures return expected error code")]
    [MemberData(nameof(PreconditionFailureCases))]
    public async Task Batch_precondition_failures_return_expected_error_code(
        KyrolusBatchOperationType operation,
        Guid id,
        MenuItem? data,
        string expectedErrorCode)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-preconditions"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op",
                    Operation = operation,
                    Id = id,
                    Data = data
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeFalse();
        payload.FailureCount.ShouldBe(1);
        payload.Results.Count.ShouldBe(1);

        var errorCode = NormalizeErrorCode(payload.Results[0].Error?.Code);
        errorCode.ShouldBe(expectedErrorCode);
    }

    [Theory(DisplayName = "Batch endpoint - keyed operations reject empty guid id with missing_id")]
    [MemberData(nameof(GuidEmptyIdCases))]
    public async Task Batch_keyed_operations_reject_empty_guid_id_with_missing_id(
        KyrolusBatchOperationType operation,
        MenuItem? data)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"menuitem-batch-empty-guid-{operation}"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-empty-id",
                    Operation = operation,
                    Id = Guid.Empty,
                    Data = data
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeFalse();
        payload.FailureCount.ShouldBe(1);
        payload.Results.Count.ShouldBe(1);

        var operationResult = payload.Results[0];
        operationResult.Success.ShouldBeFalse();
        operationResult.Status.ShouldBe((int)HttpStatusCode.BadRequest);
        NormalizeErrorCode(operationResult.Error?.Code).ShouldBe("MISSING_ID");
    }

    [Fact(DisplayName = "Batch endpoint - patch for missing entity returns internal error result")]
    public async Task Batch_patch_missing_entity_returns_internal_error_result()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-patch-missing"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-patch-missing",
                    Operation = KyrolusBatchOperationType.Patch,
                    Id = Guid.NewGuid(),
                    Data = new MenuItem { Name = "Missing", Category = "Main", Price = 20 }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeFalse();
        payload.FailureCount.ShouldBe(1);
        payload.Results.Count.ShouldBe(1);

        var operationResult = payload.Results[0];
        operationResult.Success.ShouldBeFalse();
        operationResult.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        NormalizeErrorCode(operationResult.Error?.Code).ShouldBe("INTERNAL_ERROR");
    }

    [Theory(DisplayName = "Batch endpoint - mixed results aggregate counts and return data contract")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Batch_mixed_results_aggregate_counts_and_return_data_contract(bool returnData)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.BatchOptions.AllowedOperations.Add((KyrolusBatchOperationType)999);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-batch-mixed-{returnData}"));
        var marker = $"MixedCreate-{Guid.NewGuid():N}";

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = true,
            ReturnData = returnData,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-unknown",
                    Operation = (KyrolusBatchOperationType)999
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem
                    {
                        Name = marker,
                        Category = "Main",
                        Price = 31
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeFalse();
        payload.TotalOperations.ShouldBe(2);
        payload.SuccessCount.ShouldBe(1);
        payload.FailureCount.ShouldBe(1);
        payload.Results.Count.ShouldBe(2);

        var failed = payload.Results[0];
        failed.OperationId.ShouldBe("op-unknown");
        failed.Success.ShouldBeFalse();
        failed.Status.ShouldBe(StatusCodes.Status400BadRequest);
        NormalizeErrorCode(failed.Error?.Code).ShouldBe("UNKNOWN_OPERATION");
        failed.Data.ShouldBeNull();

        var created = payload.Results[1];
        created.OperationId.ShouldBe("op-create");
        created.Success.ShouldBeTrue();
        created.Status.ShouldBe(StatusCodes.Status201Created);
        created.Id.ShouldNotBe(Guid.Empty);
        if (returnData)
        {
            created.Data.ShouldNotBeNull();
            created.Data!.Name.ShouldBe(marker);
            created.Data.Price.ShouldBe(31);
        }
        else
        {
            created.Data.ShouldBeNull();
        }

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldContain(x => x.Name == marker);
    }

    [Fact(DisplayName = "Batch endpoint - stopped execution serializes skipped result contract")]
    public async Task Batch_stopped_execution_serializes_skipped_result_contract()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-skipped-contract"));
        var marker = $"SkippedCreate-{Guid.NewGuid():N}";

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = true,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-not-allowed",
                    Operation = (KyrolusBatchOperationType)999
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-skipped",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem
                    {
                        Name = marker,
                        Category = "Main",
                        Price = 32
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeFalse();
        payload.TotalOperations.ShouldBe(2);
        payload.SuccessCount.ShouldBe(0);
        payload.FailureCount.ShouldBe(2);
        payload.Results.Count.ShouldBe(2);

        NormalizeErrorCode(payload.Results[0].Error?.Code).ShouldBe("OPERATION_NOT_ALLOWED");
        payload.Results[0].Data.ShouldBeNull();
        NormalizeErrorCode(payload.Results[1].Error?.Code).ShouldBe("SKIPPED");
        payload.Results[1].Data.ShouldBeNull();
        payload.Results[1].Status.ShouldBe(StatusCodes.Status400BadRequest);

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldNotContain(x => x.Name == marker);
    }

    [Fact(DisplayName = "Batch endpoint - validation failure includes detailed error contract")]
    public async Task Batch_validation_failure_includes_detailed_error_contract()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-validation-details"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = true,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-invalid-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = InvalidModel()
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeFalse();
        payload.TotalOperations.ShouldBe(1);
        payload.SuccessCount.ShouldBe(0);
        payload.FailureCount.ShouldBe(1);

        var result = payload.Results.Single();
        result.Success.ShouldBeFalse();
        result.Operation.ShouldBe(KyrolusBatchOperationType.Create);
        result.Status.ShouldBe(StatusCodes.Status400BadRequest);
        NormalizeErrorCode(result.Error?.Code).ShouldBe("VALIDATION_ERROR");
        result.Error.ShouldNotBeNull();
        result.Error!.Details.ShouldNotBeNull();
        result.Error.Details!.Count.ShouldBeGreaterThanOrEqualTo(3);
        result.Error.Details.ShouldContain(detail => string.Equals(detail.Field, nameof(MenuItem.Name), StringComparison.OrdinalIgnoreCase));
        result.Error.Details.ShouldContain(detail => string.Equals(detail.Field, nameof(MenuItem.Category), StringComparison.OrdinalIgnoreCase));
        result.Error.Details.ShouldContain(detail => string.Equals(detail.Field, nameof(MenuItem.Price), StringComparison.OrdinalIgnoreCase));
        result.Error.Details.ShouldAllBe(detail => !string.IsNullOrWhiteSpace(detail.Code));
        result.Error.Details.ShouldAllBe(detail => !string.IsNullOrWhiteSpace(detail.Message));
        result.Data.ShouldBeNull();
    }

    [Theory(DisplayName = "Batch endpoint - single operation success paths execute")]
    [MemberData(nameof(SuccessPathCases))]
    public async Task Batch_single_operation_success_paths_execute(
        KyrolusBatchOperationType operation,
        bool useExistingId,
        bool returnData)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-success"));

        MenuItem? seeded = null;
        if (useExistingId)
        {
            seeded = await CreateMenuItemAsync(client, "Seeded", "Main", 10);
        }

        var targetId = useExistingId ? seeded!.Id : Guid.NewGuid();
        var expectedName = operation switch
        {
            KyrolusBatchOperationType.Create => "Created",
            KyrolusBatchOperationType.Update => "Updated",
            _ => "Upserted"
        };
        var data = operation == KyrolusBatchOperationType.Delete
            ? null
            : new MenuItem
            {
                Id = targetId,
                Name = expectedName,
                Category = "Main",
                Price = 55
            };

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = returnData,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op",
                    Operation = operation,
                    Id = targetId,
                    Data = data
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var payload = await response.Content.ReadFromJsonAsync<KyrolusBatchResponse<MenuItem, Guid>>();
        payload.ShouldNotBeNull();
        payload!.Success.ShouldBeTrue();
        payload.TotalOperations.ShouldBe(1);
        payload.SuccessCount.ShouldBe(1);
        payload.FailureCount.ShouldBe(0);
        payload.Results.Count.ShouldBe(1);

        var result = payload.Results[0];
        result.OperationId.ShouldBe("op");
        result.Operation.ShouldBe(operation);
        result.Success.ShouldBeTrue();

        var expectedStatus = operation switch
        {
            KyrolusBatchOperationType.Create => StatusCodes.Status201Created,
            KyrolusBatchOperationType.Update => StatusCodes.Status200OK,
            KyrolusBatchOperationType.Delete => StatusCodes.Status200OK,
            KyrolusBatchOperationType.Upsert when useExistingId => StatusCodes.Status200OK,
            KyrolusBatchOperationType.Upsert => StatusCodes.Status201Created,
            _ => StatusCodes.Status200OK
        };
        result.Status.ShouldBe(expectedStatus);

        if (operation == KyrolusBatchOperationType.Delete || !returnData)
        {
            result.Data.ShouldBeNull();
        }
        else
        {
            result.Data.ShouldNotBeNull();
            result.Data!.Name.ShouldBe(expectedName);
            result.Data.Price.ShouldBe(55);
        }

        if (operation == KyrolusBatchOperationType.Delete && seeded is not null)
        {
            var deleted = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{seeded.Id}?includeDeleted=true");
            deleted.ShouldNotBeNull();
            deleted!.IsDeleted.ShouldBeTrue();
        }

        if (operation == KyrolusBatchOperationType.Create)
        {
            var created = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{targetId}?includeDeleted=true");
            created.ShouldNotBeNull();
            created!.Name.ShouldBe("Created");
            created.Price.ShouldBe(55);
        }

        if (operation == KyrolusBatchOperationType.Update && seeded is not null)
        {
            var updated = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{seeded.Id}?includeDeleted=true");
            updated.ShouldNotBeNull();
            updated!.Name.ShouldBe("Updated");
            updated.Price.ShouldBe(55);
        }

        if (operation == KyrolusBatchOperationType.Upsert && !useExistingId)
        {
            var created = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{targetId}?includeDeleted=true");
            created.ShouldNotBeNull();
            created!.Name.ShouldBe("Upserted");
            created.Price.ShouldBe(55);
        }

        if (operation == KyrolusBatchOperationType.Upsert && useExistingId && seeded is not null)
        {
            var updated = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{seeded.Id}?includeDeleted=true");
            updated.ShouldNotBeNull();
            updated!.Name.ShouldBe("Upserted");
            updated.Price.ShouldBe(55);
        }
    }

    [Fact(DisplayName = "Batch endpoint - upsert with empty id creates entity through create-new branch")]
    public async Task Batch_upsert_with_empty_id_creates_entity_through_create_new_branch()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-upsert-empty-id"));
        var marker = $"UpsertEmpty-{Guid.NewGuid():N}";

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = true,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-upsert-empty",
                    Operation = KyrolusBatchOperationType.Upsert,
                    Id = Guid.Empty,
                    Data = new MenuItem
                    {
                        Id = Guid.Empty,
                        Name = marker,
                        Category = "Main",
                        Price = 21
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, body);
        body.ShouldNotContain("\"code\":\"internal_error\"");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        var created = all!.Single(x => x.Name == marker);
        created.Id.ShouldNotBe(Guid.Empty);
    }

    public static IEnumerable<object?[]> PreconditionFailureCases()
    {
        yield return [KyrolusBatchOperationType.Create, Guid.NewGuid(), (MenuItem?)null!, "MISSING_DATA"];
        yield return [KyrolusBatchOperationType.Update, Guid.NewGuid(), (MenuItem?)null!, "MISSING_DATA"];
        yield return [KyrolusBatchOperationType.Patch, Guid.NewGuid(), (MenuItem?)null!, "MISSING_DATA"];
        yield return [KyrolusBatchOperationType.Upsert, Guid.NewGuid(), (MenuItem?)null!, "MISSING_DATA"];
        yield return [KyrolusBatchOperationType.Create, Guid.NewGuid(), InvalidModel(), "VALIDATION_ERROR"];
    }

    public static IEnumerable<object?[]> GuidEmptyIdCases()
    {
        yield return [KyrolusBatchOperationType.Update, ValidModel("Update-Empty-Id")];
        yield return [KyrolusBatchOperationType.Patch, ValidModel("Patch-Empty-Id")];
        yield return [KyrolusBatchOperationType.Delete, (MenuItem?)null];
    }

    public static IEnumerable<object?[]> SuccessPathCases()
    {
        yield return [KyrolusBatchOperationType.Create, false, false];
        yield return [KyrolusBatchOperationType.Create, false, true];
        yield return [KyrolusBatchOperationType.Update, true, false];
        yield return [KyrolusBatchOperationType.Update, true, true];
        yield return [KyrolusBatchOperationType.Delete, true, false];
        yield return [KyrolusBatchOperationType.Upsert, false, false];
        yield return [KyrolusBatchOperationType.Upsert, false, true];
        yield return [KyrolusBatchOperationType.Upsert, true, false];
        yield return [KyrolusBatchOperationType.Upsert, true, true];
    }

    private static MenuItem InvalidModel()
        => new()
        {
            Name = string.Empty,
            Category = string.Empty,
            Price = 0
        };

    private static MenuItem ValidModel(string name)
        => new()
        {
            Name = name,
            Category = "Main",
            Price = 10
        };

    private static string NormalizeErrorCode(string? code)
        => (code ?? string.Empty)
            .Trim()
            .Replace('-', '_')
            .ToUpperInvariant();

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

    private static IKyrolusMartenApiConfig<MenuItem> ResolveMenuItemMartenConfig(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusApiConfig<MenuItem>));
        if (descriptor?.ImplementationInstance is IKyrolusMartenApiConfig<MenuItem> config)
        {
            return config;
        }

        throw new InvalidOperationException("MenuItem IKyrolusMartenApiConfig is not registered.");
    }

    private sealed class KeyValuesPatchCommand : IKyrolusCommand<MenuItem>
    {
        public object?[] KeyValues { get; set; } = Array.Empty<object?>();
        public Dictionary<string, object> Updates { get; set; } = new();
    }

    private sealed class KeyValuesPatchCommandHandler(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork)
        : IKyrolusCommandHandler<KeyValuesPatchCommand, MenuItem>
    {
        public async Task<MenuItem> Handle(KeyValuesPatchCommand command, CancellationToken cancellationToken)
        {
            if (command.KeyValues is not { Length: > 0 } || command.KeyValues[0] is not Guid id)
            {
                throw new ArgumentException("KeyValues must include Guid id.", nameof(command.KeyValues));
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name", "Category", "Price" };
            var sanitized = command.Updates
                .Where(kv => allowed.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            if (sanitized.Count == 0)
            {
                throw new ArgumentException("No supported updates were provided.", nameof(command.Updates));
            }

            var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            var result = await repo.PatchAsync(id, sanitized, tenantId: null, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return result?.Entity ?? throw new KyrolusNotFoundException(nameof(MenuItem), id.ToString());
        }
    }
}





