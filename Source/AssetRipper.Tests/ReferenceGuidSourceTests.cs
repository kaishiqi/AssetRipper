using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Processing;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
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

		Assert.That(ReadGuid(fileSystem, MetaPath), Is.EqualTo(KnownGuid));
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

		Assert.That(ReadGuid(fileSystem, MetaPath), Is.Not.EqualTo(KnownGuid));
	}

	[Test]
	public void ReferenceRootWithoutExportedProjectFolderIsSupported()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/Assets/Mesh/Mesh.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference");

		Assert.That(ReadGuid(fileSystem, MetaPath), Is.EqualTo(KnownGuid));
	}

	[Test]
	public void DuplicateOutputPathsDoNotShareGuid()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Mesh.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference", meshCount: 2);

		string firstGuid = ReadGuid(fileSystem, MetaPath);
		string secondGuid = ReadGuid(fileSystem, "/output/ExportedProject/Assets/Mesh/Mesh_0.asset.meta");
		using (Assert.EnterMultipleScope())
		{
			Assert.That(firstGuid, Is.EqualTo(KnownGuid));
			Assert.That(secondGuid, Is.Not.EqualTo(KnownGuid));
		}
	}

	[Test]
	public void ReferenceGuidIsNotReusedByTwoCollections()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Mesh.asset.meta", KnownGuid);
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Other.asset.meta", KnownGuid);

		ExportNamed(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference", "Mesh", "Other");

		string firstGuid = ReadGuid(fileSystem, MetaPath);
		string secondGuid = ReadGuid(fileSystem, "/output/ExportedProject/Assets/Mesh/Other.asset.meta");
		using (Assert.EnterMultipleScope())
		{
			Assert.That(firstGuid, Is.Not.EqualTo(secondGuid));
			Assert.That(firstGuid == KnownGuid ^ secondGuid == KnownGuid, Is.True);
		}
	}

	[Test]
	public void ReferencesAreRewritten()
	{
		const string oldGuid = "11111111111111111111111111111111";
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Mesh.asset.meta", KnownGuid);
		WriteMeta(fileSystem, "/output/ExportedProject/Assets/Mesh/Mesh.asset.meta", oldGuid);
		WriteFile(fileSystem, "/output/ExportedProject/Assets/Mesh/Mesh.asset",
			$"m_Texture: {{fileID: 2800000, guid: {oldGuid}, type: 3}}\n");

		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		settings.ExportSettings.ReferenceProjectPath = "/reference";

		GuidReconciler.Reconcile(settings, fileSystem);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(ReadGuid(fileSystem, "/output/ExportedProject/Assets/Mesh/Mesh.asset.meta"), Is.EqualTo(KnownGuid));
			Assert.That(fileSystem.File.ReadAllText("/output/ExportedProject/Assets/Mesh/Mesh.asset"), Does.Contain(KnownGuid));
		}
	}

	[Test]
	public void GuidMapIsWritten()
	{
		VirtualFileSystem fileSystem = new();
		WriteMeta(fileSystem, "/reference/ExportedProject/Assets/Mesh/Mesh.asset.meta", KnownGuid);

		Export(fileSystem, settings => settings.ExportSettings.ReferenceProjectPath = "/reference");

		string guidMapPath = "/output/AuxiliaryFiles/guid-map.json";
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.Exists(guidMapPath), Is.True);
			Assert.That(fileSystem.File.ReadAllText(guidMapPath), Does.Contain("Assets/Mesh/Mesh.asset"));
			Assert.That(fileSystem.File.ReadAllText(guidMapPath), Does.Contain(KnownGuid));
		}
	}

	private static string ExportAndReadGuid(VirtualFileSystem fileSystem)
	{
		Export(fileSystem);
		return ReadGuid(fileSystem, MetaPath);
	}

	private static string ReadGuid(FileSystem fileSystem, string metaPath)
	{
		string metaText = fileSystem.File.ReadAllText(metaPath);
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

		Export(fileSystem, collection, configure);
	}

	private static void ExportNamed(VirtualFileSystem fileSystem, Action<FullConfiguration>? configure, params string[] meshNames)
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		foreach (string name in meshNames)
		{
			IMesh mesh = collection.CreateMesh();
			mesh.Name = name;
		}

		Export(fileSystem, collection, configure);
	}

	private static void Export(VirtualFileSystem fileSystem, ProcessedAssetCollection collection, Action<FullConfiguration>? configure)
	{
		FullConfiguration settings = new();
		configure?.Invoke(settings);
		new ExportHandler(settings).Export(CreateGameData(collection), "/output", fileSystem);
	}

	private static void WriteMeta(FileSystem fileSystem, string path, string guid)
	{
		WriteFile(fileSystem, path, $"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n");
	}

	private static void WriteFile(FileSystem fileSystem, string path, string content)
	{
		string directory = fileSystem.Path.GetDirectoryName(path)!;
		fileSystem.Directory.Create(directory);
		fileSystem.File.WriteAllText(path, content);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
