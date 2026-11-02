using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// Reads the Unity guids of a previous export so that a new export can reuse them.
/// </summary>
/// <remarks>
/// Assets are matched by their path relative to the Unity project root. Only the .meta files
/// for paths that are actually exported are read.
/// </remarks>
internal sealed class ReferenceGuidSource
{
	private ReferenceGuidSource(FileSystem fileSystem, string projectRootPath)
	{
		this.fileSystem = fileSystem;
		this.projectRootPath = projectRootPath;
	}

	private readonly FileSystem fileSystem;
	private readonly string projectRootPath;
	private readonly Dictionary<string, UnityGuid?> cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Create a source for the supplied reference path, or <see langword="null"/> when no usable reference is configured.
	/// </summary>
	/// <param name="referencePath">The export root containing an ExportedProject folder, or a Unity project root containing an Assets folder.</param>
	/// <param name="fileSystem">The file system used for reading.</param>
	public static ReferenceGuidSource? Create(string? referencePath, FileSystem fileSystem)
	{
		string? projectRootPath = NormalizeProjectRoot(referencePath, fileSystem);
		return projectRootPath is null ? null : new ReferenceGuidSource(fileSystem, projectRootPath);
	}

	public static string? NormalizeProjectRoot(string? referencePath, FileSystem fileSystem)
	{
		if (string.IsNullOrWhiteSpace(referencePath))
		{
			return null;
		}

		string trimmedPath = referencePath.Trim();
		string exportedProjectPath = fileSystem.Path.Join(trimmedPath, "ExportedProject");
		if (fileSystem.Directory.Exists(fileSystem.Path.Join(exportedProjectPath, "Assets")))
		{
			return exportedProjectPath;
		}

		if (fileSystem.Directory.Exists(fileSystem.Path.Join(trimmedPath, "Assets")))
		{
			return trimmedPath;
		}

		Logger.Warning(LogCategory.Export, $"The reference project path '{trimmedPath}' does not contain an Assets folder. Guids will be newly generated.");
		return null;
	}

	/// <summary>
	/// Try to get the guid of the asset at <paramref name="relativePath"/> in the reference project.
	/// </summary>
	/// <param name="relativePath">The asset path relative to the Unity project root. Uses the platform's directory separator.</param>
	/// <param name="guid">The reference guid when found.</param>
	public bool TryGetGuid(string relativePath, out UnityGuid guid)
	{
		if (cache.TryGetValue(relativePath, out UnityGuid? cached))
		{
			guid = cached ?? default;
			return cached.HasValue;
		}

		UnityGuid? result = ReadGuid(relativePath);
		cache.Add(relativePath, result);
		guid = result ?? default;
		return result.HasValue;
	}

	private UnityGuid? ReadGuid(string relativePath)
	{
		if (relativePath.StartsWith("..", StringComparison.Ordinal))
		{
			return null;
		}

		string metaPath = fileSystem.Path.Join(projectRootPath, relativePath) + ".meta";
		if (!fileSystem.File.Exists(metaPath))
		{
			return null;
		}

		string text;
		try
		{
			text = fileSystem.File.ReadAllText(metaPath);
		}
		catch (Exception ex)
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Failed to read reference meta file '{metaPath}': {ex.Message}");
			return null;
		}

		return TryParseGuid(text, out UnityGuid guid) ? guid : null;
	}

	internal static bool TryParseGuid(string metaText, out UnityGuid guid)
	{
		guid = default;
		foreach (string line in metaText.Split('\n'))
		{
			if (!line.StartsWith("guid:", StringComparison.Ordinal))
			{
				continue;
			}

			string value = line[5..].Trim();
			if (value.Length != 32)
			{
				return false;
			}

			try
			{
				guid = UnityGuid.Parse(value);
				return !guid.IsZero;
			}
			catch (Exception)
			{
				return false;
			}
		}
		return false;
	}
}
