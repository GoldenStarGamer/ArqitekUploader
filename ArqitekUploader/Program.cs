using ArqitekUploader;
using System.Data;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;

internal class Program
{
	static void clear()
	{
		Console.Clear();
		Console.WriteLine("Arqitek Upload Tool");
	}
	public static void Main(string[] args)
	{
		clear();
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
			if(config.folds[i] is not null && config.archives[i] is not null)
			{
				archives.Add(new(config.folds[i]!, config.archives[i]!));
			}
		}

		clear();
		List<Task> tasks = [];
		foreach (var item in archives)
		{
			tasks.Add(Task.Run(() => item.Compress(Enprint)));
		}

		Task.WaitAll(tasks.ToArray());

		clear();
		using (Drive drive = new())
		{
			tasks.Clear();
			for (int i = 0; i < 3; i++)
			{
				if (config.archives[i] is not null)
				{
					if (config.name is not null)
						tasks.Add(drive.UploadFile(config.archives[i]!, "application/x-zip", drive.FindItem(config.name, true, true), Enprint));
					else tasks.Add(drive.UploadFile(config.archives[i]!, "application/x-zip"));
				}
			}
			Task.WaitAll(tasks.ToArray());
		}

		clear();
		if (config.webhook is not null and not "")
		SendWebhookMessage(config.webhook, config.message is null or "" ? (version is null or "" ? "A new version has been released" : "Version <version> has been released") : config.message , version ?? "").Wait();

		clear();
		Console.WriteLine("Upload Complete");
		Console.ReadLine();
		Console.Clear();
	}

	static Dictionary<Archive, int> Arlines = new Dictionary<Archive, int>();
	static Dictionary<string, int> Stlines = new Dictionary<string, int>();

	static object locker = new();

	// Track the starting cursor position for each line
	public static void Enprint(float progress, Archive self)
	{
		// If the Archive hasn't been added, add it and store its cursor line position
		if (!Arlines.ContainsKey(self))
		{
			lock (Arlines)
			{
				Arlines.Add(self, Arlines.Count+1);
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

		using (HttpClient client = new HttpClient())
		{
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