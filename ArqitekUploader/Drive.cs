using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArqitekUploader
{
	internal class Drive : IDisposable
	{
		UserCredential credential = Auth();

		DriveService service;

		private static UserCredential Auth()
		{
			if (!System.IO.File.Exists("client_id.json")) throw new FileNotFoundException("client id not found");

			using (var stream = new FileStream("client_id.json", FileMode.Open, FileAccess.Read))
			{

				return GoogleWebAuthorizationBroker.AuthorizeAsync(
					GoogleClientSecrets.FromStream(stream).Secrets,
					[DriveService.Scope.Drive],
					"user",
					CancellationToken.None,
					new FileDataStore(Path.Combine("aqtk", "kreda"))).Result;
			}
		}
		
		public Drive()
		{
			service = new DriveService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential
			});
		}

		public Google.Apis.Drive.v3.Data.File[] List()
		{
			var req = service.Files.List();
			var files = req.Execute().Files;
			return (from file in files where file is not null select file).ToArray();
		}

		void IDisposable.Dispose()
		{
			service.Dispose();
		}
	}
}
