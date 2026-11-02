using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.NativeEnums.Fmod;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class AudioReportTests
{
	private const string ReportPath = "/output/AuxiliaryFiles/audio-report.json";

	[Test]
	public void DecodableAudioIsReported()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(3, 5));
		IAudioClip clip = collection.CreateAudioClip();
		clip.Name = "TestClip";
		clip.Type = (int)FmodSoundType.Wav;
		clip.AudioData = new byte[] { 1, 2, 3, 4 };

		Export(fileSystem, collection);

		string report = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(report, Does.Contain("\"Name\": \"TestClip\""));
			Assert.That(report, Does.Contain("\"Container\": \"wav\""));
			Assert.That(report, Does.Contain("\"CanDecode\": true"));
			Assert.That(report, Does.Contain("\"ExportedExtension\": \"wav\""));
			Assert.That(report, Does.Contain("\"TotalCount\": 1"));
		}
	}

	[Test]
	public void MissingAudioDataFallsBackToYamlAndIsReported()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(3, 5));
		IAudioClip clip = collection.CreateAudioClip();
		clip.Name = "EmptyClip";

		Export(fileSystem, collection);

		string report = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(report, Does.Contain("\"Name\": \"EmptyClip\""));
			Assert.That(report, Does.Contain("\"CanDecode\": false"));
			Assert.That(report, Does.Contain("\"ExportedExtension\": \"asset\""));
			Assert.That(report, Does.Contain("could not be found"));
		}
	}

	private static void Export(VirtualFileSystem fileSystem, ProcessedAssetCollection collection)
	{
		new ExportHandler(new FullConfiguration()).Export(CreateGameData(collection), "/output", fileSystem);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
