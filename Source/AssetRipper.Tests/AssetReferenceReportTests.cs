using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Addressables;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Extensions;
using System.Text;

namespace AssetRipper.Tests;

internal class AssetReferenceReportTests
{
	private const string AssetsPath = "/output/ExportedProject/Assets";
	private const string ConfigPath = AssetsPath + "/Data/Config.asset";
	private const string MetaPath = AssetsPath + "/Data/Target.asset.meta";
	private const string CatalogPath = AssetsPath + "/StreamingAssets/aa/catalog.json";
	private const string ReportPath = "/output/AuxiliaryFiles/asset-references.json";

	private const string UnityGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
	private const string CatalogKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
	private const string MissingGuid = "cccccccccccccccccccccccccccccccc";

	[Test]
	public void AssetReferenceGuidsAreClassified()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(AssetsPath + "/Data");
		fileSystem.Directory.Create(AssetsPath + "/StreamingAssets/aa");
		fileSystem.File.WriteAllText(MetaPath, $"fileFormatVersion: 2\nguid: {UnityGuid}\n");
		fileSystem.File.WriteAllText(ConfigPath, $"""
			MonoBehaviour:
			  m_First:
			    m_AssetGUID: {UnityGuid}
			  m_Second:
			    m_AssetGUID: {CatalogKey}
			  m_Third:
			    m_AssetGUID: {MissingGuid}
			""");
		fileSystem.File.WriteAllText(CatalogPath, CreateCatalog(CatalogKey));

		WriteReport(fileSystem);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"CatalogFound\": true"));
			Assert.That(json, Does.Contain("\"UnityGuidCount\": 1"));
			Assert.That(json, Does.Contain("\"CatalogKeyCount\": 1"));
			Assert.That(json, Does.Contain("\"MissingCount\": 1"));
			Assert.That(json, Does.Contain("Assets/Data/Config.asset"));
		}
	}

	[Test]
	public void MissingCatalogLeavesUnknownGuidsUnclassified()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(AssetsPath + "/Data");
		fileSystem.File.WriteAllText(ConfigPath, $"""
			MonoBehaviour:
			  m_Ref:
			    m_AssetGUID: {CatalogKey}
			""");

		WriteReport(fileSystem);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"CatalogFound\": false"));
			Assert.That(json, Does.Contain("\"MissingCount\": 1"));
		}
	}

	private static string CreateCatalog(string key)
	{
		byte[] keyData = Encoding.UTF8.GetBytes(key);
		return $$"""{"m_KeyDataString": "{{Convert.ToBase64String(keyData)}}"}""";
	}

	private static void WriteReport(VirtualFileSystem fileSystem)
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		new AssetReferenceReportPostExporter().DoPostExport(CreateGameData(), settings, fileSystem);
	}

	private static GameData CreateGameData()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
