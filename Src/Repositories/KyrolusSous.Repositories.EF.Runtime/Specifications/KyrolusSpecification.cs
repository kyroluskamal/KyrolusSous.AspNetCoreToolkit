using KyrolusSous.Repositories.EF.Abstractions.Specifications;

namespace KyrolusSous.Repositories.EF.Runtime.Specifications;

/// <summary>
/// Fluent base class for building strongly-typed composable specifications.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class KyrolusSpecification<TEntity> : IKyrolusSpecification<TEntity>
    where TEntity : class
{
    private readonly List<Expression<Func<TEntity, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];

    public Expression<Func<TEntity, bool>>? Criteria { get; private set; }
    public IReadOnlyList<Expression<Func<TEntity, object>>> Includes => _includes;
    public IReadOnlyList<string> IncludeStrings => _includeStrings;
    public Expression<Func<TEntity, object>>? OrderBy { get; private set; }
    public Expression<Func<TEntity, object>>? OrderByDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }
    public bool IsSplitQuery { get; private set; }
    public bool IsNoTracking { get; private set; }

    public KyrolusSpecification() { }

    public KyrolusSpecification(Expression<Func<TEntity, bool>> criteria)
    {
        Criteria = criteria;
    }

    public KyrolusSpecification<TEntity> Where(Expression<Func<TEntity, bool>> criteria)
    {
        Criteria = Criteria is null ? criteria : CombineAnd(Criteria, criteria);
        return this;
    }

    public KyrolusSpecification<TEntity> AddInclude(Expression<Func<TEntity, object>> includeExpression)
    {
        _includes.Add(includeExpression);
        return this;
    }

    public KyrolusSpecification<TEntity> AddInclude(string includeString)
    {
        if (!string.IsNullOrWhiteSpace(includeString))
        {
            _includeStrings.Add(includeString);
        }
        return this;
    }

    public KyrolusSpecification<TEntity> ApplyOrderBy(Expression<Func<TEntity, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
        OrderByDescending = null;
        return this;
    }

    public KyrolusSpecification<TEntity> ApplyOrderByDescending(Expression<Func<TEntity, object>> orderByDescendingExpression)
    {
        OrderByDescending = orderByDescendingExpression;
        OrderBy = null;
        return this;
    }

    public KyrolusSpecification<TEntity> ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
        return this;
    }

    public KyrolusSpecification<TEntity> AsSplitQuery()
    {
        IsSplitQuery = true;
        return this;
    }

    public KyrolusSpecification<TEntity> AsNoTracking()
    {
        IsNoTracking = true;
        return this;
    }

    public KyrolusSpecification<TEntity> And(IKyrolusSpecification<TEntity> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Criteria is not null)
        {
            Criteria = Criteria is null ? other.Criteria : CombineAnd(Criteria, other.Criteria);
        }
        _includes.AddRange(other.Includes);
        _includeStrings.AddRange(other.IncludeStrings);
        return this;
    }

    public KyrolusSpecification<TEntity> Or(IKyrolusSpecification<TEntity> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Criteria is not null)
        {
            Criteria = Criteria is null ? other.Criteria : CombineOr(Criteria, other.Criteria);
        }
        _includes.AddRange(other.Includes);
        _includeStrings.AddRange(other.IncludeStrings);
        return this;
    }

    private static Expression<Func<TEntity, bool>> CombineAnd(
        Expression<Func<TEntity, bool>> left,
        Expression<Func<TEntity, bool>> right)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], param);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], param);
        return Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(leftBody, rightBody), param);
    }

    private static Expression<Func<TEntity, bool>> CombineOr(
        Expression<Func<TEntity, bool>> left,
        Expression<Func<TEntity, bool>> right)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], param);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], param);
        return Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(leftBody, rightBody), param);
    }

    private static Expression ReplaceParameter(Expression body, ParameterExpression toReplace, ParameterExpression replacement)
    {
        return new ParameterReplacer(toReplace, replacement).Visit(body);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}

/// <summary>
/// Evaluates and applies specifications onto an <see cref="IQueryable{TEntity}"/>.
/// </summary>
public static class KyrolusSpecificationEvaluator
{
    public static IQueryable<TEntity> GetQuery<TEntity>(
        IQueryable<TEntity> inputQuery,
        IKyrolusSpecification<TEntity> specification)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(inputQuery);
        ArgumentNullException.ThrowIfNull(specification);

        var query = inputQuery;

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        query = specification.Includes.Aggregate(query, static (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, static (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        if (specification.IsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        if (specification.IsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification.IsPagingEnabled)
        {
            if (specification.Skip.HasValue)
            {
                query = query.Skip(specification.Skip.Value);
            }

            if (specification.Take.HasValue)
            {
                query = query.Take(specification.Take.Value);
            }
        }

        return query;
    }
}
