using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// Writes a map from asset paths (relative to the Unity project root) to their guids.
/// </summary>
/// <remarks>
/// The map can be used to compare exports or as a reference for a subsequent export.
/// </remarks>
public sealed class GuidMapPostExporter : IPostExporter
{
	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		string projectRootPath = settings.ProjectRootPath;
		if (!fileSystem.Directory.Exists(projectRootPath))
		{
			return;
		}

		SortedDictionary<string, string> guidMap = new(StringComparer.Ordinal);
		foreach (string metaPath in fileSystem.Directory.EnumerateFiles(projectRootPath, "*.meta", SearchOption.AllDirectories))
		{
			string relativePath = fileSystem.Path.GetRelativePath(projectRootPath, metaPath);
			relativePath = relativePath[..^".meta".Length];

			string metaText;
			try
			{
				metaText = fileSystem.File.ReadAllText(metaPath);
			}
			catch (Exception ex)
			{
				Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read meta file '{metaPath}': {ex.Message}");
				continue;
			}

			if (ReferenceGuidSource.TryParseGuid(metaText, out UnityGuid guid))
			{
				guidMap[relativePath] = guid.ToString();
			}
		}

		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, "guid-map.json"));
		JsonSerializer.Serialize(stream, guidMap, GuidMapSerializerContext.Default.SortedDictionaryStringString);
		stream.Flush();
	}
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SortedDictionary<string, string>))]
internal sealed partial class GuidMapSerializerContext : JsonSerializerContext
{
}
