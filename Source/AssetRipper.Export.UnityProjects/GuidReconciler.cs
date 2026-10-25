using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// Replaces the guids of an exported Unity project with the guids from a previous export.
/// </summary>
/// <remarks>
/// Matching is done on the actual output paths after the export has finished. This is more
/// reliable than predicting output file names up front, because unique file name suffixes
/// depend on the export order and on files written by other collections.
/// </remarks>
public static class GuidReconciler
{
	private static readonly Regex GuidRegex = new("[0-9a-fA-F]{32}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".anim", ".asmdef", ".asset", ".blendtree", ".brush", ".cfg", ".conf", ".controller", ".cs",
		".css", ".csv", ".cubemap", ".flare", ".fontsettings", ".giparams", ".guiskin", ".htm", ".html",
		".ini", ".js", ".json", ".lighting", ".log", ".lua", ".mask", ".mat", ".md", ".meta", ".mixer",
		".overrideController", ".physicMaterial", ".playable", ".prefab", ".preset", ".proto", ".py",
		".renderTexture", ".shader", ".shadervariants", ".sh", ".signal", ".spriteatlas", ".state",
		".statemachine", ".terrainlayer", ".transition", ".tsv", ".txt", ".unity", ".uss", ".uxml",
		".xml", ".yaml", ".yml",
	};

	public static void Reconcile(FullConfiguration settings, FileSystem fileSystem)
	{
		ReferenceGuidSource? reference = ReferenceGuidSource.Create(settings.ExportSettings.ReferenceProjectPath, fileSystem);
		if (reference is null)
		{
			return;
		}

		string projectRootPath = settings.ProjectRootPath;
		if (!fileSystem.Directory.Exists(projectRootPath))
		{
			return;
		}

		Logger.Info(LogCategory.Export, $"Reconciling guids with reference project '{settings.ExportSettings.ReferenceProjectPath}'");

		Dictionary<string, string> replacements = CreateReplacements(projectRootPath, fileSystem, reference);
		if (replacements.Count == 0)
		{
			return;
		}

		int rewrittenFileCount = 0;
		foreach (string filePath in fileSystem.Directory.EnumerateFiles(projectRootPath, "*", SearchOption.AllDirectories))
		{
			if (!IsTextFile(filePath))
			{
				continue;
			}

			string text;
			try
			{
				text = fileSystem.File.ReadAllText(filePath);
			}
			catch (Exception ex)
			{
				Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read '{filePath}' while reconciling guids: {ex.Message}");
				continue;
			}

			string replacedText = GuidRegex.Replace(text, match =>
			{
				return replacements.TryGetValue(match.Value.ToLowerInvariant(), out string? replacement) ? replacement : match.Value;
			});

			if (!string.Equals(text, replacedText, StringComparison.Ordinal))
			{
				fileSystem.File.WriteAllText(filePath, replacedText);
				rewrittenFileCount++;
			}
		}

		Logger.Info(LogCategory.Export, $"Reconciled {replacements.Count} guids from the reference project and rewrote {rewrittenFileCount} files.");
	}

	private static Dictionary<string, string> CreateReplacements(string projectRootPath, FileSystem fileSystem, ReferenceGuidSource reference)
	{
		Dictionary<string, string> replacements = new(StringComparer.Ordinal);
		HashSet<string> usedReferenceGuids = new(StringComparer.Ordinal);

		foreach (string metaPath in fileSystem.Directory.EnumerateFiles(projectRootPath, "*.meta", SearchOption.AllDirectories))
		{
			string relativeMetaPath = fileSystem.Path.GetRelativePath(projectRootPath, metaPath).Replace('\\', '/');
			if (!relativeMetaPath.StartsWith("Assets/", StringComparison.Ordinal))
			{
				continue;
			}

			string relativeAssetPath = relativeMetaPath[..^".meta".Length].Replace('/', Path.DirectorySeparatorChar);
			if (!reference.TryGetGuid(relativeAssetPath, out UnityGuid referenceGuid))
			{
				continue;
			}

			string referenceGuidString = referenceGuid.ToString();
			if (!usedReferenceGuids.Add(referenceGuidString))
			{
				continue;
			}

			string metaText;
			try
			{
				metaText = fileSystem.File.ReadAllText(metaPath);
			}
			catch (Exception ex)
			{
				Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read '{metaPath}' while reconciling guids: {ex.Message}");
				continue;
			}

			if (!ReferenceGuidSource.TryParseGuid(metaText, out UnityGuid currentGuid))
			{
				continue;
			}

			string currentGuidString = currentGuid.ToString();
			if (!string.Equals(currentGuidString, referenceGuidString, StringComparison.Ordinal))
			{
				replacements[currentGuidString] = referenceGuidString;
			}
		}

		return replacements;
	}

	private static bool IsTextFile(string filePath)
	{
		return TextExtensions.Contains(Path.GetExtension(filePath));
	}
}
