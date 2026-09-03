using System.Linq.Expressions;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Specification for a search suggester (Phrase, Term, Completion).
/// </summary>
public sealed record KyrolusSuggesterSpec(
    string Name,
    string Type,
    string Text,
    string Field,
    double Confidence = 0.0,
    int MaxErrors = 2,
    bool Fuzzy = false);

/// <summary>
/// Fluent query builder for building complex, optimized Elasticsearch queries, aggregations, suggestions, routing, and filters.
/// </summary>
public sealed class KyrolusSmartSearchBuilder<TDocument> where TDocument : class
{
    private string? _queryText;
    private readonly List<string> _searchFields = [];
    private string _fuzziness = "AUTO";
    private int _fuzzyPrefixLength;
    private readonly List<Action<QueryDescriptor<TDocument>>> _mustActions = [];
    private readonly List<Action<QueryDescriptor<TDocument>>> _filterActions = [];
    private readonly List<Action<QueryDescriptor<TDocument>>> _shouldActions = [];
    private readonly List<Action<QueryDescriptor<TDocument>>> _mustNotActions = [];
    private readonly List<SortOptions> _sortOptions = [];
    private readonly Dictionary<string, Action<AggregationDescriptor<TDocument>>> _aggregationActions = [];
    private readonly Dictionary<string, KyrolusSuggesterSpec> _suggesters = [];
    private int _from;
    private int _size = 10;
    private float? _minScore;
    private readonly List<string> _highlightFields = [];
    private string? _routing;
    private int? _minimumShouldMatch;

    public IReadOnlyDictionary<string, KyrolusSuggesterSpec> Suggesters => _suggesters;

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

    public KyrolusSmartSearchBuilder<TDocument> Routing(string routingKey)
    {
        _routing = routingKey;
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

    public KyrolusSmartSearchBuilder<TDocument> Highlight(params string[] fields)
    {
        _highlightFields.AddRange(fields.Where(f => !string.IsNullOrWhiteSpace(f)));
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

    public KyrolusSmartSearchBuilder<TDocument> Filter(Action<QueryDescriptor<TDocument>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _filterActions.Add(query);
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Must(Action<QueryDescriptor<TDocument>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _mustActions.Add(query);
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> Should(Action<QueryDescriptor<TDocument>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _shouldActions.Add(query);
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> MustNot(Action<QueryDescriptor<TDocument>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _mustNotActions.Add(query);
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> MinimumShouldMatch(int count)
    {
        _minimumShouldMatch = count;
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

    public KyrolusSmartSearchBuilder<TDocument> Nested<TValue>(
        Expression<Func<TDocument, TValue>> pathField,
        Action<QueryDescriptor<TDocument>> nestedQuery)
    {
        var name = ExpressionHelper.GetPropertyName(pathField);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _filterActions.Add(q => q.Nested(n => n
                .Path(new Field(name))
                .Query(nestedQuery)));
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

    public KyrolusSmartSearchBuilder<TDocument> OrderBy(string fieldName, bool descending = false)
    {
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _sortOptions.Add(SortOptions.Field(new Field(fieldName), new FieldSort
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

    #region Aggregations

    public KyrolusSmartSearchBuilder<TDocument> TermsAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field,
        int size = 10)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.Terms(t => t.Field(new Field(fieldName)).Size(size));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> DateHistogramAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field,
        CalendarInterval interval,
        string? format = null)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.DateHistogram(dh =>
            {
                dh.Field(new Field(fieldName)).CalendarInterval(interval);
                if (!string.IsNullOrWhiteSpace(format)) dh.Format(format);
            });
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> HistogramAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field,
        double interval)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.Histogram(h => h.Field(new Field(fieldName)).Interval(interval));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> StatsAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.Stats(s => s.Field(new Field(fieldName)));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> ExtendedStatsAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.ExtendedStats(es => es.Field(new Field(fieldName)));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> CardinalityAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field,
        int precisionThreshold = 3000)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.Cardinality(c => c.Field(new Field(fieldName)).PrecisionThreshold(precisionThreshold));
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> PercentilesAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field,
        params double[] percents)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.Percentiles(p =>
            {
                p.Field(new Field(fieldName));
                if (percents.Length > 0)
                {
                    p.Percents(percents);
                }
            });
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> RangeAggregation<TValue>(
        string name,
        Expression<Func<TDocument, TValue>> field,
        params (double? From, double? To)[] ranges)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            _aggregationActions[name] = a => a.Range(r =>
            {
                r.Field(new Field(fieldName));
                r.Ranges(ranges.Select(rg =>
                {
                    var item = new AggregationRange();
                    if (rg.From.HasValue) item.From = rg.From.Value;
                    if (rg.To.HasValue) item.To = rg.To.Value;
                    return item;
                }).ToArray());
            });
        }
        return this;
    }

    #endregion

    #region Suggestions

    public KyrolusSmartSearchBuilder<TDocument> SuggestPhrase<TValue>(
        string name,
        string text,
        Expression<Func<TDocument, TValue>> field,
        double confidence = 0.0,
        int maxErrors = 2)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(text))
        {
            _suggesters[name] = new KyrolusSuggesterSpec(name, "phrase", text, fieldName, confidence, maxErrors);
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> SuggestTerm<TValue>(
        string name,
        string text,
        Expression<Func<TDocument, TValue>> field)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(text))
        {
            _suggesters[name] = new KyrolusSuggesterSpec(name, "term", text, fieldName);
        }
        return this;
    }

    public KyrolusSmartSearchBuilder<TDocument> SuggestCompletion<TValue>(
        string name,
        string prefix,
        Expression<Func<TDocument, TValue>> field,
        bool fuzzy = false)
    {
        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(prefix))
        {
            _suggesters[name] = new KyrolusSuggesterSpec(name, "completion", prefix, fieldName, Fuzzy: fuzzy);
        }
        return this;
    }

    #endregion

    public void Apply(SearchRequestDescriptor<TDocument> descriptor)
    {
        descriptor.From(_from);
        descriptor.Size(_size);

        if (!string.IsNullOrWhiteSpace(_routing))
        {
            descriptor.Routing(new Routing(_routing));
        }

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

        if (_aggregationActions.Count > 0)
        {
            descriptor.Aggregations(dict =>
            {
                foreach (var (name, action) in _aggregationActions)
                {
                    dict.Add(name, action);
                }
                return dict;
            });
        }

        var mustQueries = new List<Action<QueryDescriptor<TDocument>>>(_mustActions);

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

            if (_mustNotActions.Count > 0)
            {
                b.MustNot(_mustNotActions.Select(action => (Action<QueryDescriptor<TDocument>>)(qd => action(qd))).ToArray());
            }

            if (_minimumShouldMatch.HasValue)
            {
                b.MinimumShouldMatch(new MinimumShouldMatch(_minimumShouldMatch.Value));
            }
        }));
    }

    public void Apply(DeleteByQueryRequestDescriptor<TDocument> descriptor)
    {
        if (!string.IsNullOrWhiteSpace(_routing))
        {
            descriptor.Routing(new Routing(_routing));
        }

        var mustQueries = new List<Action<QueryDescriptor<TDocument>>>(_mustActions);

        if (!string.IsNullOrWhiteSpace(_queryText))
        {
            if (_searchFields.Count == 1)
            {
                var fieldName = _searchFields[0];
                mustQueries.Add(q => q.Match(m => m.Field(new Field(fieldName)).Query(_queryText)));
            }
            else if (_searchFields.Count > 1)
            {
                var fieldList = _searchFields.Select(f => new Field(f)).ToArray();
                mustQueries.Add(q => q.MultiMatch(mm => mm.Fields(fieldList).Query(_queryText)));
            }
            else
            {
                mustQueries.Add(q => q.QueryString(qs => qs.Query(_queryText)));
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

            if (_mustNotActions.Count > 0)
            {
                b.MustNot(_mustNotActions.Select(action => (Action<QueryDescriptor<TDocument>>)(qd => action(qd))).ToArray());
            }
        }));
    }

    public void Apply(UpdateByQueryRequestDescriptor<TDocument> descriptor)
    {
        if (!string.IsNullOrWhiteSpace(_routing))
        {
            descriptor.Routing(new Routing(_routing));
        }

        var mustQueries = new List<Action<QueryDescriptor<TDocument>>>(_mustActions);

        if (!string.IsNullOrWhiteSpace(_queryText))
        {
            if (_searchFields.Count == 1)
            {
                var fieldName = _searchFields[0];
                mustQueries.Add(q => q.Match(m => m.Field(new Field(fieldName)).Query(_queryText)));
            }
            else if (_searchFields.Count > 1)
            {
                var fieldList = _searchFields.Select(f => new Field(f)).ToArray();
                mustQueries.Add(q => q.MultiMatch(mm => mm.Fields(fieldList).Query(_queryText)));
            }
            else
            {
                mustQueries.Add(q => q.QueryString(qs => qs.Query(_queryText)));
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

            if (_mustNotActions.Count > 0)
            {
                b.MustNot(_mustNotActions.Select(action => (Action<QueryDescriptor<TDocument>>)(qd => action(qd))).ToArray());
            }
        }));
    }
}
