using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;

namespace ArqitekUploader
{
	internal class Drive : IDisposable
	{
		UserCredential credential = Auth();

		DriveService service;

		public delegate void OnProgressChanged(float progress, string filename);

		private static UserCredential Auth()
		{
			if (!System.IO.File.Exists("client_id.json"))
				throw new FileNotFoundException("client_id.json not found");

			using (var stream = new FileStream("client_id.json", FileMode.Open, FileAccess.Read))
			{
				try
				{
					var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
						GoogleClientSecrets.FromStream(stream).Secrets,
						new[] { DriveService.Scope.Drive },
						"user",
						CancellationToken.None,
						new FileDataStore(Path.Combine("aqtk", "kreda"))
					).Result;

					// Check if the token is expired and refresh if necessary
					if (credential.Token.IsStale)
					{
						// Try to refresh the token
						bool refreshed = credential.RefreshTokenAsync(CancellationToken.None).Result;

						if (!refreshed)
						{
							// If refresh fails, reauthorize
							throw new Exception("Token refresh failed. Reauthorization required.");
						}
					}

					return credential;
				}
				catch (AggregateException ex)
				{
					// Handle token refresh or authorization failure
					throw new Exception("Authorization failed. Ensure client_id.json is correct.", ex);
				}
			}
		}

		public async Task UploadFile(string filePath, string mimeType, string? folderId = null, OnProgressChanged? progressCallback = null)
		{
			await Task.Run(() =>
			{
				// Ensure the file exists locally
				if (!System.IO.File.Exists(filePath))
					throw new FileNotFoundException($"File not found: {filePath}");

				// Get the file name
				string fileName = Path.GetFileName(filePath);

				// Search for existing file with the same name in the specified folder
				var searchRequest = service.Files.List();
				searchRequest.Q = $"name = '{fileName}' and trashed = false";
				if (!string.IsNullOrEmpty(folderId))
				{
					searchRequest.Q += $" and '{folderId}' in parents";
				}
				searchRequest.Fields = "files(id, name)";

				var searchResult = searchRequest.Execute();
				var existingFile = searchResult.Files.FirstOrDefault();

				// If the file exists, delete it
				if (existingFile != null)
				{
					service.Files.Delete(existingFile.Id).Execute();
				}

				// Create a new file metadata
				var fileMetadata = new Google.Apis.Drive.v3.Data.File
				{
					Name = fileName
				};

				// Add the folder ID to the metadata if specified
				if (!string.IsNullOrEmpty(folderId))
				{
					fileMetadata.Parents = new List<string> { folderId };
				}

				// Upload the file
				using (var stream = new FileStream(filePath, FileMode.Open))
				{
					var uploadRequest = service.Files.Create(fileMetadata, stream, mimeType);
					uploadRequest.Fields = "id, name, parents";

					// Attach the progress callback
					uploadRequest.ProgressChanged += (progress) =>
					{
						if (progressCallback != null)
						{
							if (progress.Status == UploadStatus.Uploading)
							{
								// Calculate progress percentage
								int percentage = (int)((progress.BytesSent * 100) / stream.Length);
								progressCallback(percentage, fileName);
							}
						}
					};

					// Start the upload
					var uploadResponse = uploadRequest.Upload();

					if (uploadResponse.Status == UploadStatus.Failed)
					{
						throw new Exception($"Upload failed: {uploadResponse.Exception}");
					}
				}
			});
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

		public string FindItem(string itemName, bool isFolder = false, bool createIfNotFound = false)
		{
			// Set MIME type based on whether it's a folder or file
			string? mimeType = isFolder ? "application/vnd.google-apps.folder" : null;

			// Create the request to search for files or folders
			var request = service.Files.List();
			request.Q = $"name = '{itemName}' and trashed = false";
			if (mimeType != null)
			{
				request.Q += $" and mimeType = '{mimeType}'";
			}
			request.Fields = "files(id, name)";

			// Execute the request
			var result = request.Execute();

			// If the item exists, return its ID
			var item = result.Files.FirstOrDefault();
			if (item != null)
			{
				return item.Id;
			}
			else
			{
				// If item doesn't exist and createIfNotFound is true, create a new folder
				if (createIfNotFound && isFolder)
				{
					// Create a new folder
					var newFolderMetadata = new Google.Apis.Drive.v3.Data.File
					{
						Name = itemName,
						MimeType = "application/vnd.google-apps.folder"
					};

					var createRequest = service.Files.Create(newFolderMetadata);
					var newFolder = createRequest.Execute();

					// Return the ID of the newly created folder
					return newFolder.Id;
				}
				else if (createIfNotFound && !isFolder)
				{
					throw new NotSupportedException("Automatic creation of files is not supported.");
				}
				else
				{
					// Throw an exception if the item doesn't exist and createIfNotFound is false
					throw new FileNotFoundException($"Item '{itemName}' not found.");
				}
			}
		}

		public async Task<string> GetFileContents(string fileId)
		{
			try
			{
				// Ensure the service is initialized
				if (service == null)
				{
					throw new InvalidOperationException("Drive service is not initialized.");
				}

				// Get the file stream from Google Drive
				var request = service.Files.Get(fileId);
				using (var memoryStream = new MemoryStream())
				{
					await request.DownloadAsync(memoryStream);
					memoryStream.Seek(0, SeekOrigin.Begin);

					// Read the file contents as a string
					using (var reader = new StreamReader(memoryStream))
					{
						return await reader.ReadToEndAsync();
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Failed to get file contents: {ex.Message}", ex);
			}
		}


		public bool FileExists(string fileName, string? folderId = null)
		{
			// Create the request to list files
			var request = service.Files.List();

			// Build the query to search for the file by name
			request.Q = $"name = '{fileName}' and trashed = false";
			if (!string.IsNullOrEmpty(folderId))
			{
				request.Q += $" and '{folderId}' in parents";
			}

			// Limit the fields to the file IDs
			request.Fields = "files(id)";

			// Execute the request
			var result = request.Execute();

			// Return true if any files are found, otherwise false
			return result.Files.Any();
		}


		void IDisposable.Dispose()
		{
			service.Dispose();
		}
	}
}
