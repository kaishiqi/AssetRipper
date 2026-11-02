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

internal class DataConfigReportTests
{
	private const string AssetsPath = "/output/ExportedProject/Assets";
	private const string ConfigPath = AssetsPath + "/DataConfig/TestConfig.asset";
	private const string ReportPath = "/output/AuxiliaryFiles/dataconfig-guid-report.json";

	private const string MatchGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
	private const string StaleGuid = "cccccccccccccccccccccccccccccccc";
	private const string ActualGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
	private const string MissingGuid = "dddddddddddddddddddddddddddddddd";

	[Test]
	public void DataConfigGuidsAreClassified()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(AssetsPath + "/DataConfig");
		fileSystem.Directory.Create(AssetsPath + "/Foo");
		fileSystem.File.WriteAllText(AssetsPath + "/Foo/Bar.prefab.meta", $"fileFormatVersion: 2\nguid: {MatchGuid}\n");
		fileSystem.File.WriteAllText(AssetsPath + "/Foo/Baz.prefab.meta", $"fileFormatVersion: 2\nguid: {ActualGuid}\n");
		fileSystem.File.WriteAllText(ConfigPath, $"""
			MonoBehaviour:
			  resConfigList:
			  - fileKey: Foo/Bar.prefab
			    assetReference:
			      m_AssetGUID: {MatchGuid}
			  - fileKey: Foo/Baz.prefab
			    assetReference:
			      m_AssetGUID: {StaleGuid}
			  - fileKey: Foo/Missing.prefab
			    assetReference:
			      m_AssetGUID: {MissingGuid}
			""");

		WriteReport(fileSystem);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"TotalCount\": 3"));
			Assert.That(json, Does.Contain("\"MatchCount\": 1"));
			Assert.That(json, Does.Contain("\"MismatchCount\": 1"));
			Assert.That(json, Does.Contain("\"MissingAssetCount\": 1"));
			Assert.That(json, Does.Contain("\"Kind\": \"guid_mismatch\""));
			Assert.That(json, Does.Contain("\"Kind\": \"asset_missing\""));
			Assert.That(json, Does.Contain("\"ActualGuid\": \"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\""));
		}
	}

	[Test]
	public void MissingDataConfigDirectoryWritesEmptyReport()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(AssetsPath);

		WriteReport(fileSystem);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"TotalCount\": 0"));
			Assert.That(json, Does.Contain("\"Problems\": []"));
		}
	}

	private static void WriteReport(VirtualFileSystem fileSystem)
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		new DataConfigReportPostExporter().DoPostExport(CreateGameData(), settings, fileSystem);
	}

	private static GameData CreateGameData()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
