namespace KyrolusSous.Elasticsearch;

public sealed class SmartSearchBuilder<TDocument> where TDocument : class
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

    public SmartSearchBuilder<TDocument> Search(string queryText, params Expression<Func<TDocument, object>>[] fields)
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

    public SmartSearchBuilder<TDocument> Search(string queryText, params string[] fields)
    {
        _queryText = queryText;
        _searchFields.AddRange(fields.Where(f => !string.IsNullOrWhiteSpace(f)));
        return this;
    }

    public SmartSearchBuilder<TDocument> Fuzzy(string fuzziness = "AUTO", int prefixLength = 0)
    {
        _fuzziness = fuzziness;
        _fuzzyPrefixLength = prefixLength;
        return this;
    }

    public SmartSearchBuilder<TDocument> Filter<TValue>(Expression<Func<TDocument, TValue>> field, TValue value)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name) && value is not null)
        {
            var valStr = value.ToString()!;
            _filterActions.Add(q => q.Term(t => t.Field(new Field(name)).Value(valStr)));
        }
        return this;
    }

    public SmartSearchBuilder<TDocument> FilterIn<TValue>(Expression<Func<TDocument, TValue>> field, IEnumerable<TValue> values)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        var valStrings = values.Where(v => v is not null).Select(v => (FieldValue)v!.ToString()!).ToList();
        if (!string.IsNullOrWhiteSpace(name) && valStrings.Count > 0)
        {
            _filterActions.Add(q => q.Terms(t => t.Field(new Field(name)).Terms(new TermsQueryField(valStrings))));
        }
        return this;
    }

    public SmartSearchBuilder<TDocument> Range<TValue>(
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

    public SmartSearchBuilder<TDocument> GeoDistance<TValue>(
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
                .Distance($"{distanceKm}km")
                .Location(new LatLonGeoLocation { Lat = latitude, Lon = longitude })));
        }
        return this;
    }

    public SmartSearchBuilder<TDocument> BoostWhen<TValue>(
        Expression<Func<TDocument, TValue>> field,
        TValue matchValue,
        float boost)
    {
        var name = ExpressionHelper.GetPropertyName(field);
        if (!string.IsNullOrWhiteSpace(name) && matchValue is not null)
        {
            var valStr = matchValue.ToString()!;
            _shouldActions.Add(q => q.Term(t => t.Field(new Field(name)).Value(valStr).Boost(boost)));
        }
        return this;
    }

    public SmartSearchBuilder<TDocument> OrderBy<TValue>(Expression<Func<TDocument, TValue>> field, bool descending = false)
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

    public SmartSearchBuilder<TDocument> OrderByRelevance()
    {
        _sortOptions.Add(SortOptions.Score(new ScoreSort()));
        return this;
    }

    public SmartSearchBuilder<TDocument> Paginate(int page, int pageSize)
    {
        _from = Math.Max(0, (page - 1) * pageSize);
        _size = Math.Max(1, pageSize);
        return this;
    }

    public SmartSearchBuilder<TDocument> MinScore(float minScore)
    {
        _minScore = minScore;
        return this;
    }

    public void Apply(SearchRequestDescriptor<TDocument> descriptor)
    {
        descriptor.From(_from).Size(_size);

        if (_minScore.HasValue)
        {
            descriptor.MinScore(_minScore.Value);
        }

        if (_sortOptions.Count > 0)
        {
            descriptor.Sort(_sortOptions);
        }

        descriptor.Query(q =>
        {
            q.Bool(b =>
            {
                if (!string.IsNullOrWhiteSpace(_queryText))
                {
                    if (_searchFields.Count > 0)
                    {
                        var fields = _searchFields.Select(f => new Field(f)).ToArray();
                        b.Must(m => m.MultiMatch(mm => mm
                            .Query(_queryText)
                            .Fields(fields)
                            .Fuzziness(new Fuzziness(_fuzziness))
                            .PrefixLength(_fuzzyPrefixLength)));
                    }
                    else
                    {
                        b.Must(m => m.QueryString(qs => qs
                            .Query(_queryText)
                            .Fuzziness(new Fuzziness(_fuzziness))));
                    }
                }

                if (_filterActions.Count > 0)
                {
                    b.Filter(_filterActions.ToArray());
                }

                if (_shouldActions.Count > 0)
                {
                    b.Should(_shouldActions.ToArray());
                }
            });
        });
    }
}
