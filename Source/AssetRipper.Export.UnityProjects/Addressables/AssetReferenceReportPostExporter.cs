using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects.Addressables;

/// <summary>
/// Classifies the <c>m_AssetGUID</c> values of Addressables <c>AssetReference</c> fields.
/// </summary>
/// <remarks>
/// In some builds <c>m_AssetGUID</c> holds an Addressables catalog key (an md5 hash) instead of
/// a Unity guid, so <c>AssetDatabase.GUIDToAssetPath</c> cannot resolve it. Telling the two
/// apart prevents post-processing from "fixing" valid catalog keys.
/// </remarks>
public sealed class AssetReferenceReportPostExporter : IPostExporter
{
	public const string FileName = "asset-references.json";

	private static readonly Regex AssetGuidRegex = new(
		@"^[ \t]*m_AssetGUID:[ \t]*(?<guid>[0-9a-fA-F]{32})[ \t]*\r?$",
		RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

	private static readonly Regex HexRegex = new(
		"[0-9a-fA-F]{32}",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly string[] ScanExtensions = [".asset", ".prefab"];

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		string projectRootPath = settings.ProjectRootPath;
		AssetReferenceReport report = new() { ExportVersion = settings.Version.ToString() };

		string assetsPath = settings.AssetsPath;
		if (fileSystem.Directory.Exists(assetsPath))
		{
			HashSet<string> unityGuids = BuildUnityGuidSet(fileSystem, assetsPath);
			HashSet<string>? catalogKeys = LoadCatalogKeys(settings, fileSystem);
			report.CatalogFound = catalogKeys is not null;

			Dictionary<(string, string), AssetReferenceEntry> entries = new();
			foreach (string pattern in ScanExtensions)
			{
				foreach (string filePath in fileSystem.Directory.EnumerateFiles(assetsPath, $"*{pattern}", SearchOption.AllDirectories))
				{
					string text;
					try
					{
						text = fileSystem.File.ReadAllText(filePath);
					}
					catch (Exception ex)
					{
						Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read '{filePath}' while scanning asset references: {ex.Message}");
						continue;
					}

					if (!text.Contains("m_AssetGUID", StringComparison.Ordinal))
					{
						continue;
					}

					string relativePath = GetRelativePath(fileSystem, projectRootPath, filePath);
					foreach (Match match in AssetGuidRegex.Matches(text))
					{
						string guid = match.Groups["guid"].Value.ToLowerInvariant();
						string kind = unityGuids.Contains(guid)
							? "unity_guid"
							: catalogKeys?.Contains(guid) == true ? "catalog_key" : "missing";

						if (entries.TryGetValue((relativePath, guid), out AssetReferenceEntry? existing))
						{
							existing.Occurrences++;
						}
						else
						{
							entries.Add((relativePath, guid), new AssetReferenceEntry
							{
								Path = relativePath,
								Guid = guid,
								Kind = kind,
							});
						}
					}
				}
			}

			report.References = entries.Values
				.OrderBy(e => e.Path, StringComparer.Ordinal)
				.ThenBy(e => e.Guid, StringComparer.Ordinal)
				.ToList();
			report.TotalCount = report.References.Count;
			report.UnityGuidCount = report.References.Count(e => e.Kind == "unity_guid");
			report.CatalogKeyCount = report.References.Count(e => e.Kind == "catalog_key");
			report.MissingCount = report.References.Count(e => e.Kind == "missing");
		}

		Write(settings, fileSystem, report);
		Log(report);
	}

	private static HashSet<string> BuildUnityGuidSet(FileSystem fileSystem, string assetsPath)
	{
		HashSet<string> guids = new(StringComparer.Ordinal);
		foreach (string metaPath in fileSystem.Directory.EnumerateFiles(assetsPath, "*.meta", SearchOption.AllDirectories))
		{
			string text;
			try
			{
				text = fileSystem.File.ReadAllText(metaPath);
			}
			catch
			{
				continue;
			}

			Match match = HexRegex.Match(text);
			if (match.Success)
			{
				guids.Add(match.Value.ToLowerInvariant());
			}
		}
		return guids;
	}

	private static HashSet<string>? LoadCatalogKeys(FullConfiguration settings, FileSystem fileSystem)
	{
		string catalogDirectory = fileSystem.Path.Join(settings.AssetsPath, "StreamingAssets", "aa");
		if (!fileSystem.Directory.Exists(catalogDirectory))
		{
			return null;
		}

		string catalogPath = fileSystem.Path.Join(catalogDirectory, "catalog.json");
		if (!fileSystem.File.Exists(catalogPath))
		{
			catalogPath = fileSystem.Directory.GetFiles(catalogDirectory, "*.json")
				.FirstOrDefault(p => fileSystem.Path.GetFileName(p).StartsWith("catalog", StringComparison.OrdinalIgnoreCase))
				?? string.Empty;
			if (catalogPath.Length == 0)
			{
				return null;
			}
		}

		string text;
		try
		{
			text = fileSystem.File.ReadAllText(catalogPath);
		}
		catch
		{
			return null;
		}

		HashSet<string> keys = new(StringComparer.Ordinal);
		AddHexStrings(text, keys);

		try
		{
			using JsonDocument document = JsonDocument.Parse(text);
			if (document.RootElement.TryGetProperty("m_KeyDataString", out JsonElement keyData)
				&& keyData.ValueKind == JsonValueKind.String)
			{
				string? base64 = keyData.GetString();
				if (!string.IsNullOrEmpty(base64))
				{
					try
					{
						byte[] bytes = Convert.FromBase64String(base64);
						AddHexStrings(Encoding.Latin1.GetString(bytes), keys);
					}
					catch (FormatException)
					{
					}
				}
			}

			if (document.RootElement.TryGetProperty("m_Keys", out JsonElement keyArray)
				&& keyArray.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement element in keyArray.EnumerateArray())
				{
					if (element.ValueKind == JsonValueKind.String)
					{
						AddHexStrings(element.GetString() ?? string.Empty, keys);
					}
				}
			}
		}
		catch (JsonException)
		{
		}

		return keys;
	}

	private static void AddHexStrings(string text, HashSet<string> keys)
	{
		foreach (Match match in HexRegex.Matches(text))
		{
			keys.Add(match.Value.ToLowerInvariant());
		}
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, AssetReferenceReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, AssetReferenceReportSerializerContext.Default.AssetReferenceReport);
		stream.Flush();
	}

	private static void Log(AssetReferenceReport report)
	{
		if (report.TotalCount == 0)
		{
			return;
		}

		Logger.Info(LogCategory.Export,
			$"Found {report.TotalCount} AssetReference guids: {report.UnityGuidCount} unity, {report.CatalogKeyCount} catalog keys, {report.MissingCount} missing. See '{FileName}'.");

		if (!report.CatalogFound && report.CatalogKeyCount + report.MissingCount > 0)
		{
			Logger.Warning(LogCategory.Export, "No Addressables catalog was found; catalog keys could not be identified.");
		}
	}

	private static string GetRelativePath(FileSystem fileSystem, string projectRootPath, string path)
	{
		string relativePath;
		try
		{
			relativePath = fileSystem.Path.GetRelativePath(projectRootPath, path);
		}
		catch (ArgumentException)
		{
			relativePath = path;
		}
		return relativePath.Replace('\\', '/');
	}
}
