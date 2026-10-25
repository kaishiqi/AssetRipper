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

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		PlatformGameStructure? platform = gameData.PlatformStructure;
		string? sourceDirectory = platform is null ? null : FindSourceDirectory(platform);
		List<string> copiedFiles = new();

		if (sourceDirectory is not null)
		{
			string destinationRoot = fileSystem.Path.Join(settings.AssetsPath, "StreamingAssets", "aa");
			fileSystem.Directory.Create(destinationRoot);

			foreach (string sourceFile in platform!.FileSystem.Directory.EnumerateFiles(sourceDirectory, "*.json", SearchOption.AllDirectories))
			{
				string relativePath = platform.FileSystem.Path.GetRelativePath(sourceDirectory, sourceFile);
				string destinationPath = fileSystem.Path.Join(destinationRoot, relativePath);
				string? destinationDirectory = fileSystem.Path.GetDirectoryName(destinationPath);
				if (!string.IsNullOrEmpty(destinationDirectory))
				{
					fileSystem.Directory.Create(destinationDirectory);
				}

				using (Stream readStream = platform.FileSystem.File.OpenRead(sourceFile))
				using (Stream writeStream = fileSystem.File.Create(destinationPath))
				{
					readStream.CopyTo(writeStream);
				}
				copiedFiles.Add(relativePath.Replace('\\', '/'));
			}
		}

		AddressablesReport report = new()
		{
			SourceDirectory = sourceDirectory,
			DestinationDirectory = "Assets/StreamingAssets/aa",
			CopiedFiles = copiedFiles,
			HasCatalog = copiedFiles.Any(f => string.Equals(Path.GetFileName(f), "catalog.json", StringComparison.OrdinalIgnoreCase)),
			HasSettings = copiedFiles.Any(f => string.Equals(Path.GetFileName(f), "settings.json", StringComparison.OrdinalIgnoreCase)),
			Note = GetNote(platform, sourceDirectory),
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
			Logger.Info(LogCategory.Export, $"Copied {copiedFiles.Count} Addressables files from '{sourceDirectory}'.");
		}
		else if (sourceDirectory is not null)
		{
			Logger.Warning(LogCategory.Export, $"Found Addressables directory '{sourceDirectory}', but no catalog.json was copied.");
		}
	}

	private static string? FindSourceDirectory(PlatformGameStructure platform)
	{
		FileSystem fileSystem = platform.FileSystem;
		List<string?> candidates = new()
		{
			GetSubdirectory(platform.StreamingAssetsPath, "aa"),
			GetSubdirectory(platform.RootPath, "assets", "aa"),
			GetSubdirectory(platform.RootPath, "aa"),
			GetSubdirectory(platform.GameDataPath, "Raw", "aa"),
		};

		foreach (string? candidate in candidates)
		{
			if (candidate is not null && fileSystem.Directory.Exists(candidate))
			{
				return candidate;
			}
		}
		return null;

		string? GetSubdirectory(string? root, params string[] parts)
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
	}

	private static string GetNote(PlatformGameStructure? platform, string? sourceDirectory)
	{
		if (platform is null)
		{
			return "Platform structure is unavailable (mixed or raw file input); Addressables files were not copied.";
		}
		if (sourceDirectory is null)
		{
			return "No Addressables 'aa' directory was found.";
		}

		return "Catalog keys are not remapped to the exported asset paths.";
	}
}
