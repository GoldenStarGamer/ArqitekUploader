using ArqitekUploader;
using System.Data;
using System.Text;

internal class Program
{
	static void Clear()
	{
		Console.Clear();
		Console.WriteLine("Arqitek Upload Tool");
	}
	public static void Main()
	{
		try
		{
			Clear();
			UploadConfig config;
			if (!File.Exists("config.msgpack"))
			{
				Console.Write("Config file (write $new to make a new config): ");

				string? filepath = null;

				while (filepath is null)
				{
					filepath = Console.ReadLine();
				}

				if (filepath is "$new")
				{
					config = UploadConfig.CreateConfig();
					config.Serialize("config.msgpack");
				}
				else
				{
					config = UploadConfig.FromFile(filepath);
				}
			}
			else
			{
				config = UploadConfig.FromFile("config.msgpack");
			}

			string version = "";

			if (config.versioning)
			{

				using (var drive = new Drive())
				{
					if (drive.FileExists("version.txt"))
					{
						Console.WriteLine($"Previous version: {drive.GetFileContents(drive.FindItem("version.txt")).Result}");
					}
					Console.Write("Version: ");
					string? _ver = null;
					while (_ver is null)
						_ver = Console.ReadLine();
					version = _ver;
					File.WriteAllText("version.txt", version);
					if (config.name is null)
						drive.UploadFile("version.txt", "text/plain").Wait();
					else drive.UploadFile("version.txt", "text/plain", drive.FindItem(config.name, true, true)).Wait();
				}

				foreach (var item in (from path in config.folds where path is not null select path))
				{
					if (config.name is null)
						File.Copy("version.txt", Path.Combine(item, "version.txt"), true);
					else File.Copy("version.txt", Path.Combine(item, config.name, "version.txt"), true);
				}

			}

			List<Archive> archives = [];
			for (int i = 0; i < 3; i++)
			{
				if (!string.IsNullOrEmpty(config.folds[i]) && !string.IsNullOrEmpty(config.archives[i]))
				{
					archives.Add(new(config.folds[i]!, config.archives[i]!));
				}
			}

			Clear();
			List<Task> tasks = [];
			foreach (var item in archives)
			{
				tasks.Add(Task.Run(() => item.Compress(Enprint)));
			}

			Task.WaitAll([.. tasks]);

			Clear();
			using (Drive drive = new())
			{
				tasks.Clear();
				for (int i = 0; i < 3; i++)
				{
					if (!string.IsNullOrEmpty(config.archives[i]))
					{
						if (config.name is not null)
							tasks.Add(drive.UploadFile(config.archives[i]!, "application/x-zip", drive.FindItem(config.name, true, true), Enprint));
						else tasks.Add(drive.UploadFile(config.archives[i]!, "application/x-zip"));
					}
				}
				Task.WaitAll([.. tasks]);
			}

			Clear();
			if (!string.IsNullOrEmpty(config.webhook))
				SendWebhookMessage(config.webhook, string.IsNullOrEmpty(config.message) ? (string.IsNullOrEmpty(version) ? "A new version has been released" : "Version <version> has been released") : config.message, version ?? string.Empty).Wait();

			Clear();
			Console.WriteLine("Upload Complete");
			Console.ReadLine();
			Console.Clear();
		}
		catch (Exception ex)
		{
			Console.WriteLine("ERROR:" + ex.Message);
			Console.ReadLine();
		}

	}

	readonly static Dictionary<Archive, int> Arlines = [];
	readonly static Dictionary<string, int> Stlines = [];

	static readonly object locker = new();

	// Track the starting cursor position for each line
	public static void Enprint(float progress, Archive self)
	{
		// If the Archive hasn't been added, add it and store its cursor line position
		if (!Arlines.ContainsKey(self))
		{
			lock (Arlines)
			{
				Arlines.Add(self, Arlines.Count + 1);
			}
		}
		lock (locker)
		{
			// Move the cursor to the stored position for this Archive
			Console.SetCursorPosition(0, Arlines[self]);
			Console.WriteLine($"{self.folder} Compress: {Math.Round(progress)}%");
		}
	}

	public static async Task SendWebhookMessage(string webhookUrl, string message, string version)
	{
		if (string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(message))
		{
			Console.WriteLine("Webhook URL or message is missing.");
			return;
		}

		// Replace <version> in the message
		string formattedMessage = message.Replace("<version>", version);

		// Create JSON payload
		string jsonPayload = $"{{\"content\": \"{formattedMessage}\"}}";

		using HttpClient client = new();
		try
		{
			HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
			HttpResponseMessage response = await client.PostAsync(webhookUrl, content);

			if (response.IsSuccessStatusCode)
			{
				Console.WriteLine("Webhook message sent successfully.");
			}
			else
			{
				Console.WriteLine($"Failed to send webhook message. Status code: {response.StatusCode}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error sending webhook message: {ex.Message}");
		}
	}

	public static void Enprint(float progress, string filename)
	{
		// If the filename hasn't been added, add it and store its cursor line position
		if (!Stlines.ContainsKey(filename))
		{
			lock (Stlines)
			{
				Stlines.Add(filename, Stlines.Count + 1);
			}
		}

		lock (locker)
		{
			// Move the cursor to the stored position for this filename
			Console.SetCursorPosition(0, Stlines[filename]);
			Console.WriteLine($"{filename} Upload: {Math.Round(progress)}%");
		}
	}
}