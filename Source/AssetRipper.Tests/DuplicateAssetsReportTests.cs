using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Diagnostics;
using AssetRipper.IO.Files;

namespace AssetRipper.Tests;

internal class DuplicateAssetsReportTests
{
	private const string TextureDirectory = "/output/ExportedProject/Assets/Texture2D";
	private const string ReportPath = "/output/AuxiliaryFiles/duplicate-assets.json";

	[Test]
	public void NumberedCopyNextToBaseAssetIsReported()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(TextureDirectory);
		fileSystem.File.WriteAllText(TextureDirectory + "/icon.png", "abc");
		fileSystem.File.WriteAllText(TextureDirectory + "/icon_0.png", "abcd");
		fileSystem.File.WriteAllText(TextureDirectory + "/other_5.png", "unrelated");

		DuplicateAssetsReport.Write(CreateSettings(), fileSystem);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"TotalPairCount\": 1"));
			Assert.That(json, Does.Contain("Assets/Texture2D/icon.png"));
			Assert.That(json, Does.Contain("Assets/Texture2D/icon_0.png"));
			Assert.That(json, Does.Not.Contain("other_5.png"));
		}
	}

	[Test]
	public void EmptyProjectWritesEmptyReport()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(TextureDirectory);

		DuplicateAssetsReport.Write(CreateSettings(), fileSystem);

		Assert.That(fileSystem.File.ReadAllText(ReportPath), Does.Contain("\"TotalPairCount\": 0"));
	}

	private static FullConfiguration CreateSettings()
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		return settings;
	}
}
