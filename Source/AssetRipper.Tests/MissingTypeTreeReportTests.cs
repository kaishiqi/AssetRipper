using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Diagnostics;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class MissingTypeTreeReportTests
{
	private const string ReportPath = "/output/AuxiliaryFiles/missing-typetree.json";

	[Test]
	public void ProcessedCollectionsAreIgnored()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateMesh();

		WriteReport(fileSystem, collection);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"TypelessCollectionCount\": 0"));
			Assert.That(json, Does.Contain("\"TypelessMonoBehaviourCount\": 0"));
			Assert.That(json, Does.Contain("\"Collections\": []"));
		}
	}

	private static void WriteReport(VirtualFileSystem fileSystem, ProcessedAssetCollection collection)
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		new MissingTypeTreeReportPostExporter().DoPostExport(CreateGameData(collection), settings, fileSystem);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
