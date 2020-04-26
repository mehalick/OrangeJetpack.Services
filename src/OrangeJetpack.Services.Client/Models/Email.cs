namespace OrangeJetpack.Services.Client.Models
{
	public class Email
	{
		public string FromAddress { get; set; }
		public string FromName { get; set; }
		public string ToAddress { get; set; }
		public string Subject { get; set; }
		public string PreviewText { get; set; }
		public string Message { get; set; }
	}
}
