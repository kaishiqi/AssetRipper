using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class AddressablesPostExporterTests
{
	private const string CatalogPath = "/output/ExportedProject/Assets/StreamingAssets/aa/catalog.json";
	private const string SettingsPath = "/output/ExportedProject/Assets/StreamingAssets/aa/settings.json";
	private const string ReportPath = "/output/AuxiliaryFiles/addressables-report.json";

	[Test]
	public void AndroidAddressablesFilesAreCopied()
	{
		VirtualFileSystem inputFileSystem = new();
		inputFileSystem.Directory.Create("/game/assets/aa/Android");
		inputFileSystem.File.WriteAllText("/game/assets/aa/catalog.json", "{}");
		inputFileSystem.File.WriteAllText("/game/assets/aa/settings.json", "{}");
		inputFileSystem.File.WriteAllText("/game/assets/aa/Android/catalog.bundle", "not copied");

		VirtualFileSystem outputFileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateMesh();
		TestPlatformGameStructure platform = new("/game", inputFileSystem);
		GameData gameData = new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), platform);

		new ExportHandler(new FullConfiguration()).Export(gameData, "/output", outputFileSystem);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(outputFileSystem.File.Exists(CatalogPath), Is.True);
			Assert.That(outputFileSystem.File.Exists(SettingsPath), Is.True);
			Assert.That(outputFileSystem.File.Exists("/output/ExportedProject/Assets/StreamingAssets/aa/Android/catalog.bundle"), Is.False);
			Assert.That(outputFileSystem.File.ReadAllText(ReportPath), Does.Contain("\"HasCatalog\": true"));
			Assert.That(outputFileSystem.File.ReadAllText(ReportPath), Does.Contain("\"HasSettings\": true"));
		}
	}

	[Test]
	public void MissingAddressablesDirectoryIsReported()
	{
		VirtualFileSystem inputFileSystem = new();
		inputFileSystem.Directory.Create("/game/assets");
		VirtualFileSystem outputFileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateMesh();
		TestPlatformGameStructure platform = new("/game", inputFileSystem);
		GameData gameData = new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), platform);

		new ExportHandler(new FullConfiguration()).Export(gameData, "/output", outputFileSystem);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(outputFileSystem.File.Exists(ReportPath), Is.True);
			Assert.That(outputFileSystem.File.ReadAllText(ReportPath), Does.Contain("No Addressables"));
			Assert.That(outputFileSystem.File.Exists(CatalogPath), Is.False);
		}
	}

	private sealed class TestPlatformGameStructure : PlatformGameStructure
	{
		public TestPlatformGameStructure(string rootPath, FileSystem fileSystem) : base(rootPath, fileSystem)
		{
		}
	}
}
