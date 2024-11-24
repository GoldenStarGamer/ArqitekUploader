using ArqitekUploader;
using System.Security.AccessControl;
using System.Security.Cryptography;

internal class Program
{
	public static void Main(string[] args)
	{
		Console.WriteLine("Arqitek Upload Tool");
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
		using (Drive drive = new())
		{
			foreach (var file in drive.List())
			{
				Console.WriteLine(file.Name);
			}
		}

	}
	
}