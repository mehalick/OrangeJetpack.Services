using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OrangeJetpack.Services.Client.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace OrangeJetpack.Services.Client.Messaging
{
	public class MessageService : IMessageService
	{
		private readonly EmailSettings _emailSettings;

		public MessageService(EmailSettings emailSettings, SmsSettings smsSettings = null)
		{
			_emailSettings = emailSettings;
		}

		/// <summary>
		/// Sends an email message using a third-party email service provide such as SendGrid.
		/// </summary>
		public async Task Send(Email email)
		{
			email.FromAddress = email.FromAddress ?? _emailSettings.SenderAddress;
			email.FromName = email.FromName ?? _emailSettings.SenderName;

			var apiToken = ConfigurationManager.AppSettings["SendGrid:ApiToken"];
			if (string.IsNullOrWhiteSpace(apiToken))
			{
				return;
			}

			var client = new SendGridClient(apiToken);
			var sender = new EmailAddress(email.FromAddress, email.FromName);

			Trace.TraceInformation($"Sending email, recipients: '{email.ToAddress}'");

			var recipients = email.ToAddress.Split(new[] { ";", ",", "|", " " }, StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim());
			foreach (var recipient in recipients)
			{
				try
				{
					var to = MailHelper.StringToEmailAddress(recipient);

					email.ToAddress = to.Email;

					Trace.TraceInformation($"Sending email to '{email.ToAddress}'");

					var message = MailHelper.CreateSingleEmail(sender, to, email.Subject, null, email.Message);

					await client.SendEmailAsync(message);
				}
				catch (Exception ex)
				{
					Trace.TraceError(ex.Message);
				}
			}
		}

		/// <summary>
		/// Performs a noop, Twilio is no longer enabled in this library.
		/// </summary>
		[Obsolete("This method has been deprecated, Twilio SMS is no longer enabled.")]
		public void Send(Sms sms)
		{
			return;
		}
	}
}
