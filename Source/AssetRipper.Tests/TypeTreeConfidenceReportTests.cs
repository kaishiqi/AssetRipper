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

internal class TypeTreeConfidenceReportTests
{
	private const string ReportPath = "/output/AuxiliaryFiles/typetree-confidence.json";

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
			Assert.That(json, Does.Contain("\"EvaluatedCount\": 0"));
			Assert.That(json, Does.Contain("\"SuspiciousCount\": 0"));
			Assert.That(json, Does.Contain("\"Entries\": []"));
		}
	}

	private static void WriteReport(VirtualFileSystem fileSystem, ProcessedAssetCollection collection)
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		new TypeTreeConfidenceReportPostExporter().DoPostExport(CreateGameData(collection), settings, fileSystem);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
