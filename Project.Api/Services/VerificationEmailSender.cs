using System.Net;
using System.Net.Mail;

namespace Project.Api.Services;

public interface IVerificationEmailSender
{
    Task SendAsync(string recipient, string verificationUrl);
}

public class VerificationEmailSender : IVerificationEmailSender
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _environment;

    public VerificationEmailSender(IConfiguration config, IWebHostEnvironment environment)
    {
        _config = config;
        _environment = environment;
        if (!environment.IsDevelopment() &&
            (string.IsNullOrWhiteSpace(config["Mail:SmtpHost"]) ||
             string.IsNullOrWhiteSpace(config["Mail:From"]) ||
             string.IsNullOrWhiteSpace(config["Mail:SmtpUser"]) ||
             string.IsNullOrWhiteSpace(config["Mail:SmtpPassword"]) ||
             string.IsNullOrWhiteSpace(config["Mail:PublicFrontendUrl"])))
            throw new InvalidOperationException("Production mail configuration is incomplete");
    }

    public async Task SendAsync(string recipient, string verificationUrl)
    {
        var host = _config["Mail:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host) && _environment.IsDevelopment())
        {
            var directory = Path.Combine(_environment.ContentRootPath, ".maildrop");
            Directory.CreateDirectory(directory);
            var filename = Path.Combine(directory, $"verify-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(filename, $"To: {recipient}\nVerification URL: {verificationUrl}\n");
            return;
        }

        if (string.IsNullOrWhiteSpace(host)) throw new InvalidOperationException("SMTP host is not configured");
        using var message = new MailMessage(_config["Mail:From"]!, recipient)
        {
            Subject = "Xác minh email tài khoản DACN",
            Body = $"Mở liên kết sau để xác minh email: {verificationUrl}"
        };
        using var client = new SmtpClient(host, int.TryParse(_config["Mail:SmtpPort"], out var port) ? port : 587)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_config["Mail:SmtpUser"], _config["Mail:SmtpPassword"])
        };
        await client.SendMailAsync(message);
    }
}
