using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace OrangeJetpack.Services.Client.Storage
{
	public interface IStorageService
	{
		Task<Uri> SaveFile(string containerName, HttpPostedFileBase postedFile);

		Task<List<Uri>> SaveImage(string containerName, HttpPostedFileBase postedFile, ImageSettings imageSettings);

		Task DeleteFile(string containerName, string fileName);
	}
}
