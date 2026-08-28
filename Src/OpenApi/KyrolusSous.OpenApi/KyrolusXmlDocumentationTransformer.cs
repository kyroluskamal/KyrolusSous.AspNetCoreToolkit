using System.Collections.Concurrent;
using System.Xml.Linq;

namespace KyrolusSous.OpenApi;

/// <summary>
/// Transformer that reads XML documentation comments and applies them to OpenAPI operations and parameters.
/// </summary>
public sealed class KyrolusXmlDocumentationTransformer : IOpenApiOperationTransformer
{
    private readonly KyrolusOpenApiOptions _options;
    private readonly ConcurrentDictionary<string, XmlMemberDoc> _members = new(StringComparer.Ordinal);
    private readonly object _initLock = new();
    private volatile bool _initialized;

    public KyrolusXmlDocumentationTransformer(KyrolusOpenApiOptions options)
    {
        _options = options;
    }

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableXmlComments)
        {
            return Task.CompletedTask;
        }

        EnsureInitialized();

        var methodInfo = GetMethodInfo(context);
        if (methodInfo is null)
        {
            return Task.CompletedTask;
        }

        var memberKey = GetMemberKey(methodInfo);
        if (_members.TryGetValue(memberKey, out var doc) || TryFindFuzzyDoc(methodInfo, out doc))
        {
            if (!string.IsNullOrWhiteSpace(doc.Summary))
            {
                if (string.IsNullOrWhiteSpace(operation.Summary))
                {
                    operation.Summary = doc.Summary;
                }

                if (string.IsNullOrWhiteSpace(operation.Description))
                {
                    operation.Description = doc.Summary;
                }
            }

            if (!string.IsNullOrWhiteSpace(doc.Remarks))
            {
                var remarksText = $"\n\n{doc.Remarks}";
                if (string.IsNullOrWhiteSpace(operation.Description))
                {
                    operation.Description = doc.Remarks;
                }
                else if (!operation.Description.Contains(doc.Remarks, StringComparison.OrdinalIgnoreCase))
                {
                    operation.Description += remarksText;
                }
            }

            if (operation.Parameters is not null && doc.Params.Count > 0)
            {
                foreach (var param in operation.Parameters)
                {
                    if (string.IsNullOrWhiteSpace(param.Description) &&
                        !string.IsNullOrWhiteSpace(param.Name) &&
                        doc.Params.TryGetValue(param.Name, out var paramDesc))
                    {
                        param.Description = paramDesc;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(doc.Returns) && operation.Responses is not null)
            {
                if (operation.Responses.TryGetValue("200", out var res200) &&
                    (string.IsNullOrWhiteSpace(res200.Description) || string.Equals(res200.Description, "OK", StringComparison.OrdinalIgnoreCase)))
                {
                    res200.Description = doc.Returns;
                }
            }
        }

        return Task.CompletedTask;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in _options.XmlDocAbsolutePaths)
            {
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                {
                    paths.Add(Path.GetFullPath(p));
                }
            }

            var baseDir = AppContext.BaseDirectory;
            foreach (var asm in _options.XmlCommentAssemblies)
            {
                var asmName = asm.GetName().Name;
                if (!string.IsNullOrWhiteSpace(asmName))
                {
                    var candidate = Path.Combine(baseDir, $"{asmName}.xml");
                    if (File.Exists(candidate))
                    {
                        paths.Add(candidate);
                    }
                }
            }

            var entryAsm = Assembly.GetEntryAssembly();
            if (entryAsm is not null)
            {
                var entryCandidate = Path.Combine(baseDir, $"{entryAsm.GetName().Name}.xml");
                if (File.Exists(entryCandidate))
                {
                    paths.Add(entryCandidate);
                }
            }

            foreach (var path in paths)
            {
                LoadXmlFile(path);
            }

            _initialized = true;
        }
    }

    private void LoadXmlFile(string path)
    {
        try
        {
            var doc = XDocument.Load(path);
            var members = doc.Root?.Element("members")?.Elements("member");
            if (members is null)
            {
                return;
            }

            foreach (var member in members)
            {
                var name = member.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var summary = member.Element("summary")?.Value?.Trim();
                var remarks = member.Element("remarks")?.Value?.Trim();
                var returns = member.Element("returns")?.Value?.Trim();
                var paramDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in member.Elements("param"))
                {
                    var pName = p.Attribute("name")?.Value;
                    if (!string.IsNullOrWhiteSpace(pName) && !string.IsNullOrWhiteSpace(p.Value))
                    {
                        paramDict[pName] = p.Value.Trim();
                    }
                }

                _members[name] = new XmlMemberDoc(summary, remarks, returns, paramDict);
            }
        }
        catch
        {
            // Gracefully ignore corrupt XML documentation files
        }
    }

    private static MethodInfo? GetMethodInfo(OpenApiOperationTransformerContext context)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata is null)
        {
            return null;
        }

        return metadata.OfType<MethodInfo>().FirstOrDefault()
            ?? metadata.OfType<Delegate>().FirstOrDefault()?.Method;
    }

    private static string GetMemberKey(MethodInfo method)
    {
        var typeName = method.DeclaringType?.FullName ?? "";
        return $"M:{typeName}.{method.Name}";
    }

    private bool TryFindFuzzyDoc(MethodInfo method, out XmlMemberDoc doc)
    {
        var prefix = $"M:{method.DeclaringType?.FullName}.{method.Name}";
        foreach (var (key, value) in _members)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                doc = value;
                return true;
            }
        }

        doc = default;
        return false;
    }

    private readonly record struct XmlMemberDoc(string? Summary, string? Remarks, string? Returns, Dictionary<string, string> Params);
}
