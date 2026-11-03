using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Textures;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class TextureReportTests
{
	private const string ReportPath = "/output/AuxiliaryFiles/texture-report.json";

	[Test]
	public void PlaceholderTextureIsReported()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateTexture2D();

		WriteReport(fileSystem, collection);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"TotalCount\": 1"));
			Assert.That(json, Does.Contain("\"PlaceholderCount\": 1"));
			Assert.That(json, Does.Contain("\"Width\": 0"));
		}
	}

	[Test]
	public void NonTextureAssetIsNotCounted()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateMesh();

		WriteReport(fileSystem, collection);

		Assert.That(fileSystem.File.ReadAllText(ReportPath), Does.Contain("\"TotalCount\": 0"));
	}

	private static void WriteReport(VirtualFileSystem fileSystem, ProcessedAssetCollection collection)
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		new TextureReportPostExporter().DoPostExport(CreateGameData(collection), settings, fileSystem);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
