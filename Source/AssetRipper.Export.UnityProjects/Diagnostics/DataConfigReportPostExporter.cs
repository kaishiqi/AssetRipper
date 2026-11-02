using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Writes a report comparing DataConfig <c>assetReference.m_AssetGUID</c> values with the
/// exported .meta guids of the assets their <c>fileKey</c> points at.
/// </summary>
public sealed class DataConfigReportPostExporter : IPostExporter
{
	public const string FileName = "dataconfig-guid-report.json";

	private static readonly Regex EntryRegex = new(
		@"fileKey:[ \t]*(?<key>[^\r\n]+?)[ \t]*\r?\n[ \t]*assetReference:[ \t]*\r?\n[ \t]*m_AssetGUID:[ \t]*(?<guid>[0-9a-fA-F]*)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex GuidRegex = new(
		"^guid:[ \t]*([0-9a-fA-F]{32})",
		RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		string projectRootPath = settings.ProjectRootPath;
		DataConfigReport report = new() { ExportVersion = settings.Version.ToString() };

		string configDirectory = fileSystem.Path.Join(settings.AssetsPath, "DataConfig");
		if (fileSystem.Directory.Exists(configDirectory))
		{
			Dictionary<string, string> guidByPath = BuildMetaGuidMap(fileSystem, settings.AssetsPath);
			foreach (string configPath in fileSystem.Directory.EnumerateFiles(configDirectory, "*.asset", SearchOption.TopDirectoryOnly))
			{
				string text;
				try
				{
					text = fileSystem.File.ReadAllText(configPath);
				}
				catch (Exception ex)
				{
					Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read '{configPath}' while checking DataConfig guids: {ex.Message}");
					continue;
				}

				string relativeConfigPath = GetRelativePath(fileSystem, projectRootPath, configPath);
				foreach (Match match in EntryRegex.Matches(text))
				{
					string fileKey = NormalizeKey(match.Groups["key"].Value);
					if (fileKey.Length == 0)
					{
						continue;
					}

					string configuredGuid = match.Groups["guid"].Value.ToLowerInvariant();
					report.TotalCount++;

					if (guidByPath.TryGetValue(fileKey, out string? actualGuid))
					{
						if (string.Equals(configuredGuid, actualGuid, StringComparison.Ordinal))
						{
							report.MatchCount++;
						}
						else
						{
							report.MismatchCount++;
							report.Problems.Add(new DataConfigEntry
							{
								ConfigPath = relativeConfigPath,
								FileKey = fileKey,
								ConfiguredGuid = configuredGuid,
								ActualGuid = actualGuid,
								Kind = "guid_mismatch",
							});
						}
					}
					else
					{
						report.MissingAssetCount++;
						report.Problems.Add(new DataConfigEntry
						{
							ConfigPath = relativeConfigPath,
							FileKey = fileKey,
							ConfiguredGuid = configuredGuid,
							Kind = "asset_missing",
						});
					}
				}
			}
		}

		report.Problems.Sort((a, b) =>
		{
			int result = string.CompareOrdinal(a.ConfigPath, b.ConfigPath);
			return result != 0 ? result : string.CompareOrdinal(a.FileKey, b.FileKey);
		});

		Write(settings, fileSystem, report);
		Log(report);
	}

	private static Dictionary<string, string> BuildMetaGuidMap(FileSystem fileSystem, string assetsPath)
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
			if (!match.Success)
			{
				continue;
			}

			string relativePath = fileSystem.Path.GetRelativePath(assetsPath, metaPath[..^".meta".Length]).Replace('\\', '/');
			map[relativePath] = match.Groups[1].Value.ToLowerInvariant();
		}
		return map;
	}

	private static string NormalizeKey(string key)
	{
		return key.Trim().Replace('\\', '/');
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, DataConfigReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, DataConfigReportSerializerContext.Default.DataConfigReport);
		stream.Flush();
	}

	private static void Log(DataConfigReport report)
	{
		if (report.TotalCount == 0)
		{
			return;
		}

		Logger.Info(LogCategory.Export,
			$"DataConfig: {report.TotalCount} entries, {report.MatchCount} match, {report.MismatchCount} guid mismatches, {report.MissingAssetCount} missing assets. See '{FileName}'.");
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
