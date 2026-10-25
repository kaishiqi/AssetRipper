using System.Diagnostics;

namespace AssetRipper.IO.Files;

public partial class LocalFileSystem : FileSystem
{
	public static LocalFileSystem Instance { get; } = new();

	public partial class LocalFileImplementation
	{
	}

	public partial class LocalDirectoryImplementation
	{
		public override void Create(string path) => System.IO.Directory.CreateDirectory(path);

		public override void Delete(string path) => System.IO.Directory.Delete(path, true);

		public override void Move(string sourceDirName, string destDirName)
		{
			if (string.Equals(sourceDirName, destDirName, StringComparison.Ordinal))
			{
				return;
			}

			// A case-only rename fails on case-insensitive file systems, so go through a temporary name.
			string temporaryPath = destDirName + "." + Guid.NewGuid().ToString("N") + ".tmp";
			System.IO.Directory.Move(sourceDirName, temporaryPath);
			System.IO.Directory.Move(temporaryPath, destDirName);
		}
	}

	public static string ExecutingDirectory => AppContext.BaseDirectory;

	private string LocalTemporaryDirectory => Path.Join(ExecutingDirectory, "temp", GetRandomString()[0..4]);

	private string SystemTemporaryDirectory => Path.Join(System.IO.Path.GetTempPath(), "AssetRipper", GetRandomString()[0..4]);

	public override string TemporaryDirectory
	{
		get
		{
			if (string.IsNullOrEmpty(field))
			{
				field = LocalTemporaryDirectory;
				Debug.Assert(!Directory.Exists(field));
				try
				{
					Directory.Create(field);
					File.WriteAllText(Path.Join(field, ".WriteTest"), "test");
					Directory.Delete(field);
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					field = SystemTemporaryDirectory;
				}
			}
			return field;
		}
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				field = Path.GetFullPath(value);
			}
		}
	}
}
