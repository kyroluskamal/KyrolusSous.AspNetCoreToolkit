using System.Net;
using System.Net.Mail;
using KyrolusSous.Notifications.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Notifications.Smtp;

public sealed class KyrolusSmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; } = false;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string DefaultFrom { get; set; } = "noreply@kyrolus.local";
}

public sealed class KyrolusSmtpEmailSender : IKyrolusEmailSender
{
    private readonly KyrolusSmtpOptions _options;
    private readonly ILogger<KyrolusSmtpEmailSender>? _logger;

    public KyrolusSmtpEmailSender(IOptions<KyrolusSmtpOptions> options, ILogger<KyrolusSmtpEmailSender>? logger = null)
    {
        _options = options?.Value ?? new KyrolusSmtpOptions();
        _logger = logger;
    }

    public async Task<KyrolusNotificationResult> SendEmailAsync(KyrolusEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrEmpty(_options.UserName) && !string.IsNullOrEmpty(_options.Password))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            }

            var from = !string.IsNullOrEmpty(message.From) ? message.From : _options.DefaultFrom;
            using var mail = new MailMessage(from, message.To, message.Subject, message.BodyHtml ?? message.BodyText ?? string.Empty)
            {
                IsBodyHtml = !string.IsNullOrEmpty(message.BodyHtml)
            };

            if (message.Cc != null)
            {
                foreach (var cc in message.Cc) mail.CC.Add(cc);
            }

            if (message.Bcc != null)
            {
                foreach (var bcc in message.Bcc) mail.Bcc.Add(bcc);
            }

            if (message.Attachments != null)
            {
                foreach (var att in message.Attachments)
                {
                    mail.Attachments.Add(new Attachment(new MemoryStream(att.Content), att.FileName, att.ContentType));
                }
            }

            await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
            return KyrolusNotificationResult.Success(Guid.NewGuid().ToString("N"), "SMTP");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send email to {To} via SMTP.", message.To);
            return KyrolusNotificationResult.Failure(ex.Message, "SMTP");
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusSmtp(this IServiceCollection services, Action<KyrolusSmtpOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IKyrolusEmailSender, KyrolusSmtpEmailSender>();
        return services;
    }
}
