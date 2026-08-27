using System.Text.Json;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace KyrolusSous.Auth.OpenIddict.Handlers;

/// <summary>
/// Adds Kyrolus diagnostic parameters to OAuth error responses, without touching the standard
/// <c>error</c>, <c>error_description</c> and <c>error_uri</c> fields.
/// </summary>
/// <remarks>
/// <para>
/// The token endpoint is a protocol surface, not an application API: replacing its error body
/// with a house error envelope breaks every conforming client library. So this only <em>adds</em>
/// two parameters - <c>error_code</c> carrying the Kyrolus code, and <c>errors</c> carrying a
/// field-level breakdown a form can bind to. Clients that do not know about them ignore them.
/// </para>
/// <para>
/// This replaces an earlier handler that threw <c>KyrolusValidationException</c> from inside the
/// response pipeline. Throwing there aborts OpenIddict mid-write: the client gets a 500 with no
/// OAuth error body at all, and the exception escapes to whatever middleware is outermost.
/// </para>
/// </remarks>
public sealed class KyrolusErrorEnrichmentHandler : IOpenIddictServerHandler<ApplyTokenResponseContext>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ApplyTokenResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.Response;
        if (response is null || string.IsNullOrEmpty(response.Error))
        {
            return default;
        }

        response["error_code"] = MapErrorCode(response.Error);

        var items = BuildErrorItems(response.Error, response.ErrorDescription);
        if (items.Count > 0)
        {
            response["errors"] = SerializeItems(items);
        }

        return default;
    }

    private static string MapErrorCode(string error) => error switch
    {
        Errors.InvalidGrant => KyrolusErrorCodes.Unauthorized,
        Errors.InvalidClient => KyrolusErrorCodes.Unauthorized,
        Errors.InvalidToken => KyrolusErrorCodes.Unauthorized,
        Errors.AccessDenied => KyrolusErrorCodes.Forbidden,
        Errors.UnauthorizedClient => KyrolusErrorCodes.Forbidden,
        Errors.InvalidRequest => KyrolusErrorCodes.Validation,
        Errors.InvalidScope => KyrolusErrorCodes.Validation,
        Errors.UnsupportedGrantType => KyrolusErrorCodes.BadRequest,
        Errors.UnsupportedResponseType => KyrolusErrorCodes.BadRequest,
        Errors.SlowDown => KyrolusErrorCodes.RateLimit,
        Errors.ServerError => KyrolusErrorCodes.InternalError,
        Errors.TemporarilyUnavailable => KyrolusErrorCodes.ExternalService,
        _ => KyrolusErrorCodes.BadRequest,
    };

    /// <summary>
    /// Turns the free-text description OpenIddict produces for a missing parameter into structured
    /// per-field items, so a client can highlight the offending input instead of showing a sentence.
    /// </summary>
    private static List<KyrolusErrorItem> BuildErrorItems(string error, string? description)
    {
        var items = new List<KyrolusErrorItem>();

        if (string.IsNullOrWhiteSpace(description) ||
            !string.Equals(error, Errors.InvalidRequest, StringComparison.Ordinal))
        {
            return items;
        }

        // OpenIddict phrases these as "The mandatory 'username' and 'password' parameters are
        // missing.", quoting each parameter name. Reading the quoted names is far more robust
        // than guessing which fields a given grant type happens to require.
        if (!description.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return items;
        }

        foreach (var field in ExtractQuotedNames(description))
        {
            items.Add(new KyrolusErrorItem(field, KyrolusErrorCodes.Validation, $"The {field} field is required."));
        }

        return items;
    }

    private static List<string> ExtractQuotedNames(string description)
    {
        var names = new List<string>();
        var span = description.AsSpan();
        var index = 0;

        while (index < span.Length)
        {
            var open = span[index..].IndexOf('\'');
            if (open < 0)
            {
                break;
            }

            open += index + 1;

            var close = span[open..].IndexOf('\'');
            if (close < 0)
            {
                break;
            }

            var name = span.Slice(open, close).ToString();
            if (name.Length > 0 && !names.Contains(name))
            {
                names.Add(name);
            }

            index = open + close + 1;
        }

        return names;
    }

    /// <summary>
    /// Writes the items with <see cref="Utf8JsonWriter"/> rather than a serializer: no reflection,
    /// so the whole path stays trim- and AOT-safe.
    /// </summary>
    private static OpenIddictParameter SerializeItems(List<KyrolusErrorItem> items)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();

            foreach (var item in items)
            {
                writer.WriteStartObject();
                writer.WriteString("field", item.Field);
                writer.WriteString("code", item.Code);
                writer.WriteString("message", item.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        // Clone detaches the element from the JsonDocument, which is disposed straight after.
        using var document = JsonDocument.Parse(buffer.ToArray());
        return new OpenIddictParameter(document.RootElement.Clone());
    }
}
