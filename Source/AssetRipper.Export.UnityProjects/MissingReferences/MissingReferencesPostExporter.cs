using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.MissingReferences;

public static class MissingReferencesPostExporter
{
	public const string FileName = "missing-references.json";

	public static void Write(FullConfiguration settings, FileSystem fileSystem, MissingReferenceCollector collector)
	{
		string projectRootPath = settings.ProjectRootPath;
		MissingReferencesReport report = new()
		{
			ExportVersion = settings.Version.ToString(),
			PlaceholderGuid = UnityGuid.MissingReference.ToString(),
			TotalCount = collector.Count,
			TotalOccurrences = collector.TotalOccurrences,
			References = collector.GetSortedReferences().Select(info => Copy(fileSystem, projectRootPath, info)).ToList(),
			NullInSourceCount = collector.NullInSourceCount,
			NullInSourceOccurrences = collector.NullInSourceOccurrences,
			NullInSourceTruncated = collector.NullInSourceTruncated,
			NullInSourceReferences = collector.GetSortedNullInSourceReferences().Select(info => Copy(fileSystem, projectRootPath, info)).ToList(),
		};

		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, MissingReferencesSerializerContext.Default.MissingReferencesReport);
		stream.Flush();

		if (report.TotalCount == 0)
		{
			Logger.Info(LogCategory.Export, "No missing references were found.");
		}
		else
		{
			Logger.Warning(LogCategory.Export, $"{report.TotalCount} distinct missing references ({report.TotalOccurrences} occurrences) were found. See '{FileName}'.");
		}

		if (report.NullInSourceCount > 0)
		{
			string truncated = report.NullInSourceTruncated ? $", truncated to {MissingReferenceCollector.MaxNullInSourceReferences}" : string.Empty;
			Logger.Info(LogCategory.Export, $"{report.NullInSourceCount} distinct null source pointers ({report.NullInSourceOccurrences} occurrences{truncated}) were found. These are not export failures.");
		}
	}

	private static MissingReferenceInfo Copy(FileSystem fileSystem, string projectRootPath, MissingReferenceInfo info)
	{
		return new MissingReferenceInfo
		{
			ReferrerPath = GetRelativePath(fileSystem, projectRootPath, info.ReferrerPath),
			ReferrerSourceFile = info.ReferrerSourceFile,
			ReferrerClassID = info.ReferrerClassID,
			ReferrerPathID = info.ReferrerPathID,
			ReferrerExportID = info.ReferrerExportID,
			DocumentKind = info.DocumentKind,
			FieldPath = info.FieldPath,
			TargetClassID = info.TargetClassID,
			TargetPathID = info.TargetPathID,
			TargetFileID = info.TargetFileID,
			TargetSourceFile = info.TargetSourceFile,
			Reason = info.Reason,
			PlaceholderFileID = info.PlaceholderFileID,
			Occurrences = info.Occurrences,
		};
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
