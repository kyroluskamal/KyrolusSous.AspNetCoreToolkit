namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;

public sealed record TestFilterClause(string Property, string Operator, string? Value);

public sealed record TestOrderClause(string Property, bool Desc = false);

public sealed record TestQueryRequest(
    TestFilterClause[]? Filters = null,
    TestOrderClause[]? OrderBy = null,
    string[]? IncludeProperties = null,
    object? IncludeGraph = null,
    string[]? Fields = null,
    bool? AsNoTracking = null,
    bool? UseSplitQuery = null);

public sealed record TestPagedQueryRequest(
    TestQueryRequest? Request,
    int? PageNumber = null,
    int? PageSize = null,
    bool? Cacheable = null,
    bool? IncludeDeleted = null);

public sealed record TestSeekQueryRequest(
    TestQueryRequest? Request,
    int? PageSize = null,
    string? Cursor = null,
    bool? Cacheable = null,
    bool? IncludeDeleted = null,
    bool? IncludeTotalCount = null,
    bool? Descending = null);

public sealed record TestFilterBuilderDiagnosticsRequest(
    string? Filter = null,
    TestFilterClause[]? Clauses = null,
    string[]? AllowedProperties = null,
    bool? Strict = null,
    bool? CaseInsensitive = null);
