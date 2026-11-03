using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Diagnostics;
using AssetRipper.IO.Files;

namespace AssetRipper.Tests;

internal class TextReferenceRepairerTests
{
	private const string ProjectRoot = "/output/ExportedProject";
	private const string SpineDirectory = ProjectRoot + "/Assets/Spine";
	private const string AtlasPath = SpineDirectory + "/hero.atlas";
	private const string ReportPath = "/output/AuxiliaryFiles/renamed-textures.json";

	[Test]
	public void AtlasPageNameIsRepairedFromRename()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(SpineDirectory);
		fileSystem.File.WriteAllText(AtlasPath, "rukou.png\nsize: 1024,1024\nformat: RGBA8888\n");
		fileSystem.File.WriteAllText(SpineDirectory + "/rukou_0.png", "image");

		AssetRenameInfo rename = new()
		{
			AssetName = "rukou",
			ClassID = 28,
			OriginalFileName = "rukou.png",
			ExportedFileName = "rukou_0.png",
			Directory = SpineDirectory,
		};

		TextReferenceRepairer.Repair(CreateSettings(), fileSystem, [rename]);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(AtlasPath), Does.StartWith("rukou_0.png\nsize: 1024,1024"));
			Assert.That(json, Does.Contain("\"OldName\": \"rukou.png\""));
			Assert.That(json, Does.Contain("\"NewName\": \"rukou_0.png\""));
			Assert.That(json, Does.Contain("\"RepairedFileCount\": 1"));
			Assert.That(json, Does.Contain("\"UnresolvedCount\": 0"));
		}
	}

	[Test]
	public void AtlasPageWithoutExtensionKeepsTheRenameStyle()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(SpineDirectory);
		fileSystem.File.WriteAllText(AtlasPath, "rukou\nsize: 512,512\n");
		fileSystem.File.WriteAllText(SpineDirectory + "/rukou_0.png", "image");

		AssetRenameInfo rename = new()
		{
			AssetName = "rukou",
			ClassID = 28,
			OriginalFileName = "rukou.png",
			ExportedFileName = "rukou_0.png",
			Directory = SpineDirectory,
		};

		TextReferenceRepairer.Repair(CreateSettings(), fileSystem, [rename]);

		Assert.That(fileSystem.File.ReadAllText(AtlasPath), Does.StartWith("rukou_0\nsize: 512,512"));
	}

	[Test]
	public void PageFoundInAnotherDirectoryIsNotReported()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(SpineDirectory);
		fileSystem.Directory.Create(ProjectRoot + "/Assets/Texture2D");
		fileSystem.File.WriteAllText(AtlasPath, "fire_2.png\nsize: 64,64\n");
		fileSystem.File.WriteAllText(ProjectRoot + "/Assets/Texture2D/fire_2.png", "image");

		AssetRenameInfo rename = new()
		{
			AssetName = "other",
			ClassID = 28,
			OriginalFileName = "other.png",
			ExportedFileName = "other_0.png",
			Directory = ProjectRoot + "/Assets/Texture2D",
		};

		TextReferenceRepairer.Repair(CreateSettings(), fileSystem, [rename]);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(AtlasPath), Does.StartWith("fire_2.png"));
			Assert.That(json, Does.Contain("\"UnresolvedCount\": 0"));
			Assert.That(json, Does.Contain("\"Repairs\": []"));
		}
	}

	[Test]
	public void UnmatchedPageIsReported()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(SpineDirectory);
		fileSystem.File.WriteAllText(AtlasPath, "missing.png\nsize: 64,64\n");

		AssetRenameInfo rename = new()
		{
			AssetName = "other",
			ClassID = 28,
			OriginalFileName = "other.png",
			ExportedFileName = "other_0.png",
			Directory = SpineDirectory,
		};

		TextReferenceRepairer.Repair(CreateSettings(), fileSystem, [rename]);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(AtlasPath), Does.StartWith("missing.png"));
			Assert.That(json, Does.Contain("\"UnresolvedCount\": 1"));
			Assert.That(json, Does.Contain("\"Name\": \"missing.png\""));
		}
	}

	[Test]
	public void ExistingPageIsLeftAlone()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(SpineDirectory);
		fileSystem.File.WriteAllText(AtlasPath, "hero.png\nsize: 128,128\n");
		fileSystem.File.WriteAllText(SpineDirectory + "/hero.png", "image");

		AssetRenameInfo rename = new()
		{
			AssetName = "hero",
			ClassID = 28,
			OriginalFileName = "hero.png",
			ExportedFileName = "hero_1.png",
			Directory = SpineDirectory,
		};

		TextReferenceRepairer.Repair(CreateSettings(), fileSystem, [rename]);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(AtlasPath), Does.StartWith("hero.png"));
			Assert.That(json, Does.Contain("\"Repairs\": []"));
		}
	}

	[Test]
	public void RepairCanBeDisabled()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(SpineDirectory);
		fileSystem.File.WriteAllText(AtlasPath, "rukou.png\nsize: 1024,1024\n");
		fileSystem.File.WriteAllText(SpineDirectory + "/rukou_0.png", "image");

		AssetRenameInfo rename = new()
		{
			AssetName = "rukou",
			ClassID = 28,
			OriginalFileName = "rukou.png",
			ExportedFileName = "rukou_0.png",
			Directory = SpineDirectory,
		};

		FullConfiguration settings = CreateSettings();
		settings.ExportSettings.RepairTextReferences = false;
		TextReferenceRepairer.Repair(settings, fileSystem, [rename]);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(AtlasPath), Does.StartWith("rukou.png"));
			Assert.That(json, Does.Contain("\"TotalRenameCount\": 1"));
			Assert.That(json, Does.Contain("\"Repairs\": []"));
		}
	}

	private static FullConfiguration CreateSettings()
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		return settings;
	}
}
