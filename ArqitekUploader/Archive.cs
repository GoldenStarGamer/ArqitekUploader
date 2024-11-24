using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArqitekUploader
{
	
	public class Archive
	{
		public delegate void OnProgressChanged(float progress, Archive self);

		public string folder { get; private set; }
		string archive;

		public Archive(string fold, string arch)
		{
			folder = fold;
			archive = arch;
		}
		public async Task Compress(OnProgressChanged? onProgress = null, int maxDegreeOfParallelism = 10)
		{
			if (!Directory.Exists(folder))
			{
				throw new DirectoryNotFoundException("Folder Does Not Exist");
			}
			using (var zip = ZipArchive.Create())
			{
				var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

				// Calculate the total size of all files
				long totalSize = files.Sum(file => new FileInfo(file).Length);
				long processedSize = 0;

				var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
				object lockObject = new object();

				var tasks = files.Select(async file =>
				{
					await semaphore.WaitAsync(); // Limit concurrency

					try
					{
						var relativePath = Path.GetRelativePath(folder, file);
						var fileInfo = new FileInfo(file);

						// Add file to the archive
						lock (lockObject)
						{
							zip.AddEntry(relativePath, file);
						}

						// Update processed size and report progress
						lock (lockObject)
						{
							processedSize += fileInfo.Length;
							float progress = (float)processedSize / totalSize * 100f;
							onProgress?.Invoke(progress, this);
						}
					}
					finally
					{
						semaphore.Release(); // Release the semaphore
					}
				}).ToList();

				await Task.WhenAll(tasks);

				// Save the archive after all files have been added
				zip.SaveTo(archive, CompressionType.Deflate);
			}
			
		}

	}
}
