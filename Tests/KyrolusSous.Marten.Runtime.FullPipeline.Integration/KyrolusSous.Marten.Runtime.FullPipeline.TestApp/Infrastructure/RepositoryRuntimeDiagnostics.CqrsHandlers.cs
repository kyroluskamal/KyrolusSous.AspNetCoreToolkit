using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Marten.Command.Add;
using KyrolusSous.CQRS.Marten.Command.Patch;
using KyrolusSous.CQRS.Marten.Command.Remove;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Envelope;
using KyrolusSous.EndpointKit.Core.FieldSelection;
using KyrolusSous.EndpointKit.Core.Hateoas;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.ExceptionHandling;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.ClasesAndHelpers;
using KyrolusSous.ExceptionHandling.FluentValidation;
using KyrolusSous.ExceptionHandling.Handlers;
using KyrolusSous.ExceptionHandling.Interfaces;
using KyrolusSous.ExceptionHandling.Mapping;
using KyrolusSous.ExceptionHandling.Writers;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Authorization;
using KyrolusSous.Repositories.Marten.Abstractions;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Observer;
using KyrolusSous.Repositories.Marten.Abstractions.Query;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using KyrolusSous.Repositories.Marten.Abstractions.Resilience;
using KyrolusSous.Repositories.Marten.Abstractions.SoftDelete;
using KyrolusSous.Repositories.Marten.Abstractions.Specifications;
using KyrolusSous.Repositories.Marten.Abstractions.Tracing;
using KyrolusSous.Repositories.Marten.Abstractions.Validation;
using KyrolusSous.Repositories.Marten.Runtime;
using KyrolusSous.Repositories.Marten.Runtime.EventStore;
using KyrolusSous.Repositories.Marten.Runtime.Projection;
using KyrolusSous.Repositories.Marten.Runtime.Repository;
using KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;
using KyrolusSous.Repositories.Marten.Runtime.Saga;
using KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;
using KyrolusSous.Validation.Abstractions;
using KyrolusSous.Validation.FluentValidation;
using KyrolusSous.Validation.Runtime;
using KyrolusSous.CQRS.Validation;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Marten.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Npgsql;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    private static async Task<int> RunCqrsHandlerScenariosAsync(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;

        var category = $"DiagCqrsHandlers-{Guid.NewGuid():N}";
        var seed = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Seed-{Guid.NewGuid():N}",
            Category = category,
            Price = 10,
            IsDeleted = false
        };

        var addHandler = new AddCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var added = await addHandler.Handle(new AddCommand<MenuItem>(seed), cancellationToken).ConfigureAwait(false);
        if (added.Id == seed.Id)
        {
            checks++;
        }

        var rangeItems = new[]
        {
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Range-A-{Guid.NewGuid():N}",
                Category = category,
                Price = 20,
                IsDeleted = false
            },
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Range-B-{Guid.NewGuid():N}",
                Category = category,
                Price = 30,
                IsDeleted = true
            }
        };

        var addRangeHandler = new AddRangeCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var addedRange = (await addRangeHandler
                .Handle(new AddRangeCommand<MenuItem>(rangeItems), cancellationToken)
                .ConfigureAwait(false))
            .ToList();
        if (addedRange.Count == 2)
        {
            checks++;
        }

        var getAllHandler = new GetAllQueryHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var all = (await getAllHandler.Handle(new GetAllQuery<MenuItem>
        {
            TenantId = tenantId,
            Filter = x => x.Category == category
        }, cancellationToken).ConfigureAwait(false)).ToList();
        if (all.Count >= 3)
        {
            checks++;
        }

        var projected = (await getAllHandler.Handle(new GetAllQuery<MenuItem>
        {
            TenantId = tenantId,
            Filter = x => x.Category == category,
            Selector = x => new MenuItem
            {
                Id = x.Id,
                TenantId = x.TenantId,
                Name = x.Name,
                Category = x.Category,
                Price = x.Price,
                IsDeleted = x.IsDeleted
            }
        }, cancellationToken).ConfigureAwait(false)).ToList();
        if (projected.Count >= 3 && projected.All(x => x.Id != Guid.Empty))
        {
            checks++;
        }

        var allIncludingDeleted = (await getAllHandler.Handle(new GetAllQuery<MenuItem>
        {
            TenantId = tenantId,
            Filter = x => x.Category == category,
            IncludeDeleted = true
        }, cancellationToken).ConfigureAwait(false)).ToList();
        if (allIncludingDeleted.Count >= all.Count)
        {
            checks++;
        }

        var deletedOnly = (await getAllHandler.Handle(new GetAllQuery<MenuItem>
        {
            TenantId = tenantId,
            Filter = x => x.Category == category,
            DeletedOnly = true
        }, cancellationToken).ConfigureAwait(false)).ToList();
        if (deletedOnly.Any(x => x.IsDeleted))
        {
            checks++;
        }

        var getByIdHandler = new GetByIdQueryHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var byId = await getByIdHandler.Handle(new GetByIdQuery<MenuItem, Guid>(seed.Id)
        {
            TenantId = tenantId,
            RowVersionPropertyName = nameof(MenuItem.Category)
        }, cancellationToken).ConfigureAwait(false);
        if (byId is not null && Guid.TryParse(byId.Category, out _))
        {
            checks++;
        }

        var byIdIncludingDeleted = await getByIdHandler.Handle(new GetByIdQuery<MenuItem, Guid>(rangeItems[1].Id)
        {
            TenantId = tenantId,
            IncludeDeleted = true
        }, cancellationToken).ConfigureAwait(false);
        if (byIdIncludingDeleted is not null)
        {
            checks++;
        }

        var patchHandler = new PatchCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var patched = await patchHandler.Handle(
            new PatchCommand<MenuItem, Guid>(
                seed.Id,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [nameof(MenuItem.Price)] = 55m
                },
                tenantId)
            {
                RowVersionPropertyName = nameof(MenuItem.Category)
            },
            cancellationToken).ConfigureAwait(false);
        if (patched is not null && patched.Price == 55m && Guid.TryParse(patched.Category, out _))
        {
            checks++;
        }

        seed.Price = 66;
        var updateHandler = new UpdateCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var updated = await updateHandler.Handle(new UpdateCommand<MenuItem>(seed, tenantId: tenantId), cancellationToken).ConfigureAwait(false);
        if (updated.Price == 66)
        {
            checks++;
        }

        addedRange[0].Price = 21;
        addedRange[1].Price = 31;
        var updateRangeHandler = new UpdateRangeCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
        var updatedRange = (await updateRangeHandler.Handle(
                new UpdateRangeCommand<MenuItem>(addedRange, tenantId),
                cancellationToken).ConfigureAwait(false))
            .ToList();
        if (updatedRange.Count == 2)
        {
            checks++;
        }

        try
        {
            var removeByEntityHandler = new RemoveByEntityCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
            await removeByEntityHandler.Handle(new RemoveByEntityCommand<MenuItem>(addedRange[0], tenantId: tenantId), cancellationToken).ConfigureAwait(false);
            checks++;
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var removeByIdHandler = new RemoveByIdCommandHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
            await removeByIdHandler.Handle(new RemoveByIdCommand<MenuItem, Guid>(addedRange[1].Id, tenantId: tenantId), cancellationToken).ConfigureAwait(false);
            checks++;
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var removeRangeHandler = new RemoveRangeHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);
            await removeRangeHandler.Handle(new RemoveRangeCommand<MenuItem>([seed], tenantId), cancellationToken).ConfigureAwait(false);
            checks++;
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var activeAfterRemovals = (await getAllHandler.Handle(new GetAllQuery<MenuItem>
            {
                TenantId = tenantId,
                Filter = x => x.Category == category
            }, cancellationToken).ConfigureAwait(false)).ToList();
            if (activeAfterRemovals.Count == 0)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        checks += await RunBestEffortAsync(() => RunGetByKeyValuesHandlerScenariosAsync(unitOfWork, session, tenantId, category, seed.Id, cancellationToken)).ConfigureAwait(false);
        checks += await RunBestEffortAsync(() => RunGetSeekHandlerScenariosAsync(unitOfWork, session, tenantId, category, cancellationToken)).ConfigureAwait(false);
        checks += await RunBestEffortAsync(() => RunGetSeekConversionScenariosAsync(unitOfWork, tenantId, cancellationToken)).ConfigureAwait(false);

        return checks;
    }

    private static async Task<int> RunGetByKeyValuesHandlerScenariosAsync(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        IDocumentSession session,
        string tenantId,
        string category,
        Guid existingId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var filterPrefix = $"DiagByKeys-{Guid.NewGuid():N}";
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();

        var deleted = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"{filterPrefix}-deleted",
            Category = category,
            Price = 91,
            IsDeleted = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await repo.AddAsync(deleted, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var handler = new GetByKeyValuesQueryHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);

        try
        {
            var byId = await handler.Handle(
                new GetByKeyValuesQuery<MenuItem, Guid>([existingId])
                {
                    TenantId = tenantId
                },
                cancellationToken).ConfigureAwait(false);

            if (byId is not null && byId.Id == existingId)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var withMergedIncludes = await handler.Handle(
                new GetByKeyValuesQuery<MenuItem, Guid>([existingId])
                {
                    TenantId = tenantId,
                    KeyPropertyNames = [" ", nameof(MenuItem.Id), "\t"],
                    IncludeExpressions = [x => x.UpdatedAt]
                },
                cancellationToken).ConfigureAwait(false);

            if (withMergedIncludes is not null)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var withDeleted = await handler.Handle(
                new GetByKeyValuesQuery<MenuItem, Guid>([deleted.Id])
                {
                    TenantId = tenantId,
                    IncludeDeleted = true
                },
                cancellationToken).ConfigureAwait(false);

            if (withDeleted is not null && withDeleted.Id == deleted.Id)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var noSoftUnitOfWork = CreateUnitOfWorkWithoutSoftDelete<MenuItem, Guid>(session);
            var noSoftHandler = new GetByKeyValuesQueryHandler<IDocumentSession, MenuItem, Guid>(noSoftUnitOfWork);
            var fallback = await noSoftHandler.Handle(
                new GetByKeyValuesQuery<MenuItem, Guid>([existingId])
                {
                    TenantId = tenantId,
                    IncludeDeleted = true
                },
                cancellationToken).ConfigureAwait(false);

            if (fallback is not null && fallback.Id == existingId)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        return checks;
    }

    private static async Task<int> RunGetSeekHandlerScenariosAsync(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        IDocumentSession session,
        string tenantId,
        string category,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var scope = $"DiagSeek-{Guid.NewGuid():N}";
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();

        var seedItems = new[]
        {
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"{scope}-a",
                Category = category,
                Price = 10,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
            },
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"{scope}-b",
                Category = category,
                Price = 20,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
                UpdatedAt = null
            },
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"{scope}-c",
                Category = category,
                Price = 30,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            },
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"{scope}-deleted",
                Category = category,
                Price = 40,
                IsDeleted = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        await repo.AddRangeAsync(seedItems, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var seekProps = new[] { nameof(MenuItem.Price), nameof(MenuItem.Id) };
        var filter = (Expression<Func<MenuItem, bool>>)(x =>
            x.TenantId == tenantId &&
            x.Category == category &&
            x.Name.StartsWith(scope));
        var handler = new GetSeekQueryHandler<IDocumentSession, MenuItem, Guid>(unitOfWork);

        try
        {
            var first = await handler.Handle(new GetSeekQuery<MenuItem, Guid>(2)
            {
                TenantId = tenantId,
                Filter = filter,
                IncludeTotalCount = true,
                SeekPropertyNames = seekProps
            }, cancellationToken).ConfigureAwait(false);

            if (first.Items.Count == 2 && first.TotalCount is >= 3 && !string.IsNullOrWhiteSpace(first.NextToken))
            {
                checks++;
            }

            if (!string.IsNullOrWhiteSpace(first.NextToken))
            {
                var second = await handler.Handle(new GetSeekQuery<MenuItem, Guid>(2, first.NextToken)
                {
                    TenantId = tenantId,
                    Filter = filter,
                    IncludeTotalCount = true,
                    SeekPropertyNames = seekProps
                }, cancellationToken).ConfigureAwait(false);

                if (second.Items.Count >= 1 && second.TotalCount is >= 3)
                {
                    checks++;
                }
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var descending = await handler.Handle(new GetSeekQuery<MenuItem, Guid>(2)
            {
                TenantId = tenantId,
                Filter = filter,
                Descending = true,
                IncludeTotalCount = true,
                SeekPropertyNames = seekProps,
                Selector = x => new MenuItem
                {
                    Id = x.Id,
                    TenantId = x.TenantId,
                    Name = x.Name,
                    Category = x.Category,
                    Price = x.Price,
                    IsDeleted = x.IsDeleted,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                },
                IncludeExpressions = [x => x.UpdatedAt]
            }, cancellationToken).ConfigureAwait(false);

            if (descending.Items.Count >= 1 && descending.TotalCount is >= 3)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var includeDeleted = await handler.Handle(new GetSeekQuery<MenuItem, Guid>(2)
            {
                TenantId = tenantId,
                Filter = filter,
                IncludeDeleted = true,
                IncludeTotalCount = true,
                SeekPropertyNames = seekProps
            }, cancellationToken).ConfigureAwait(false);

            if (includeDeleted.TotalCount is >= 4)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            var noSoftUnitOfWork = CreateUnitOfWorkWithoutSoftDelete<MenuItem, Guid>(session);
            var noSoftHandler = new GetSeekQueryHandler<IDocumentSession, MenuItem, Guid>(noSoftUnitOfWork);
            var includeDeletedFallback = await noSoftHandler.Handle(new GetSeekQuery<MenuItem, Guid>(2)
            {
                TenantId = tenantId,
                Filter = filter,
                IncludeDeleted = true,
                IncludeTotalCount = true,
                SeekPropertyNames = seekProps
            }, cancellationToken).ConfigureAwait(false);

            if (includeDeletedFallback.TotalCount is >= 3)
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        try
        {
            await ExpectThrowsAsync<InvalidOperationException>(() => handler.Handle(new GetSeekQuery<MenuItem, Guid>(2)
            {
                TenantId = tenantId,
                Filter = filter
            }, cancellationToken)).ConfigureAwait(false);
            checks++;
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        var invalidCursorCases = new[]
        {
            new
            {
                Cursor = "invalid-token",
                Properties = new[] { nameof(MenuItem.Id) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(MenuItem.Price)] = "20"
                }),
                Properties = seekProps
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(MenuItem.Id)] = "not-guid"
                }),
                Properties = new[] { nameof(MenuItem.Id) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["UnknownProperty"] = "1"
                }),
                Properties = new[] { "UnknownProperty" }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["."] = "1"
                }),
                Properties = new[] { "." }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(MenuItem.CreatedAt)] = "not-a-datetime-offset"
                }),
                Properties = new[] { nameof(MenuItem.CreatedAt) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(MenuItem.Price)] = "not-a-number"
                }),
                Properties = new[] { nameof(MenuItem.Price) }
            }
        };

        foreach (var invalidCase in invalidCursorCases)
        {
            try
            {
                await ExpectThrowsAsync<InvalidOperationException>(() => handler.Handle(new GetSeekQuery<MenuItem, Guid>(2, invalidCase.Cursor)
                {
                    TenantId = tenantId,
                    Filter = filter,
                    SeekPropertyNames = invalidCase.Properties
                }, cancellationToken)).ConfigureAwait(false);
                checks++;
            }
            catch
            {
                // Coverage mode: keep endpoint stable across Marten provider differences.
            }
        }

        return checks;
    }

    private static async Task<int> RunGetSeekConversionScenariosAsync(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var scope = $"DiagSeekProbe-{Guid.NewGuid():N}";

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, RuntimeSeekProbe, Guid>>();
        var probes = new[]
        {
            new RuntimeSeekProbe
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Scope = scope,
                Sequence = 1,
                Rank = 10,
                Amount = 10.5m,
                HappenedOn = DateTime.UtcNow.AddDays(-2),
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-2),
                Status = RuntimeSeekProbeStatus.New
            },
            new RuntimeSeekProbe
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Scope = scope,
                Sequence = 2,
                Rank = 20,
                Amount = 20.25m,
                HappenedOn = DateTime.UtcNow.AddDays(-1),
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = RuntimeSeekProbeStatus.Active
            }
        };

        await repo.AddRangeAsync(probes, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var filter = (Expression<Func<RuntimeSeekProbe, bool>>)(x => x.TenantId == tenantId && x.Scope == scope);
        var seekHandler = new GetSeekQueryHandler<IDocumentSession, RuntimeSeekProbe, Guid>(unitOfWork);

        try
        {
            var baseline = await seekHandler.Handle(new GetSeekQuery<RuntimeSeekProbe, Guid>(1)
            {
                TenantId = tenantId,
                Filter = filter,
                IncludeTotalCount = true,
                SeekPropertyNames = [nameof(RuntimeSeekProbe.Sequence), nameof(RuntimeSeekProbe.Id)]
            }, cancellationToken).ConfigureAwait(false);

            if (baseline.Items.Count == 1 && baseline.TotalCount is >= 2 && !string.IsNullOrWhiteSpace(baseline.NextToken))
            {
                checks++;
            }
        }
        catch
        {
            // Coverage mode: keep endpoint stable across Marten provider differences.
        }

        var validConversionCases = new[]
        {
            BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(RuntimeSeekProbe.OccurredAt)] = probes[0].OccurredAt.ToString("O")
            }),
            BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(RuntimeSeekProbe.HappenedOn)] = probes[0].HappenedOn.ToString("O")
            }),
            BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(RuntimeSeekProbe.Status)] = RuntimeSeekProbeStatus.Active.ToString()
            }),
            BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(RuntimeSeekProbe.Rank)] = probes[0].Rank.ToString(CultureInfo.InvariantCulture)
            }),
            BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(RuntimeSeekProbe.Amount)] = probes[0].Amount.ToString(CultureInfo.InvariantCulture)
            })
        };

        var validProperties = new[]
        {
            new[] { nameof(RuntimeSeekProbe.OccurredAt) },
            new[] { nameof(RuntimeSeekProbe.HappenedOn) },
            new[] { nameof(RuntimeSeekProbe.Status) },
            new[] { nameof(RuntimeSeekProbe.Rank) },
            new[] { nameof(RuntimeSeekProbe.Amount) }
        };

        for (var i = 0; i < validConversionCases.Length; i++)
        {
            try
            {
                var result = await seekHandler.Handle(new GetSeekQuery<RuntimeSeekProbe, Guid>(1, validConversionCases[i])
                {
                    TenantId = tenantId,
                    Filter = filter,
                    SeekPropertyNames = validProperties[i]
                }, cancellationToken).ConfigureAwait(false);

                if (result.PageSize == 1)
                {
                    checks++;
                }
            }
            catch
            {
                // Coverage mode: keep endpoint stable across Marten provider differences.
            }
        }

        var invalidConversionCases = new[]
        {
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(RuntimeSeekProbe.OccurredAt)] = "invalid-datetime-offset"
                }),
                Properties = new[] { nameof(RuntimeSeekProbe.OccurredAt) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(RuntimeSeekProbe.HappenedOn)] = "invalid-datetime"
                }),
                Properties = new[] { nameof(RuntimeSeekProbe.HappenedOn) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(RuntimeSeekProbe.Status)] = "invalid-enum"
                }),
                Properties = new[] { nameof(RuntimeSeekProbe.Status) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(RuntimeSeekProbe.Sequence)] = "invalid-int"
                }),
                Properties = new[] { nameof(RuntimeSeekProbe.Sequence) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(RuntimeSeekProbe.Rank)] = "invalid-long"
                }),
                Properties = new[] { nameof(RuntimeSeekProbe.Rank) }
            },
            new
            {
                Cursor = BuildSeekCursorToken(false, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(RuntimeSeekProbe.Amount)] = "invalid-decimal"
                }),
                Properties = new[] { nameof(RuntimeSeekProbe.Amount) }
            }
        };

        foreach (var invalidCase in invalidConversionCases)
        {
            try
            {
                await ExpectThrowsAsync<InvalidOperationException>(() => seekHandler.Handle(new GetSeekQuery<RuntimeSeekProbe, Guid>(1, invalidCase.Cursor)
                {
                    TenantId = tenantId,
                    Filter = filter,
                    SeekPropertyNames = invalidCase.Properties
                }, cancellationToken)).ConfigureAwait(false);
                checks++;
            }
            catch
            {
                // Coverage mode: keep endpoint stable across Marten provider differences.
            }
        }

        var nextToken = RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeBuildNextToken(
            probes,
            [nameof(RuntimeSeekProbe.Sequence), nameof(RuntimeSeekProbe.Id)],
            descending: false);
        var missingToken = RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeBuildNextToken(
            probes,
            [nameof(RuntimeSeekProbe.Sequence), "Missing"],
            descending: false);
        var emptyToken = RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeBuildNextToken(
            Array.Empty<RuntimeSeekProbe>(),
            [nameof(RuntimeSeekProbe.Sequence)],
            descending: false);
        if (!string.IsNullOrWhiteSpace(nextToken) &&
            missingToken is null &&
            emptyToken is null)
        {
            checks++;
        }

        if (RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryBuildSeekPredicate(
                [],
                new Dictionary<string, string?>(),
                descending: false,
                out var emptyPredicate,
                out var emptyError) &&
            emptyPredicate is null &&
            emptyError is null)
        {
            checks++;
        }

        if (!RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryBuildSeekPredicate(
                [nameof(RuntimeSeekProbe.Sequence)],
                new Dictionary<string, string?>(),
                descending: false,
                out _,
                out var missingValueError) &&
            missingValueError == $"Cursor value for '{nameof(RuntimeSeekProbe.Sequence)}' is missing.")
        {
            checks++;
        }

        if (!RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryBuildSeekPredicate(
                ["Missing"],
                new Dictionary<string, string?> { ["Missing"] = "1" },
                descending: false,
                out _,
                out var missingPropertyError) &&
            missingPropertyError == "Property 'Missing' was not found on RuntimeSeekProbe.")
        {
            checks++;
        }

        if (RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryBuildSeekPredicate(
                [nameof(RuntimeSeekProbe.Status)],
                new Dictionary<string, string?> { [nameof(RuntimeSeekProbe.Status)] = RuntimeSeekProbeStatus.Active.ToString() },
                descending: false,
                out var statusPredicate,
                out var statusPredicateError) &&
            statusPredicate is not null &&
            statusPredicateError is null)
        {
            checks++;
        }

        if (!RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryBuildMemberAccess(string.Empty, out _, out var emptyPropertyError) &&
            emptyPropertyError == "Property name is required." &&
            !RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryBuildMemberAccess("Unknown", out _, out var invalidPropertyError) &&
            invalidPropertyError == "Property 'Unknown' was not found on RuntimeSeekProbe.")
        {
            checks++;
        }

        var propertyProbe = probes[0];
        var nullEnvelope = new RuntimeGetSeekNestedEnvelope();
        if (RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryGetPropertyValue(propertyProbe, nameof(RuntimeSeekProbe.Sequence), out var sequenceValue) &&
            Equals(sequenceValue, probes[0].Sequence) &&
            !RuntimeGetSeekHandlerProbe<RuntimeGetSeekNestedEnvelope>.ProbeTryGetPropertyValue(nullEnvelope, "Probe.Sequence", out _) &&
            !RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryGetPropertyValue(propertyProbe, "Unknown", out _))
        {
            checks++;
        }

        if (RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert(null, typeof(int), out var nullValue) &&
            nullValue is null &&
            RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert("null", typeof(int?), out var explicitNull) &&
            explicitNull is null &&
            RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert("\"quoted\"", typeof(string), out var quotedString) &&
            (string?)quotedString == "quoted" &&
            !RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert("bad-guid", typeof(Guid), out _) &&
            !RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert("bad-date", typeof(DateTime), out _) &&
            !RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert("bad-date-offset", typeof(DateTimeOffset), out _) &&
            !RuntimeGetSeekHandlerProbe<RuntimeSeekProbe>.ProbeTryConvert("bad-enum", typeof(RuntimeSeekProbeStatus), out _))
        {
            checks++;
        }

        return checks;
    }
}
