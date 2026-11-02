using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.IO.Files;

namespace AssetRipper.Tests;

internal class PathCasingRestorerTests
{
	private const string ProjectRoot = "/output/ExportedProject";
	private const string LowercaseDirectory = ProjectRoot + "/Assets/Resources/spriteassets/sub";
	private const string CanonicalDirectory = ProjectRoot + "/Assets/Resources/SpriteAssets/Sub";

	[Test]
	public void DirectoryCasingIsRestoredFromReference()
	{
		VirtualFileSystem fileSystem = new();
		CreateDirectory(fileSystem, "/reference/ExportedProject/Assets/Resources/SpriteAssets/Sub");
		CreateDirectory(fileSystem, LowercaseDirectory + "/sub");
		fileSystem.File.WriteAllText(LowercaseDirectory + "/icon.png", "content");
		fileSystem.File.WriteAllText(LowercaseDirectory + "/sub/data.txt", "data");

		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		settings.ExportSettings.ReferenceProjectPath = "/reference";

		PathCasingRestorer.Restore(settings, fileSystem);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.Directory.Exists(CanonicalDirectory), Is.True);
			Assert.That(fileSystem.Directory.Exists(LowercaseDirectory), Is.False);
			Assert.That(fileSystem.File.ReadAllText(CanonicalDirectory + "/icon.png"), Is.EqualTo("content"));
			Assert.That(fileSystem.File.ReadAllText(CanonicalDirectory + "/sub/data.txt"), Is.EqualTo("data"));
		}
	}

	[Test]
	public void NoReferenceLeavesPathsUnchanged()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(LowercaseDirectory);

		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";

		PathCasingRestorer.Restore(settings, fileSystem);

		Assert.That(fileSystem.Directory.Exists(LowercaseDirectory), Is.True);
	}

	private static void CreateDirectory(FileSystem fileSystem, string path)
	{
		fileSystem.Directory.Create(path);
	}
}
