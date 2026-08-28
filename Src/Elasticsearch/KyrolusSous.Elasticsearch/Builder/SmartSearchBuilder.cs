namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Fluent query builder for building complex, optimized Elasticsearch queries.
/// </summary>
public class KyrolusSmartSearchBuilder<TDocument> where TDocument : class
{
    private string? _queryText;
    private readonly List<string> _searchFields = [];
    private string _fuzziness = "AUTO";
    private int _fuzzyPrefixLength;
    private readonly List<Action<QueryDescriptor<TDocument>>> _filterActions = [];
    private readonly List<Action<QueryDescriptor<TDocument>>> _shouldActions = [];
    private readonly List<SortOptions> _sortOptions = [];
    private int _from;
    private int _size = 10;
    private float? _minScore;
    private readonly List<string> _highlightFields = [];

    public KyrolusSmartSearchBuilder<TDocument> Search(string queryText, params Expression<Func<TDocument, object>>[] fields)
    {
        _queryText = queryText;
        foreach (var field in fields)
        {
            var name = ExpressionHelper.GetPropertyName(field);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _searchFields.Add(name);
            }
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Search(string queryText, params string[] fields)
    {
        _queryText = queryText;
        _searchFields.AddRange(fields.Where(f => !string.IsNullOrWhiteSpace(f)));
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Fuzzy(string fuzziness = "AUTO", int prefixLength = 0)
    {
        _fuzziness = fuzziness;
        _fuzzyPrefixLength = prefixLength;
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Highlight(params Expression<Func<TDocument, object>>[] fields)
    {
        foreach (var f in fields)
        {
            var name = ExpressionHelper.GetPropertyName(f);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _highlightFields.Add(name);
            }
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Filter<TValue>(Expression<Func<TDocument, TValue>> field, TValue value)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name) && value is not null)
        {
            var valStr = value.ToString()!;
            _filterActions.Add(q => q.Term(t => t.Field(new Field(name)).Value(valStr)));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> FilterIn<TValue>(Expression<Func<TDocument, TValue>> field, IEnumerable<TValue> values)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        var valStrings = values.Where(v => v is not null).Select(v => (FieldValue)v!.ToString()!).ToList();
        if (!string.IsNullOrWhiteSpace(name) && valStrings.Count > 0)
        {
            _filterActions.Add(q => q.Terms(t => t.Field(new Field(name)).Terms(new TermsQueryField(valStrings))));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Range<TValue>(
        Expression<Func<TDocument, TValue>> field,
        double? min = null,
        double? max = null)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _filterActions.Add(q => q.Range(r => r.NumberRange(nr =>
            {
                nr.Field(new Field(name));
                if (min.HasValue)
                {
                    nr.Gte(min.Value);
                }
                if (max.HasValue)
                {
                    nr.Lte(max.Value);
                }
            })));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> DateRange<TValue>(
        Expression<Func<TDocument, TValue>> field,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _filterActions.Add(q => q.Range(r => r.DateRange(dr =>
            {
                dr.Field(new Field(name));
                if (from.HasValue) dr.Gte(from.Value.ToString("o"));
                if (to.HasValue) dr.Lte(to.Value.ToString("o"));
            })));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> GeoDistance<TValue>(
        Expression<Func<TDocument, TValue>> field,
        double latitude,
        double longitude,
        double distanceKm)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _filterActions.Add(q => q.GeoDistance(g => g
                .Field(new Field(name))
                .Location(GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                {
                    Lat = latitude,
                    Lon = longitude
                }))
                .Distance($"{distanceKm}km")));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> BoostWhen<TValue>(
        Expression<Func<TDocument, TValue>> field,
        TValue matchValue,
        float boost)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name) && matchValue is not null)
        {
            var valStr = matchValue.ToString()!;
            _shouldActions.Add(q => q.Term(t => t
                .Field(new Field(name))
                .Value(valStr)
                .Boost(boost)));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> OrderBy<TValue>(
        Expression<Func<TDocument, TValue>> field,
        bool descending = false)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _sortOptions.Add(SortOptions.Field(new Field(name), new FieldSort
            {
                Order = descending ? SortOrder.Desc : SortOrder.Asc
            }));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> OrderByScore()
    {
        _sortOptions.Add(SortOptions.Score(new ScoreSort()));
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Paginate(int page, int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        _from = (page - 1) * pageSize;
        _size = pageSize;
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> MinScore(float score)
    {
        _minScore = score;
        return this;
    }

    public void Apply(SearchRequestDescriptor<TDocument> descriptor)
    {
        descriptor.From(_from);
        descriptor.Size(_size);

        if (_minScore.HasValue)
        {
            descriptor.MinScore(_minScore.Value);
        }

        if (_sortOptions.Count > 0)
        {
            descriptor.Sort(_sortOptions);
        }

        if (_highlightFields.Count > 0)
        {
            descriptor.Highlight(h => h.Fields(dict =>
            {
                foreach (var f in _highlightFields)
                {
                    dict.Add(new Field(f), _ => { });
                }
                return dict;
            }));
        }

        var mustQueries = new List<Action<QueryDescriptor<TDocument>>>();

        if (!string.IsNullOrWhiteSpace(_queryText))
        {
            if (_searchFields.Count == 1)
            {
                var fieldName = _searchFields[0];
                mustQueries.Add(q => q.Match(m => m
                    .Field(new Field(fieldName))
                    .Query(_queryText)
                    .Fuzziness(new Fuzziness(_fuzziness))
                    .PrefixLength(_fuzzyPrefixLength)));
            }
            else if (_searchFields.Count > 1)
            {
                var fieldList = _searchFields.Select(f => new Field(f)).ToArray();
                mustQueries.Add(q => q.MultiMatch(mm => mm
                    .Fields(fieldList)
                    .Query(_queryText)
                    .Fuzziness(new Fuzziness(_fuzziness))
                    .PrefixLength(_fuzzyPrefixLength)));
            }
            else
            {
                mustQueries.Add(q => q.QueryString(qs => qs
                    .Query(_queryText)
                    .Fuzziness(new Fuzziness(_fuzziness))));
            }
        }

        descriptor.Query(q => q.Bool(b =>
        {
            if (mustQueries.Count > 0)
            {
                b.Must(mustQueries.Select(action => (Action<QueryDescriptor<TDocument>>)(qd => action(qd))).ToArray());
            }

            if (_filterActions.Count > 0)
            {
                b.Filter(_filterActions.Select(action => (Action<QueryDescriptor<TDocument>>)(qd => action(qd))).ToArray());
            }

            if (_shouldActions.Count > 0)
            {
                b.Should(_shouldActions.Select(action => (Action<QueryDescriptor<TDocument>>)(qd => action(qd))).ToArray());
            }
        }));
    }
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusSmartSearchBuilder{TDocument}"/>.
/// </summary>
public sealed class SmartSearchBuilder<TDocument> : KyrolusSmartSearchBuilder<TDocument> where TDocument : class
{
}
