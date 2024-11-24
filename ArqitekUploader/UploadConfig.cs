using MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ArqitekUploader
{
	[MessagePackObject]
	public struct UploadConfig
	{
		[Key(0)]
		public string? name;

		//[0]win [1]linx [2]linxarm
		[Key(1)]
		public string?[] folds;

		[Key(2)]
		public string?[] archives;

		[Key(3)]
		public string? webhook;

		[Key(4)]
		public string? message;

		[Key(5)]
		public bool versioning;

		public static UploadConfig FromFile(string file)
		{
			if (!File.Exists(file)) throw new FileNotFoundException();
			using var stream = File.OpenRead(file);
			return MessagePackSerializer.Deserialize<UploadConfig>(stream);
		}

		public UploadConfig()
		{
			name = null;
			folds = new string?[3];
			archives = new string?[3];
			webhook = null;
		}

		public void Serialize(string file)
		{
			File.WriteAllBytes(file, MessagePackSerializer.Serialize(this));
		}
		public static UploadConfig CreateConfig()
		{
			string? name = null;
			string?[] folds = new string?[3];
			string?[] archives = new string?[3];
			string? webhook;
			string? message;
			bool versioning = false;

			Console.Write("Project Name: ");
			name = Console.ReadLine();

			Console.Write("Windows Folder path: ");
			folds[0] = Console.ReadLine();

			Console.Write("Linux Folder path: ");
			folds[1] = Console.ReadLine();

			Console.Write("LinuxArm64 Folder path: ");
			folds[2] = Console.ReadLine();

			Console.Write("Windows Archive path: ");
			archives[0] = Console.ReadLine();

			Console.Write("Linux Archive path: ");
			archives[1] = Console.ReadLine();

			Console.Write("LinuxArm64 Archive path: ");
			archives[2] = Console.ReadLine();

			Console.Write("Webhook URL: ");
			webhook = Console.ReadLine();

			Console.Write("Update Message (<version> for version)");
			message = Console.ReadLine();

			Console.Write("Version System (y or n (default n)): ");
			var buffer = Console.ReadLine();
			if (buffer?.ToLower() == "y") versioning = true;

			return new UploadConfig { name = name, folds = folds, archives = archives, webhook = webhook, message = message, versioning = versioning };
		}
		
	}
}
