using System.Threading.Tasks;
using OrangeJetpack.Services.Client.Models;

namespace OrangeJetpack.Services.Client.Messaging
{
	public interface IMessageService
	{
		Task Send(Email email);

		void Send(Sms sms);
	}
}
