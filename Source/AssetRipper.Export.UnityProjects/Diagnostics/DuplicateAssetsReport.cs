using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Reports <c>name_N</c> copies that sit next to a <c>name</c> asset.
/// </summary>
/// <remarks>
/// AssetRipper appends <c>_0</c>, <c>_1</c>, ... when two assets want the same file name.
/// These copies are often empty shells or duplicated assets, so they are reported for the
/// existing deduplication tooling instead of being deleted during export.
/// </remarks>
public static class DuplicateAssetsReport
{
	public const string FileName = "duplicate-assets.json";

	private static readonly Regex NumberedCopyRegex = new("^(?<base>.+)_(?<index>[0-9]+)(?<extension>\\.[^.]+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly HashSet<string> LineCountExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".anim", ".asset", ".controller", ".mat", ".prefab", ".shader", ".unity",
	};

	public static void Write(FullConfiguration settings, FileSystem fileSystem)
	{
		string projectRootPath = settings.ProjectRootPath;
		DuplicateAssetsReportModel report = new() { ExportVersion = settings.Version.ToString() };

		string assetsPath = settings.AssetsPath;
		if (fileSystem.Directory.Exists(assetsPath))
		{
			foreach (string filePath in fileSystem.Directory.EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories))
			{
				string fileName = fileSystem.Path.GetFileName(filePath);
				if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				Match match = NumberedCopyRegex.Match(fileName);
				if (!match.Success)
				{
					continue;
				}

				string directory = fileSystem.Path.GetDirectoryName(filePath) ?? string.Empty;
				string baseName = match.Groups["base"].Value + match.Groups["extension"].Value;
				string basePath = fileSystem.Path.Join(directory, baseName);
				if (!fileSystem.File.Exists(basePath))
				{
					continue;
				}

				report.Pairs.Add(new DuplicateAssetPair
				{
					BasePath = GetRelativePath(fileSystem, projectRootPath, basePath),
					DuplicatePath = GetRelativePath(fileSystem, projectRootPath, filePath),
					BaseSize = GetFileSize(fileSystem, basePath),
					DuplicateSize = GetFileSize(fileSystem, filePath),
					DuplicateLineCount = TryCountLines(fileSystem, filePath),
				});
			}
		}

		report.TotalPairCount = report.Pairs.Count;
		report.Pairs.Sort((a, b) => string.CompareOrdinal(a.DuplicatePath, b.DuplicatePath));

		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, DuplicateAssetsSerializerContext.Default.DuplicateAssetsReportModel);
		stream.Flush();

		if (report.TotalPairCount == 0)
		{
			Logger.Info(LogCategory.Export, "No duplicate asset copies were found.");
		}
		else
		{
			Logger.Warning(LogCategory.Export, $"{report.TotalPairCount} duplicate asset copies were found. See '{FileName}'.");
		}
	}

	private static long GetFileSize(FileSystem fileSystem, string path)
	{
		using Stream stream = fileSystem.File.OpenRead(path);
		return stream.Length;
	}

	private static int? TryCountLines(FileSystem fileSystem, string path)
	{
		if (!LineCountExtensions.Contains(fileSystem.Path.GetExtension(path)))
		{
			return null;
		}

		try
		{
			string text = fileSystem.File.ReadAllText(path);
			int count = 0;
			foreach (char c in text)
			{
				if (c == '\n')
				{
					count++;
				}
			}
			return count;
		}
		catch
		{
			return null;
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

public sealed class DuplicateAssetsReportModel
{
	public string ExportVersion { get; set; } = string.Empty;
	public int TotalPairCount { get; set; }
	public List<DuplicateAssetPair> Pairs { get; set; } = new();
}

public sealed class DuplicateAssetPair
{
	public string BasePath { get; set; } = string.Empty;
	public string DuplicatePath { get; set; } = string.Empty;
	public long BaseSize { get; set; }
	public long DuplicateSize { get; set; }

	/// <summary>
	/// Line count for text based assets; <see langword="null"/> for binary assets.
	/// </summary>
	public int? DuplicateLineCount { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DuplicateAssetsReportModel))]
internal sealed partial class DuplicateAssetsSerializerContext : JsonSerializerContext
{
}
