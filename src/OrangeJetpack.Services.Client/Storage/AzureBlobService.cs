using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using ImageResizer;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace OrangeJetpack.Services.Client.Storage
{
	public class AzureBlobService : IStorageService
	{
		private readonly string _connectionString;
		private readonly string _cdnHostName;

		/// <summary>
		/// Initializes a new instance of an AzureBlogService, by default uses a connection string
		/// called "StorageConnection" defined in web.config or app.config.
		/// </summary>
		public AzureBlobService(string connectionString = null, string cdnHostName = null)
		{
			_connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["StorageConnection"]?.ConnectionString ?? ConfigurationManager.AppSettings["StorageConnection"];
			_cdnHostName = cdnHostName ?? ConfigurationManager.AppSettings["CdnHostName"];
		}

		/// <summary>
		/// Saves a file to Azure blog storage using the Orange Jetpack services REST API.
		/// </summary>
		/// <param name="containerName">The name of the Azure blog storage container to save the image to.</param>
		/// <param name="postedFile">The file to save.</param>
		/// <returns>The URL of the newly saved file.</returns>
		public async Task<Uri> SaveFile(string containerName, HttpPostedFileBase postedFile)
		{
			var safeFileName = GetFileName(postedFile.FileName);
			var blobContainer = GetBlobContainer(_connectionString, containerName);

			var blob = blobContainer.GetBlockBlobReference(safeFileName);
			blob.Properties.ContentType = postedFile.ContentType;
			blob.Properties.CacheControl = GetCacheControlForYears(1);

			await blob.UploadFromStreamAsync(postedFile.InputStream);

			return GetBlobUri(blob);
		}

		/// <summary>
		/// Saves an image to Azure blog storage using the Orange Jetpack services REST API.
		/// </summary>
		/// <param name="containerName">The name of the Azure blog storage container to save the image to.</param>
		/// <param name="postedFile">The image file to save.</param>
		/// <param name="imageSettings">The image resize settings to use.</param>
		/// <returns>A collection of URLs for the saved image(s).</returns>
		public async Task<List<Uri>> SaveImage(string containerName, HttpPostedFileBase postedFile, ImageSettings imageSettings)
		{
			var fileName = Path.GetFileName(postedFile.FileName);
			var contentType = postedFile.ContentType;

			using (var inputStream = new MemoryStream())
			{
				await CopyToStream(postedFile, inputStream);

				var blobContainer = GetBlobContainer(_connectionString, containerName);
				var blobUris = new List<Uri>();
				var rotateFlipType = GetRotateFlipType(inputStream);

				foreach (var width in imageSettings.Widths)
				{
					using (var outputStream = new MemoryStream())
					{
						var safeFileName = GetFileName(fileName, width);

						ResizeImage(inputStream, outputStream, width, imageSettings.ForceSquare, imageSettings.BackgroundColor, rotateFlipType);

						var blobUri = await SaveFile(blobContainer, outputStream, safeFileName, contentType);

						blobUris.Add(blobUri);
					}
				}

				return blobUris;
			}
		}

		private static async Task CopyToStream(HttpPostedFileBase postedFile, Stream inputStream)
		{
			await postedFile.InputStream.CopyToAsync(inputStream);
			postedFile.InputStream.Position = 0;
		}

		private async Task<Uri> SaveFile(CloudBlobContainer blobContainer, Stream stream, string fileName, string contentType)
		{
			stream.Position = 0;

			var blob = blobContainer.GetBlockBlobReference(fileName);
			blob.Properties.ContentType = contentType;
			blob.Properties.CacheControl = GetCacheControlForYears(1);

			await blob.UploadFromStreamAsync(stream);

			return GetBlobUri(blob);
		}

		private Uri GetBlobUri(IListBlobItem blob)
		{
			if (string.IsNullOrEmpty(_cdnHostName))
			{
				return blob.Uri;
			}

			var uriBuilder = new UriBuilder(blob.Uri)
			{
				Scheme = "https",
				Host = _cdnHostName,
				Port = -1
			};

			return uriBuilder.Uri;
		}

		public async Task DeleteFile(string containerName, string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName))
			{
				return;
			}

			var blobContainer = GetBlobContainer(_connectionString, containerName);
			var blob = blobContainer.GetBlockBlobReference(fileName);
			await blob.DeleteIfExistsAsync();
		}

		private static void ResizeImage(Stream inputStream, Stream outputStream, int width, bool forceSquare, Color backgroundColor, RotateFlipType rotateFlipType)
		{
			inputStream.Position = 0;

			var resizeSettings = GetResizeSettings(width, forceSquare, backgroundColor, rotateFlipType);
			ImageBuilder.Current.Build(inputStream, outputStream, resizeSettings, false);

			outputStream.Position = 0;
		}

		private static ResizeSettings GetResizeSettings(int width, bool forceSquare, Color backgroundColor, RotateFlipType rotateFlipType)
		{
			var resizeSettings = new ResizeSettings
			{
				MaxWidth = width,
				Scale = ScaleMode.Both,
				Flip = rotateFlipType
			};

			if (forceSquare)
			{
				resizeSettings.Width = width;
				resizeSettings.Height = width;
				resizeSettings.Mode = FitMode.Pad;
				resizeSettings.Anchor = ContentAlignment.MiddleCenter;
				resizeSettings.BackgroundColor = backgroundColor;
				resizeSettings.Quality = 80;
			}

			return resizeSettings;
		}

		private static RotateFlipType GetRotateFlipType(Stream inputStream)
		{
			using (var image = Image.FromStream(inputStream))
			{
				return GetRotateFlipType(image);
			}
		}

		private static RotateFlipType GetRotateFlipType(Image image)
		{
			if (Array.IndexOf(image.PropertyIdList, 274) <= -1)
			{
				return RotateFlipType.RotateNoneFlipNone;
			}

			var orientation = (int)image.GetPropertyItem(274).Value[0];
			switch (orientation)
			{
				case 3:
					return RotateFlipType.Rotate180FlipNone;

				case 4:
					return RotateFlipType.Rotate180FlipX;

				case 5:
					return RotateFlipType.Rotate90FlipX;

				case 6:
					return RotateFlipType.Rotate90FlipNone;

				case 7:
					return RotateFlipType.Rotate270FlipX;

				case 8:
					return RotateFlipType.Rotate270FlipNone;

				default:
					return RotateFlipType.RotateNoneFlipNone;
			}
		}

		private static CloudBlobContainer GetBlobContainer(string storageConnection, string containerName)
		{
			var storageAccount = CloudStorageAccount.Parse(storageConnection);
			return storageAccount.CreateCloudBlobClient().GetContainerReference(containerName);
		}

		private static string GetFileName(string fileName)
		{
			var name = GenerateSlug(Path.GetFileNameWithoutExtension(fileName));
			var extension = Path.GetExtension(fileName);

			return $"{name}-{DateTime.UtcNow.Ticks}{extension}";
		}

		private static string GetFileName(string fileName, int width)
		{
			var name = GenerateSlug(Path.GetFileNameWithoutExtension(fileName));
			if (width > 0)
			{
				name += "-" + width;
			}

			var extension = Path.GetExtension(fileName);

			return $"{name}-{DateTime.UtcNow.Ticks}{extension}";
		}

		private static string GetCacheControlForYears(int years)
		{
			const int secondsInYear = 31536000;

			return $"public, max-age={secondsInYear * years}";
		}

		private static string GenerateSlug(string txt)
		{
			var str = RemoveAccent(txt).ToLower();

			str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // invalid chars
			str = Regex.Replace(str, @"\s+", " ").Trim(); // convert multiple spaces into one space
			str = Regex.Replace(str, @"\s", "-"); // hyphens

			return str;
		}

		private static string RemoveAccent(string txt)
		{
			var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(txt);
			return Encoding.ASCII.GetString(bytes);
		}
	}
}
