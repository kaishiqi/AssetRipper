using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Processing;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class ReferenceGuidSourceTests
{
	private const string KnownGuid = "0123456789abcdef0123456789abcdef";
	private const string MetaPath = "/output/ExportedProject/Assets/Mesh/Mesh.asset.meta";

	[Test]
	public void MatchingPathReusesReferenceGuid()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Mesh.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference");

		Assert.That(fileSystem.File.ReadAllText(MetaPath), Does.Contain($"guid: {KnownGuid}"));
	}

	[Test]
	public void EmptyReferenceGeneratesNewGuids()
	{
		VirtualFileSystem fileSystem = new();

		string firstGuid = ExportAndReadGuid(fileSystem);
		string secondGuid = ExportAndReadGuid(new VirtualFileSystem());

		using (Assert.EnterMultipleScope())
		{
			Assert.That(firstGuid, Is.Not.EqualTo(KnownGuid));
			Assert.That(secondGuid, Is.Not.EqualTo(KnownGuid));
			Assert.That(firstGuid, Is.Not.EqualTo(secondGuid));
		}
	}

	[Test]
	public void UnmatchedPathFallsBackToNewGuid()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Other.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference");

		Assert.That(fileSystem.File.ReadAllText(MetaPath), Does.Not.Contain($"guid: {KnownGuid}"));
	}

	[Test]
	public void ReferenceRootWithoutExportedProjectFolderIsSupported()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/Assets/Mesh/Mesh.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference");

		Assert.That(fileSystem.File.ReadAllText(MetaPath), Does.Contain($"guid: {KnownGuid}"));
	}

	[Test]
	public void DuplicateOutputPathsDoNotShareGuid()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Mesh.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference", meshCount: 2);

		string firstMeta = fileSystem.File.ReadAllText(MetaPath);
		string secondMeta = fileSystem.File.ReadAllText("/output/ExportedProject/Assets/Mesh/Mesh_0.asset.meta");
		using (Assert.EnterMultipleScope())
		{
			Assert.That(firstMeta, Does.Contain($"guid: {KnownGuid}"));
			Assert.That(secondMeta, Does.Not.Contain($"guid: {KnownGuid}"));
		}
	}

	[Test]
	public void GuidMapIsWritten()
	{
		VirtualFileSystem fileSystem = new();

		Export(fileSystem);

		string guidMapPath = "/output/AuxiliaryFiles/guid-map.json";
		Assert.That(fileSystem.File.Exists(guidMapPath), Is.True);
		Assert.That(fileSystem.File.ReadAllText(guidMapPath), Does.Contain("Assets/Mesh/Mesh.asset"));
	}

	private static string ExportAndReadGuid(VirtualFileSystem fileSystem)
	{
		Export(fileSystem);
		string metaText = fileSystem.File.ReadAllText(MetaPath);
		foreach (string line in metaText.Split('\n'))
		{
			if (line.StartsWith("guid:", StringComparison.Ordinal))
			{
				return line[5..].Trim();
			}
		}
		throw new InvalidOperationException("No guid was exported.");
	}

	private static void Export(VirtualFileSystem fileSystem, Action<FullConfiguration>? configure = null, int meshCount = 1)
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		for (int i = 0; i < meshCount; i++)
		{
			collection.CreateMesh();
		}

		FullConfiguration settings = new();
		configure?.Invoke(settings);
		new ExportHandler(settings).Export(CreateGameData(collection), "/output", fileSystem);
	}

	private static void WriteMeta(FileSystem fileSystem, string path, string guid)
	{
		string directory = fileSystem.Path.GetDirectoryName(path)!;
		fileSystem.Directory.Create(directory);
		fileSystem.File.WriteAllText(path, $"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n");
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
