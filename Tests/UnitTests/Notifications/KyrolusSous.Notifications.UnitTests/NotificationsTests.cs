using KyrolusSous.Notifications.Abstractions;
using KyrolusSous.Notifications.Core;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Notifications.UnitTests;

public sealed class NotificationsTests
{
    [Fact(DisplayName = "Template Renderer Substitutes Variables Correctly")]
    public async Task TemplateRenderer_SubstitutesVariables_Correctly()
    {
        var renderer = new KyrolusTemplateRenderer();
        var template = "Hello {{UserName}}, your order #{{OrderId}} is ready!";
        var model = new { UserName = "Kyrolus", OrderId = 12345 };

        var result = await renderer.RenderAsync(template, model);
        result.ShouldBe("Hello Kyrolus, your order #12345 is ready!");
    }

    [Fact(DisplayName = "Template Renderer Processes Conditional Blocks")]
    public async Task TemplateRenderer_ProcessesConditionalBlocks()
    {
        var renderer = new KyrolusTemplateRenderer();
        var template = "Welcome {{Name}}! {{#if IsVip}}You get a 20% discount!{{/if}}";

        var vipModel = new { Name = "Mina", IsVip = true };
        var vipResult = await renderer.RenderAsync(template, vipModel);
        vipResult.ShouldBe("Welcome Mina! You get a 20% discount!");

        var normalModel = new { Name = "Peter", IsVip = false };
        var normalResult = await renderer.RenderAsync(template, normalModel);
        normalResult.ShouldBe("Welcome Peter! ");
    }

    [Fact(DisplayName = "Resilient Notification Dispatcher Falls Back To Secondary Provider On Failure")]
    public async Task ResilientDispatcher_FallsBack_ToSecondaryProvider()
    {
        var primarySender = Substitute.For<IKyrolusEmailSender>();
        primarySender.SendEmailAsync(Arg.Any<KyrolusEmailMessage>())
                     .Returns(Task.FromResult(KyrolusNotificationResult.Failure("Primary SMTP timeout", "PrimarySmtp")));

        var fallbackSender = Substitute.For<IKyrolusEmailSender>();
        fallbackSender.SendEmailAsync(Arg.Any<KyrolusEmailMessage>())
                      .Returns(Task.FromResult(KyrolusNotificationResult.Success("msg-123", "SendGridFallback")));

        var dispatcher = new KyrolusResilientNotificationDispatcher(
            new[] { primarySender, fallbackSender },
            Enumerable.Empty<IKyrolusSmsSender>());

        var email = new KyrolusEmailMessage
        {
            To = "user@example.com",
            Subject = "Test Alert",
            BodyText = "System is operational."
        };

        var result = await dispatcher.DispatchEmailAsync(email);
        result.Succeeded.ShouldBeTrue();
        result.ProviderName.ShouldBe("SendGridFallback");
        result.MessageId.ShouldBe("msg-123");
    }
}
