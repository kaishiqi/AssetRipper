using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Rewrites plain text references to textures that were renamed during export.
/// </summary>
/// <remarks>
/// AssetRipper appends <c>_0</c>, <c>_1</c>, ... when two assets want the same file name.
/// Spine <c>.atlas</c> files are pure text and keep referring to the old name, which makes
/// the Spine runtime fail with <c>Material with texture name ... not found</c>.
/// </remarks>
public static class TextReferenceRepairer
{
	public const string FileName = "renamed-textures.json";

	private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".bmp", ".jpeg", ".jpg", ".ktx", ".ktx2", ".png", ".psd", ".tga", ".webp",
	};

	public static void Repair(FullConfiguration settings, FileSystem fileSystem, IReadOnlyList<AssetRenameInfo> renames)
	{
		string projectRootPath = settings.ProjectRootPath;
		RenamedTexturesReport report = new()
		{
			ExportVersion = settings.Version.ToString(),
			TotalRenameCount = renames.Count,
		};

		foreach (AssetRenameInfo rename in renames)
		{
			report.Renames.Add(new TextRenameEntry
			{
				AssetName = rename.AssetName,
				ClassID = rename.ClassID,
				OriginalFileName = rename.OriginalFileName,
				ExportedFileName = rename.ExportedFileName,
				Directory = GetRelativePath(fileSystem, projectRootPath, rename.Directory),
				SourceFile = rename.SourceFile,
			});
		}

		string assetsPath = settings.AssetsPath;
		if (renames.Count > 0 && settings.ExportSettings.RepairTextReferences && fileSystem.Directory.Exists(assetsPath))
		{
			Dictionary<string, List<AssetRenameInfo>> byStem = BuildLookup(fileSystem, renames);
			HashSet<string> exportedFileNames = EnumerateExportedFileNames(fileSystem, assetsPath);
			foreach (string textPath in EnumerateAtlasFiles(fileSystem, assetsPath))
			{
				RepairFile(fileSystem, projectRootPath, textPath, byStem, exportedFileNames, report);
			}
		}

		report.UnresolvedCount = report.Unresolved.Count;
		Write(settings, fileSystem, report);
		LogReport(report);
	}

	private static Dictionary<string, List<AssetRenameInfo>> BuildLookup(FileSystem fileSystem, IReadOnlyList<AssetRenameInfo> renames)
	{
		Dictionary<string, List<AssetRenameInfo>> byStem = new(StringComparer.OrdinalIgnoreCase);
		foreach (AssetRenameInfo rename in renames)
		{
			string stem = fileSystem.Path.GetFileNameWithoutExtension(rename.OriginalFileName);
			if (stem.Length == 0)
			{
				continue;
			}

			if (!byStem.TryGetValue(stem, out List<AssetRenameInfo>? list))
			{
				list = new List<AssetRenameInfo>();
				byStem.Add(stem, list);
			}
			list.Add(rename);
		}
		return byStem;
	}

	private static void RepairFile(FileSystem fileSystem, string projectRootPath, string textPath, Dictionary<string, List<AssetRenameInfo>> byStem, HashSet<string> exportedFileNames, RenamedTexturesReport report)
	{
		string text;
		try
		{
			text = fileSystem.File.ReadAllText(textPath);
		}
		catch (Exception ex)
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read '{textPath}' while repairing texture references: {ex.Message}");
			return;
		}

		string directory = fileSystem.Path.GetDirectoryName(textPath) ?? string.Empty;
		string relativePath = GetRelativePath(fileSystem, projectRootPath, textPath);
		string[] lines = text.Split('\n');
		bool changed = false;

		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];
			string pageName = line.Trim();
			if (pageName.Length == 0 || pageName.Contains(':') || !IsPageHeader(lines, i))
			{
				continue;
			}

			if (fileSystem.File.Exists(fileSystem.Path.Join(directory, pageName)))
			{
				continue;
			}

			// The texture may live in a type folder such as Assets/Texture2D.
			// Spine matches materials by texture name, not by directory.
			if (ExistsAnywhere(exportedFileNames, pageName))
			{
				continue;
			}

			string? newName = ResolvePageName(fileSystem, directory, pageName, byStem);
			if (newName is null)
			{
				report.Unresolved.Add(new UnresolvedTextReference { Path = relativePath, Name = pageName });
				continue;
			}

			int index = line.IndexOf(pageName, StringComparison.Ordinal);
			lines[i] = string.Concat(line.AsSpan(0, index), newName, line.AsSpan(index + pageName.Length));
			changed = true;
			report.Repairs.Add(new TextReferenceRepair { Path = relativePath, OldName = pageName, NewName = newName });
		}

		if (changed)
		{
			fileSystem.File.WriteAllText(textPath, string.Join('\n', lines));
			report.RepairedFileCount++;
		}
	}

	/// <summary>
	/// Spine atlas page headers are followed by a <c>size:</c> line.
	/// </summary>
	private static bool IsPageHeader(string[] lines, int index)
	{
		for (int i = index + 1; i < lines.Length; i++)
		{
			string next = lines[i].Trim();
			if (next.Length == 0)
			{
				continue;
			}
			return next.StartsWith("size:", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static string? ResolvePageName(FileSystem fileSystem, string directory, string pageName, Dictionary<string, List<AssetRenameInfo>> byStem)
	{
		string extension = fileSystem.Path.GetExtension(pageName);
		string stem = fileSystem.Path.GetFileNameWithoutExtension(pageName);
		if (stem.Length == 0)
		{
			stem = pageName;
		}

		if (byStem.TryGetValue(stem, out List<AssetRenameInfo>? candidates))
		{
			AssetRenameInfo? match = candidates.FirstOrDefault(c => string.Equals(c.Directory, directory, StringComparison.OrdinalIgnoreCase))
				?? (candidates.Count == 1 ? candidates[0] : null);
			if (match is not null)
			{
				return extension.Length == 0
					? fileSystem.Path.GetFileNameWithoutExtension(match.ExportedFileName)
					: match.ExportedFileName;
			}
		}

		// Fall back to a unique "_<number>" variant in the same directory.
		List<string> variants = new();
		foreach (string file in fileSystem.Directory.GetFiles(directory))
		{
			string fileName = fileSystem.Path.GetFileName(file);
			string fileExtension = fileSystem.Path.GetExtension(fileName);
			if (extension.Length != 0)
			{
				if (!string.Equals(fileExtension, extension, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
			}
			else if (!ImageExtensions.Contains(fileExtension))
			{
				continue;
			}

			if (IsNumberedSuffixOf(fileSystem.Path.GetFileNameWithoutExtension(fileName), stem))
			{
				variants.Add(fileName);
			}
		}

		if (variants.Count == 1)
		{
			return extension.Length == 0 ? fileSystem.Path.GetFileNameWithoutExtension(variants[0]) : variants[0];
		}
		return null;
	}

	private static bool IsNumberedSuffixOf(string candidate, string stem)
	{
		if (candidate.Length <= stem.Length + 1)
		{
			return false;
		}
		if (!candidate.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (candidate[stem.Length] != '_')
		{
			return false;
		}
		for (int i = stem.Length + 1; i < candidate.Length; i++)
		{
			if (candidate[i] is < '0' or > '9')
			{
				return false;
			}
		}
		return true;
	}

	private static IEnumerable<string> EnumerateAtlasFiles(FileSystem fileSystem, string assetsPath)
	{
		foreach (string filePath in fileSystem.Directory.EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories))
		{
			string fileName = fileSystem.Path.GetFileName(filePath);
			if (fileName.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)
				|| fileName.EndsWith(".atlas.txt", StringComparison.OrdinalIgnoreCase))
			{
				yield return filePath;
			}
		}
	}

	private static HashSet<string> EnumerateExportedFileNames(FileSystem fileSystem, string assetsPath)
	{
		HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
		foreach (string filePath in fileSystem.Directory.EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories))
		{
			names.Add(fileSystem.Path.GetFileName(filePath));
		}
		return names;
	}

	private static bool ExistsAnywhere(HashSet<string> exportedFileNames, string pageName)
	{
		if (exportedFileNames.Contains(pageName))
		{
			return true;
		}

		// Some atlases omit the image extension in the page name.
		if (Path.GetExtension(pageName).Length == 0)
		{
			foreach (string extension in ImageExtensions)
			{
				if (exportedFileNames.Contains(pageName + extension))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, RenamedTexturesReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, RenamedTexturesSerializerContext.Default.RenamedTexturesReport);
		stream.Flush();
	}

	private static void LogReport(RenamedTexturesReport report)
	{
		if (report.TotalRenameCount == 0)
		{
			Logger.Info(LogCategory.Export, "No texture renames were needed.");
		}
		else
		{
			Logger.Info(LogCategory.Export, $"{report.TotalRenameCount} textures were renamed. Repaired {report.Repairs.Count} text references in {report.RepairedFileCount} files. See '{FileName}'.");
		}

		if (report.UnresolvedCount > 0)
		{
			Logger.Warning(LogCategory.Export, $"{report.UnresolvedCount} text references could not be matched to an exported texture. See '{FileName}'.");
		}
	}

	private static string GetRelativePath(FileSystem fileSystem, string projectRootPath, string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return string.Empty;
		}

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
