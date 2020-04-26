using System.Text.RegularExpressions;

namespace OrangeJetpack.Services.Client.Models
{
	public class Sms
	{
		public string CountryCode { get; set; }
		public string LocalNumber { get; set; }
		public string Message { get; set; }

		public string PhoneNumber
		{
			get
			{
				var phoneNumber = StripNonDigits(CountryCode + LocalNumber);
				if (!phoneNumber.StartsWith("+"))
				{
					phoneNumber = "+" + phoneNumber;
				}

				return phoneNumber;
			}
		}

		/// <summary>
		/// Compiled regular expression for performance.
		/// </summary>
		private static readonly Regex NotDigitsRegex = new Regex(@"[^(0-9|/\u0660-\u0669/)]", RegexOptions.Compiled);

		/// <summary>
		/// Gets a string with all non-numeric digits removed.
		/// </summary>
		private static string StripNonDigits(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return input;
			}

			return NotDigitsRegex.Replace(input, "").Trim();
		}
	}
}
