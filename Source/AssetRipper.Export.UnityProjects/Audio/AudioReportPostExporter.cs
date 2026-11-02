using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Audio;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Audio;

/// <summary>
/// Writes a report describing the format of every audio clip and what the current export mode produces.
/// </summary>
public sealed class AudioReportPostExporter : IPostExporter
{
	public const string FileName = "audio-report.json";

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		AudioExportFormat exportMode = settings.ExportSettings.AudioExportFormat;
		List<AudioReportEntry> clips = new();

		foreach (var collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (var asset in collection)
			{
				if (asset is not IAudioClip audioClip)
				{
					continue;
				}

				AudioClipDecoder.AudioClipProbe probe = AudioClipDecoder.Probe(audioClip);
				clips.Add(new AudioReportEntry
				{
					Name = audioClip.GetBestName(),
					PathID = audioClip.PathID,
					Collection = collection.Name,
					DeclaredCompression = GetDeclaredCompression(audioClip),
					Container = probe.Container,
					Codec = probe.Codec,
					CanDecode = probe.CanDecode,
					ExportedExtension = GetExportedExtension(audioClip, probe, exportMode),
					InlineDataLength = audioClip.Has_AudioData() ? audioClip.AudioData.Length : 0,
					StreamSource = audioClip.Resource?.Source.String,
					StreamSize = audioClip.Resource?.Size ?? 0,
					Message = probe.Message,
				});
			}
		}

		clips.Sort((a, b) =>
		{
			int result = string.CompareOrdinal(a.Name, b.Name);
			return result != 0 ? result : a.PathID.CompareTo(b.PathID);
		});

		AudioReport report = new()
		{
			ExportMode = exportMode.ToString(),
			TotalCount = clips.Count,
			Clips = clips,
		};

		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, AudioReportSerializerContext.Default.AudioReport);
		stream.Flush();

		int undecodable = clips.Count(c => !c.CanDecode);
		Logger.Info(LogCategory.Export, $"Wrote an audio format report for {clips.Count} clips ({undecodable} could not be decoded).");
	}

	private static string? GetDeclaredCompression(IAudioClip audioClip)
	{
		if (audioClip.Has_Type())
		{
			return audioClip.GetSoundType().ToString();
		}
		if (audioClip.Has_CompressionFormat())
		{
			return audioClip.CompressionFormatE.ToString();
		}
		return null;
	}

	private static string? GetExportedExtension(IAudioClip audioClip, AudioClipDecoder.AudioClipProbe probe, AudioExportFormat exportMode)
	{
		switch (exportMode)
		{
			case AudioExportFormat.Yaml:
				return "asset";
			case AudioExportFormat.Native:
				if (audioClip.Has_Type())
				{
					return audioClip.GetSoundType().ToRawExtension();
				}
				return audioClip.Has_CompressionFormat() ? audioClip.CompressionFormatE.ToRawExtension() : null;
			case AudioExportFormat.Default:
				return probe.CanDecode ? probe.DecodedExtension : "asset";
			case AudioExportFormat.PreferWav:
				if (!probe.CanDecode)
				{
					return "asset";
				}
				return probe.DecodedExtension == "ogg" ? "wav" : probe.DecodedExtension;
			default:
				return null;
		}
	}
}
