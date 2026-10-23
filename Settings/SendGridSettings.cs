using SendGrid.Helpers.Mail;

namespace Quantmatix.Robots.Settings; 

public class SendGridSettings {
    public string GenericEmailTemplateId { get; set; } = null!;
    public EmailAddress FromAddress { get; set; } = null!;
}