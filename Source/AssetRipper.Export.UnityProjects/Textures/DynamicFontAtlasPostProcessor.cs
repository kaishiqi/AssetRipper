using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects.Textures;

/// <summary>
/// Applies the import settings that TMP dynamic fonts require to their atlas textures.
/// </summary>
/// <remarks>
/// TMP adds glyphs at runtime with <c>FontEngine.TryAddGlyphToTexture</c>, which only works on
/// readable, uncompressed, single channel textures. Unity 2022 shows this as
/// <c>textureType: 10</c> (Single Channel) plus <c>textureCompression: 0</c>; the legacy
/// <c>textureFormat</c> field is ignored.
/// </remarks>
public static class DynamicFontAtlasPostProcessor
{
	public const string FileName = "font-atlas-report.json";

	private const string DynamicAtlasModeLine = "m_AtlasPopulationMode: 1";

	private static readonly Regex GuidRegex = new("[0-9a-fA-F]{32}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly string[] FontAssetPatterns = [".asset", ".prefab"];

	public static void Process(FullConfiguration settings, FileSystem fileSystem)
	{
		string projectRootPath = settings.ProjectRootPath;
		FontAtlasReport report = new() { ExportVersion = settings.Version.ToString() };

		string assetsPath = settings.AssetsPath;
		if (fileSystem.Directory.Exists(assetsPath))
		{
			List<(string Path, List<string> Guids)> fontAssets = FindDynamicFontAssets(fileSystem, assetsPath);
			report.FontAssetCount = fontAssets.Count;

			if (fontAssets.Any(f => f.Guids.Count > 0))
			{
				Dictionary<string, string> metaByGuid = BuildMetaMap(fileSystem, assetsPath);
				foreach ((string fontPath, List<string> guids) in fontAssets)
				{
					FontAtlasEntry entry = new()
					{
						FontAssetPath = GetRelativePath(fileSystem, projectRootPath, fontPath),
					};

					foreach (string guid in guids)
					{
						FontAtlasTexture atlas = new() { Guid = guid };
						if (metaByGuid.TryGetValue(guid, out string? metaPath))
						{
							atlas.MetaPath = GetRelativePath(fileSystem, projectRootPath, metaPath);
							atlas.Modified = RewriteMeta(fileSystem, metaPath);
							if (atlas.Modified)
							{
								report.ModifiedMetaCount++;
							}
						}
						else
						{
							report.MissingAtlasCount++;
						}
						entry.AtlasTextures.Add(atlas);
						report.AtlasTextureCount++;
					}

					report.FontAssets.Add(entry);
				}
			}
		}

		Write(settings, fileSystem, report);
		Log(report);
	}

	private static List<(string Path, List<string> Guids)> FindDynamicFontAssets(FileSystem fileSystem, string assetsPath)
	{
		List<(string, List<string>)> results = new();
		foreach (string pattern in FontAssetPatterns)
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
					Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read '{filePath}' while looking for TMP font assets: {ex.Message}");
					continue;
				}

				if (text.Contains(DynamicAtlasModeLine, StringComparison.Ordinal))
				{
					results.Add((filePath, ExtractAtlasGuids(text)));
				}
			}
		}
		return results;
	}

	private static List<string> ExtractAtlasGuids(string text)
	{
		List<string> guids = new();
		HashSet<string> seen = new(StringComparer.Ordinal);
		string[] lines = text.Split('\n');

		for (int i = 0; i < lines.Length; i++)
		{
			if (!lines[i].TrimStart().StartsWith("m_AtlasTextures:", StringComparison.Ordinal))
			{
				continue;
			}

			int keyIndent = CountIndent(lines[i]);
			for (int j = i + 1; j < lines.Length; j++)
			{
				string trimmed = lines[j].Trim();
				if (trimmed.Length == 0)
				{
					continue;
				}

				bool isListItem = trimmed.StartsWith('-');
				if (!isListItem && !trimmed.Contains("guid:", StringComparison.Ordinal))
				{
					break;
				}
				if (!isListItem && CountIndent(lines[j]) <= keyIndent)
				{
					break;
				}

				Match match = GuidRegex.Match(trimmed);
				if (match.Success)
				{
					string guid = match.Value.ToLowerInvariant();
					if (!IsZeroGuid(guid) && seen.Add(guid))
					{
						guids.Add(guid);
					}
				}
			}
		}
		return guids;
	}

	private static Dictionary<string, string> BuildMetaMap(FileSystem fileSystem, string assetsPath)
	{
		Dictionary<string, string> map = new(StringComparer.Ordinal);
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

			Match match = GuidRegex.Match(text);
			if (match.Success)
			{
				map.TryAdd(match.Value.ToLowerInvariant(), metaPath);
			}
		}
		return map;
	}

	private static bool RewriteMeta(FileSystem fileSystem, string metaPath)
	{
		string text = fileSystem.File.ReadAllText(metaPath);
		string updated = text;
		updated = SetValue(updated, "textureType", "10");
		updated = SetValue(updated, "textureCompression", "0");
		updated = SetValue(updated, "isReadable", "1");
		updated = SetValue(updated, "sRGBTexture", "0");
		updated = SetValue(updated, "alphaIsTransparency", "1");

		if (string.Equals(text, updated, StringComparison.Ordinal))
		{
			return false;
		}

		fileSystem.File.WriteAllText(metaPath, updated);
		return true;
	}

	private static string SetValue(string text, string key, string value)
	{
		return Regex.Replace(
			text,
			$@"^(?<prefix>[ \t]*{Regex.Escape(key)}:[ \t]*)[^\r\n]*(?<suffix>\r?)$",
			match => match.Groups["prefix"].Value + value + match.Groups["suffix"].Value,
			RegexOptions.Multiline | RegexOptions.CultureInvariant);
	}

	private static int CountIndent(string line)
	{
		int count = 0;
		while (count < line.Length && line[count] == ' ')
		{
			count++;
		}
		return count;
	}

	private static bool IsZeroGuid(string guid)
	{
		foreach (char c in guid)
		{
			if (c != '0')
			{
				return false;
			}
		}
		return true;
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, FontAtlasReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, FontAtlasReportSerializerContext.Default.FontAtlasReport);
		stream.Flush();
	}

	private static void Log(FontAtlasReport report)
	{
		if (report.FontAssetCount == 0)
		{
			return;
		}

		Logger.Info(LogCategory.Export, $"{report.FontAssetCount} TMP dynamic font assets were found. Adjusted {report.ModifiedMetaCount} atlas .meta files. See '{FileName}'.");

		if (report.MissingAtlasCount > 0)
		{
			Logger.Warning(LogCategory.Export, $"{report.MissingAtlasCount} TMP font atlas textures were not exported. See '{FileName}'.");
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
