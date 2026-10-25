using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Extensions;
using System.Reflection;

namespace AssetRipper.Tests;

internal class MissingReferencesReportTests
{
	private const string MaterialPath = "/output/ExportedProject/Assets/Material/Material.mat";
	private const string ReportPath = "/output/AuxiliaryFiles/missing-references.json";

	[Test]
	public void MissingShaderReferenceIsReported()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		IMaterial material = collection.CreateMaterial();
		SetPathID(material.Shader_C21, 123456789L);

		Export(fileSystem, collection);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(MaterialPath), Does.Contain("0000000deadbeef15deadf00d0000000"));
			Assert.That(json, Does.Contain("Assets/Material/Material.mat"));
			Assert.That(json, Does.Contain("\"FieldPath\": \"m_Shader\""));
			Assert.That(json, Does.Contain("\"TargetClassID\": 48"));
			Assert.That(json, Does.Contain("\"TargetPathID\": 123456789"));
			Assert.That(json, Does.Contain("\"Reason\": \"AssetNotFound\""));
			Assert.That(json, Does.Contain("\"TotalCount\": 1"));
		}
	}

	[Test]
	public void CleanExportWritesEmptyReport()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateMesh();

		Export(fileSystem, collection);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.Exists(ReportPath), Is.True);
			Assert.That(fileSystem.File.ReadAllText(ReportPath), Does.Contain("\"TotalCount\": 0"));
		}
	}

	[Test]
	public void StrictModeThrowsAfterWritingReport()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		IMaterial material = collection.CreateMaterial();
		SetPathID(material.Shader_C21, 123456789L);

		Assert.Throws<InvalidOperationException>(() => Export(fileSystem, collection, settings => settings.ExportSettings.FailOnMissingReferences = true));
		Assert.That(fileSystem.File.Exists(ReportPath), Is.True);
	}

	private static void Export(VirtualFileSystem fileSystem, ProcessedAssetCollection collection, Action<FullConfiguration>? configure = null)
	{
		FullConfiguration settings = new();
		configure?.Invoke(settings);
		new ExportHandler(settings).Export(CreateGameData(collection), "/output", fileSystem);
	}

	private static void SetPathID(object pptr, long pathID)
	{
		FieldInfo? field = pptr.GetType().GetField("m_PathID_", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Could not find m_PathID_ on {pptr.GetType()}");
		field.SetValue(pptr, pathID);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
