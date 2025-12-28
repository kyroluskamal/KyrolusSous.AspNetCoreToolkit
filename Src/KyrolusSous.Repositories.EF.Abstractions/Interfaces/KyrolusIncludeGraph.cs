using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public sealed class IncludeGraph<TEntity>(params Expression<Func<TEntity, object?>>[] includes)
{
    public IReadOnlyList<Expression<Func<TEntity, object?>>> Includes { get; } = includes ?? [];
}
