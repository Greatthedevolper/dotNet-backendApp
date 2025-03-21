using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DotNetApi.Services
{
    public class EmailService
    {
        private readonly string smtpServer;
        private readonly int smtpPort;
        private readonly string smtpUsername;
        private readonly string smtpPassword;

        public EmailService(IConfiguration configuration)
        {
            smtpServer = configuration["Email:SmtpServer"] ?? "sandbox.smtp.mailtrap.io";
            smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "2525");
            smtpUsername = configuration["Email:SmtpUsername"] ?? "e82565d92feb8c";
            smtpPassword = configuration["Email:SmtpPassword"] ?? "5f1dcd19d84a3d";
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    client.EnableSsl = true;

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress("no-reply@example.com"); // Change to your preferred sender
                        mailMessage.To.Add(toEmail);
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;

                        await client.SendMailAsync(mailMessage);
                    }
                }

                Console.WriteLine("✅ Email sent successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Email sending failed: {ex.Message}");
                return false;
            }
        }
    }
}
