using System.Collections;
using System.Reflection;
using System.Text;

namespace KyrolusSous.EndpointKit.Core.FieldSelection;

/// <summary>
/// Represents a parsed field selection specification.
/// Supports nested fields like: Id,Name,Category[Id,Name],Orders[Id,Total,Items[ProductId,Quantity]]
/// </summary>
public class KyrolusFieldSelection
{
    private readonly Dictionary<string, KyrolusFieldSelection?> _fields = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the selected fields at this level.</summary>
    public IReadOnlyDictionary<string, KyrolusFieldSelection?> Fields => _fields;

    /// <summary>Indicates if all fields are selected (no specific selection).</summary>
    public bool SelectAll { get; private set; } = true;

    /// <summary>Adds a field to the selection.</summary>
    public void AddField(string name, KyrolusFieldSelection? nested = null)
    {
        SelectAll = false;
        if (_fields.TryGetValue(name, out var existing) && existing is not null && nested is not null)
        {
            existing.Merge(nested);
        }
        else if (!_fields.ContainsKey(name) || nested is not null)
        {
            _fields[name] = nested;
        }
    }

    /// <summary>Merges another field selection into this one.</summary>
    public void Merge(KyrolusFieldSelection other)
    {
        if (other is null || other.SelectAll) return;
        SelectAll = false;
        foreach (var (k, v) in other._fields)
        {
            AddField(k, v);
        }
    }

    /// <summary>Checks if a field is selected.</summary>
    public bool IsFieldSelected(string fieldName)
    {
        if (SelectAll) return true;
        return _fields.ContainsKey(fieldName);
    }

    /// <summary>Gets nested field selection for a field.</summary>
    public KyrolusFieldSelection? GetNestedSelection(string fieldName)
    {
        if (_fields.TryGetValue(fieldName, out var nested))
            return nested;
        return null;
    }
}

/// <summary>
/// Parses field selection strings into structured specifications.
/// Supports formats:
/// - Simple: "Id,Name,Email"
/// - Nested dot notation: "Id,Name,Category.Name,Category.Id"
/// - Nested bracket notation: "Id,Name,Category[Id,Name]"
/// - Mixed: "Id,Category[Id,Name],Orders[Id,Items[ProductId,Quantity]]"
/// </summary>
public static class KyrolusFieldSelectionParser
{
    /// <summary>
    /// Parses a field selection string.
    /// </summary>
    /// <param name="fields">The fields string (comma-separated, with optional nested brackets).</param>
    /// <param name="selection">The parsed selection.</param>
    /// <param name="error">Error message if parsing failed.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParse(string? fields, out KyrolusFieldSelection selection, out string? error)
    {
        selection = new KyrolusFieldSelection();
        error = null;

        if (string.IsNullOrWhiteSpace(fields))
        {
            return true; // Empty = select all
        }

        try
        {
            var parser = new FieldParser(fields);
            selection = parser.Parse();
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Parses field selection from a list of field strings.
    /// </summary>
    public static KyrolusFieldSelection Parse(IEnumerable<string>? fields)
    {
        var selection = new KyrolusFieldSelection();
        if (fields is null) return selection;

        foreach (var field in fields)
        {
            AddFieldPath(selection, field.Trim());
        }

        return selection;
    }

    private static void AddFieldPath(KyrolusFieldSelection selection, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = selection;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;

            if (isLast)
            {
                current.AddField(part, null);
            }
            else
            {
                var nested = current.GetNestedSelection(part);
                if (nested is null)
                {
                    nested = new KyrolusFieldSelection();
                    current.AddField(part, nested);
                }
                current = nested;
            }
        }
    }

    private sealed class FieldParser
    {
        private readonly string _text;
        private int _pos;

        public FieldParser(string text)
        {
            _text = text;
            _pos = 0;
        }

        public KyrolusFieldSelection Parse()
        {
            var selection = new KyrolusFieldSelection();
            ParseFields(selection);
            return selection;
        }

        private void ParseFields(KyrolusFieldSelection selection)
        {
            while (_pos < _text.Length)
            {
                SkipWhitespace();
                if (_pos >= _text.Length) break;

                var name = ReadIdentifier();
                if (string.IsNullOrEmpty(name)) break;

                SkipWhitespace();

                KyrolusFieldSelection? nested = null;

                // Check for nested selection with brackets: Field[SubField1,SubField2]
                if (_pos < _text.Length && _text[_pos] == '[')
                {
                    _pos++; // skip '['
                    nested = new KyrolusFieldSelection();
                    ParseFields(nested);
                    SkipWhitespace();
                    if (_pos < _text.Length && _text[_pos] == ']')
                    {
                        _pos++; // skip ']'
                    }
                    else
                    {
                        throw new FormatException($"Missing closing ']' for field '{name}'");
                    }
                }
                // Check for nested selection with dot notation: Field.SubField
                else if (_pos < _text.Length && _text[_pos] == '.')
                {
                    _pos++; // skip '.'
                    nested = new KyrolusFieldSelection();
                    var nestedName = ReadIdentifier();
                    if (!string.IsNullOrEmpty(nestedName))
                    {
                        nested.AddField(nestedName, null);
                    }
                }

                selection.AddField(name, nested);

                SkipWhitespace();

                // Check for separator
                if (_pos < _text.Length)
                {
                    if (_text[_pos] == ',')
                    {
                        _pos++; // skip ','
                    }
                    else if (_text[_pos] == ']')
                    {
                        break; // end of nested selection
                    }
                }
            }
        }

        private string ReadIdentifier()
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
            {
                _pos++;
            }
            return _text.Substring(start, _pos - start);
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }
    }
}

/// <summary>
/// Projects objects based on field selection.
/// </summary>
public static class KyrolusFieldProjector
{
    /// <summary>
    /// Projects an object to a dictionary containing only selected fields.
    /// </summary>
    public static object? Project(object? data, KyrolusFieldSelection selection)
    {
        if (data is null) return null;
        if (selection.SelectAll) return data;

        if (data is IEnumerable enumerable && data is not string && data is not IDictionary)
        {
            return ProjectCollection(enumerable, selection);
        }

        return ProjectSingle(data, selection);
    }

    /// <summary>
    /// Projects a single object.
    /// </summary>
    public static Dictionary<string, object?> ProjectSingle(object data, KyrolusFieldSelection selection)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (data is IDictionary<string, object?> dict)
        {
            foreach (var (fieldName, nestedSelection) in selection.Fields)
            {
                if (!dict.TryGetValue(fieldName, out var value)) continue;
                if (nestedSelection is not null && value is not null)
                {
                    value = Project(value, nestedSelection);
                }
                result[fieldName] = value;
            }
            return result;
        }

        var type = data.GetType();

        foreach (var (fieldName, nestedSelection) in selection.Fields)
        {
            var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null) continue;

            var value = property.GetValue(data);

            if (nestedSelection is not null && value is not null)
            {
                // Project nested object(s)
                value = Project(value, nestedSelection);
            }

            result[property.Name] = value;
        }

        return result;
    }

    /// <summary>
    /// Projects a collection of objects.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, object?>> ProjectCollection(IEnumerable data, KyrolusFieldSelection selection)
    {
        var results = new List<Dictionary<string, object?>>();

        foreach (var item in data)
        {
            if (item is null) continue;
            results.Add(ProjectSingle(item, selection));
        }

        return results;
    }

    /// <summary>
    /// Projects a paged result.
    /// </summary>
    public static KyrolusProjectedPagedResult ProjectPaged<T>(
        IReadOnlyList<T> items,
        long totalCount,
        int pageNumber,
        int pageSize,
        KyrolusFieldSelection selection)
    {
        var projectedItems = items
            .Where(item => item is not null)
            .Select(item => ProjectSingle(item!, selection))
            .ToList();

        return new KyrolusProjectedPagedResult(
            projectedItems,
            totalCount,
            pageNumber,
            pageSize);
    }
}

/// <summary>
/// Represents a paged result with projected items.
/// </summary>
public class KyrolusProjectedPagedResult
{
    public KyrolusProjectedPagedResult(
        IReadOnlyList<Dictionary<string, object?>> items,
        long totalCount,
        int pageNumber,
        int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
        HasNextPage = pageNumber < TotalPages;
        HasPreviousPage = pageNumber > 1;
    }

    public IReadOnlyList<Dictionary<string, object?>> Items { get; }
    public long TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages { get; }
    public bool HasNextPage { get; }
    public bool HasPreviousPage { get; }
}

/// <summary>
/// Validates field selections against a type.
/// </summary>
public static class KyrolusFieldValidator
{
    /// <summary>
    /// Validates that all selected fields exist on the type.
    /// </summary>
    public static bool Validate<T>(KyrolusFieldSelection selection, out IReadOnlyList<string> invalidFields)
    {
        return Validate(typeof(T), selection, "", out invalidFields);
    }

    /// <summary>
    /// Validates that all selected fields exist on the type.
    /// </summary>
    public static bool Validate(Type type, KyrolusFieldSelection selection, string prefix, out IReadOnlyList<string> invalidFields)
    {
        var invalid = new List<string>();

        if (selection.SelectAll)
        {
            invalidFields = invalid;
            return true;
        }

        foreach (var (fieldName, nestedSelection) in selection.Fields)
        {
            var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var fullPath = string.IsNullOrEmpty(prefix) ? fieldName : $"{prefix}.{fieldName}";

            if (property is null)
            {
                invalid.Add(fullPath);
                continue;
            }

            if (nestedSelection is not null && !nestedSelection.SelectAll)
            {
                var nestedType = GetElementType(property.PropertyType);
                Validate(nestedType, nestedSelection, fullPath, out var nestedInvalid);
                invalid.AddRange(nestedInvalid);
            }
        }

        invalidFields = invalid;
        return invalid.Count == 0;
    }

    private static Type GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType()!;

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(IReadOnlyList<>) ||
                genericDef == typeof(IReadOnlyCollection<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        var enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable is not null)
            return enumerable.GetGenericArguments()[0];

        return type;
    }
}
