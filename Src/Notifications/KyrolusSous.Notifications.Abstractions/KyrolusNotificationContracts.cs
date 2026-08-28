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

public sealed record KyrolusPushMessage
{
    public required string DeviceToken { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ImageUrl { get; init; }
    public int? Badge { get; init; }
    public string? Sound { get; init; } = "default";
    public IDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
    public KyrolusNotificationPriority Priority { get; init; } = KyrolusNotificationPriority.Normal;
}

public sealed record KyrolusInAppNotification
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public bool IsRead { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
}

public interface IKyrolusPushSender
{
    Task<KyrolusNotificationResult> SendPushAsync(KyrolusPushMessage message, CancellationToken cancellationToken = default);
}

public interface IKyrolusInAppNotificationStore
{
    Task<KyrolusInAppNotification> SaveNotificationAsync(KyrolusInAppNotification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusInAppNotification>> GetNotificationsAsync(string userId, bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(string notificationId, string userId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IKyrolusNotificationQueue
{
    Task EnqueueEmailAsync(KyrolusEmailMessage message, CancellationToken cancellationToken = default);
    Task EnqueueSmsAsync(KyrolusSmsMessage message, CancellationToken cancellationToken = default);
    Task EnqueuePushAsync(KyrolusPushMessage message, CancellationToken cancellationToken = default);
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
    Task<KyrolusNotificationResult> DispatchPushAsync(KyrolusPushMessage message, CancellationToken cancellationToken = default);
}
