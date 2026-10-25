using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.Processing;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Addressables;

/// <summary>
/// Copies the Addressables catalog and settings into the exported project's StreamingAssets folder.
/// </summary>
/// <remarks>
/// Only JSON files are copied. The catalog is not remapped to the exported asset paths, so this
/// mainly preserves the data for editor-side resolvers or manual inspection.
/// </remarks>
public sealed class AddressablesPostExporter : IPostExporter
{
	public const string FileName = "addressables-report.json";

	private readonly IReadOnlyList<string> inputPaths;
	private readonly FileSystem? inputFileSystem;

	public AddressablesPostExporter() : this([], null)
	{
	}

	/// <param name="inputPaths">The paths that were loaded. Used when a mixed/raw directory input has no platform structure.</param>
	/// <param name="inputFileSystem">The file system used for the loaded input paths.</param>
	public AddressablesPostExporter(IReadOnlyList<string>? inputPaths, FileSystem? inputFileSystem)
	{
		this.inputPaths = inputPaths ?? [];
		this.inputFileSystem = inputFileSystem;
	}

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		SourceLocation? source = FindSourceLocation(gameData.PlatformStructure);
		List<string> copiedFiles = new();

		if (source is not null)
		{
			string destinationRoot = fileSystem.Path.Join(settings.AssetsPath, "StreamingAssets", "aa");
			fileSystem.Directory.Create(destinationRoot);

			foreach (string sourceFile in source.FileSystem.Directory.EnumerateFiles(source.Directory, "*.json", SearchOption.AllDirectories))
			{
				string relativePath = source.FileSystem.Path.GetRelativePath(source.Directory, sourceFile);
				string destinationPath = fileSystem.Path.Join(destinationRoot, relativePath);
				string? destinationDirectory = fileSystem.Path.GetDirectoryName(destinationPath);
				if (!string.IsNullOrEmpty(destinationDirectory))
				{
					fileSystem.Directory.Create(destinationDirectory);
				}

				using (Stream readStream = source.FileSystem.File.OpenRead(sourceFile))
				using (Stream writeStream = fileSystem.File.Create(destinationPath))
				{
					readStream.CopyTo(writeStream);
				}
				copiedFiles.Add(relativePath.Replace('\\', '/'));
			}
		}

		AddressablesReport report = new()
		{
			SourceDirectory = source?.Directory,
			DestinationDirectory = "Assets/StreamingAssets/aa",
			CopiedFiles = copiedFiles,
			HasCatalog = copiedFiles.Any(f => string.Equals(Path.GetFileName(f), "catalog.json", StringComparison.OrdinalIgnoreCase)),
			HasSettings = copiedFiles.Any(f => string.Equals(Path.GetFileName(f), "settings.json", StringComparison.OrdinalIgnoreCase)),
			Note = GetNote(gameData.PlatformStructure, source),
		};

		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using (Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName)))
		{
			JsonSerializer.Serialize(stream, report, AddressablesReportSerializerContext.Default.AddressablesReport);
			stream.Flush();
		}

		if (report.HasCatalog)
		{
			Logger.Info(LogCategory.Export, $"Copied {copiedFiles.Count} Addressables files from '{source!.Directory}'.");
		}
		else if (source is not null)
		{
			Logger.Warning(LogCategory.Export, $"Found Addressables directory '{source.Directory}', but no catalog.json was copied.");
		}
	}

	private SourceLocation? FindSourceLocation(PlatformGameStructure? platform)
	{
		if (platform is not null)
		{
			SourceLocation? location = FindSourceLocation(platform.FileSystem, GetPlatformCandidates(platform));
			if (location is not null)
			{
				return location;
			}
		}

		if (inputFileSystem is not null)
		{
			foreach (string inputPath in inputPaths)
			{
				if (!inputFileSystem.Directory.Exists(inputPath))
				{
					continue;
				}

				SourceLocation? location = FindSourceLocation(inputFileSystem, GetInputCandidates(inputPath));
				if (location is not null)
				{
					return location;
				}
			}
		}

		return null;
	}

	private static SourceLocation? FindSourceLocation(FileSystem fileSystem, IEnumerable<string?> candidates)
	{
		foreach (string? candidate in candidates)
		{
			if (candidate is not null && fileSystem.Directory.Exists(candidate))
			{
				return new SourceLocation(fileSystem, candidate);
			}
		}
		return null;
	}

	private static IEnumerable<string?> GetPlatformCandidates(PlatformGameStructure platform)
	{
		FileSystem fileSystem = platform.FileSystem;
		yield return GetSubdirectory(fileSystem, platform.StreamingAssetsPath, "aa");
		yield return GetSubdirectory(fileSystem, platform.RootPath, "assets", "aa");
		yield return GetSubdirectory(fileSystem, platform.RootPath, "aa");
		yield return GetSubdirectory(fileSystem, platform.GameDataPath, "Raw", "aa");
	}

	private static IEnumerable<string?> GetInputCandidates(string inputPath)
	{
		yield return Path.Join(inputPath, "assets", "aa");
		yield return Path.Join(inputPath, "aa");
		yield return Path.Join(inputPath, "apk", "assets", "aa");
	}

	private static string? GetSubdirectory(FileSystem fileSystem, string? root, params string[] parts)
	{
		if (string.IsNullOrEmpty(root))
		{
			return null;
		}

		string path = root;
		foreach (string part in parts)
		{
			path = fileSystem.Path.Join(path, part);
		}
		return path;
	}

	private static string GetNote(PlatformGameStructure? platform, SourceLocation? source)
	{
		if (source is not null)
		{
			return "Catalog keys are not remapped to the exported asset paths.";
		}
		if (platform is null)
		{
			return "Platform structure is unavailable and no Addressables 'aa' directory was found in the input paths.";
		}
		return "No Addressables 'aa' directory was found.";
	}

	private sealed record class SourceLocation(FileSystem FileSystem, string Directory);
}
