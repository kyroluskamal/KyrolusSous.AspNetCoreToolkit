namespace KyrolusSous.Notifications.Abstractions;

public enum KyrolusNotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public sealed record KyrolusEmailAttachment(string FileName, byte[] Content, string ContentType = "application/octet-stream");

public sealed record KyrolusEmailMessage
{
    public required string To { get; init; }
    public string? From { get; init; }
    public required string Subject { get; init; }
    public string? BodyText { get; init; }
    public string? BodyHtml { get; init; }
    public IReadOnlyList<string>? Cc { get; init; }
    public IReadOnlyList<string>? Bcc { get; init; }
    public IReadOnlyList<KyrolusEmailAttachment>? Attachments { get; init; }
    public KyrolusNotificationPriority Priority { get; init; } = KyrolusNotificationPriority.Normal;
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}

public sealed record KyrolusSmsMessage
{
    public required string ToPhoneNumber { get; init; }
    public string? FromPhoneNumber { get; init; }
    public required string Text { get; init; }
    public KyrolusNotificationPriority Priority { get; init; } = KyrolusNotificationPriority.Normal;
}

public sealed record KyrolusNotificationResult
{
    public bool Succeeded { get; init; }
    public string? MessageId { get; init; }
    public string? ProviderName { get; init; }
    public string? ErrorMessage { get; init; }

    public static KyrolusNotificationResult Success(string? messageId = null, string? providerName = null)
        => new() { Succeeded = true, MessageId = messageId, ProviderName = providerName };

    public static KyrolusNotificationResult Failure(string error, string? providerName = null)
        => new() { Succeeded = false, ErrorMessage = error, ProviderName = providerName };
}

public interface IKyrolusEmailSender
{
    Task<KyrolusNotificationResult> SendEmailAsync(KyrolusEmailMessage message, CancellationToken cancellationToken = default);
}

public interface IKyrolusSmsSender
{
    Task<KyrolusNotificationResult> SendSmsAsync(KyrolusSmsMessage message, CancellationToken cancellationToken = default);
}

public interface IKyrolusTemplateRenderer
{
    Task<string> RenderAsync(string templateContent, object model, CancellationToken cancellationToken = default);
}

public interface IKyrolusNotificationDispatcher
{
    Task<KyrolusNotificationResult> DispatchEmailAsync(KyrolusEmailMessage message, CancellationToken cancellationToken = default);
    Task<KyrolusNotificationResult> DispatchSmsAsync(KyrolusSmsMessage message, CancellationToken cancellationToken = default);
}
