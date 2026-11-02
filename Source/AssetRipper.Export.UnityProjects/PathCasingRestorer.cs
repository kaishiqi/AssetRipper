using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// Restores the original capitalization of exported directory names by matching them
/// case-insensitively against a reference project.
/// </summary>
/// <remarks>
/// Unity often lowercases paths while building a player. The original case is not stored in
/// the bundle, so a previous export is the only reliable source. This runs before guid
/// reconciliation so that both use the canonical paths.
/// </remarks>
public static class PathCasingRestorer
{
	public static void Restore(FullConfiguration settings, FileSystem fileSystem)
	{
		string? referencePath = settings.ExportSettings.ReferenceProjectPath;
		if (string.IsNullOrWhiteSpace(referencePath))
		{
			return;
		}

		string? referenceRootPath = ReferenceGuidSource.NormalizeProjectRoot(referencePath, fileSystem);
		if (referenceRootPath is null)
		{
			return;
		}

		string projectRootPath = settings.ProjectRootPath;
		string assetsPath = settings.AssetsPath;
		if (!fileSystem.Directory.Exists(assetsPath))
		{
			return;
		}

		Dictionary<string, string> canonicalDirectories = CreateCanonicalDirectoryMap(referenceRootPath, fileSystem);
		if (canonicalDirectories.Count == 0)
		{
			return;
		}

		int renamedCount = RestoreDirectory(fileSystem, assetsPath, "Assets", canonicalDirectories);
		if (renamedCount > 0)
		{
			Logger.Info(LogCategory.Export, $"Restored the capitalization of {renamedCount} directories from the reference project.");
		}
		else
		{
			Logger.Info(LogCategory.Export, "Directory capitalization already matches the reference project.");
		}
	}

	private static Dictionary<string, string> CreateCanonicalDirectoryMap(string referenceRootPath, FileSystem fileSystem)
	{
		Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
		foreach (string directory in fileSystem.Directory.EnumerateDirectories(referenceRootPath, "*", SearchOption.AllDirectories))
		{
			string relativePath = fileSystem.Path.GetRelativePath(referenceRootPath, directory).Replace('\\', '/');
			if (relativePath.StartsWith("..", StringComparison.Ordinal))
			{
				continue;
			}
			if (!relativePath.StartsWith("Assets/", StringComparison.Ordinal))
			{
				continue;
			}
			map[relativePath] = relativePath;
		}
		return map;
	}

	private static int RestoreDirectory(FileSystem fileSystem, string directoryPath, string relativePath, Dictionary<string, string> canonicalDirectories)
	{
		int renamedCount = 0;
		foreach (string childPath in fileSystem.Directory.EnumerateDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly).ToArray())
		{
			string name = fileSystem.Path.GetFileName(childPath);
			string childRelativePath = $"{relativePath}/{name}";
			string currentPath = childPath;
			string currentRelativePath = childRelativePath;

			if (canonicalDirectories.TryGetValue(childRelativePath, out string? canonicalRelativePath))
			{
				string canonicalName = canonicalRelativePath[(canonicalRelativePath.LastIndexOf('/') + 1)..];
				if (!string.Equals(name, canonicalName, StringComparison.Ordinal))
				{
					string targetPath = fileSystem.Path.Join(directoryPath, canonicalName);
					bool isCaseOnlyChange = string.Equals(name, canonicalName, StringComparison.OrdinalIgnoreCase);

					if (!isCaseOnlyChange && fileSystem.Directory.Exists(targetPath))
					{
						Logger.Log(LogType.Warning, LogCategory.Export,
							$"Cannot restore the capitalization of '{currentPath}' because '{targetPath}' already exists.");
					}
					else
					{
						fileSystem.Directory.Move(currentPath, targetPath);
						currentPath = targetPath;
						currentRelativePath = canonicalRelativePath;
						renamedCount++;
					}
				}
			}

			renamedCount += RestoreDirectory(fileSystem, currentPath, currentRelativePath, canonicalDirectories);
		}
		return renamedCount;
	}
}
