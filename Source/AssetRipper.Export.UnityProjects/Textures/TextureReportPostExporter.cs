using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Textures;

/// <summary>
/// Writes a report listing the textures that were replaced with placeholders.
/// </summary>
public sealed class TextureReportPostExporter : IPostExporter
{
	public const string FileName = "texture-report.json";

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		TextureReport report = new() { ExportVersion = settings.Version.ToString() };

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (IUnityObjectBase asset in collection)
			{
				if (asset is not ITexture2D texture)
				{
					continue;
				}

				report.TotalCount++;
				if (TextureAssetExporter.IsPlaceholder(texture))
				{
					report.PlaceholderCount++;
					report.Placeholders.Add(new TextureReportEntry
					{
						Name = texture.GetBestName(),
						PathID = texture.PathID,
						Collection = collection.Name,
						Width = texture.Width_C28,
						Height = texture.Height_C28,
					});
				}
			}
		}

		report.Placeholders.Sort((a, b) =>
		{
			int result = string.CompareOrdinal(a.Collection, b.Collection);
			if (result != 0)
			{
				return result;
			}
			result = string.CompareOrdinal(a.Name, b.Name);
			return result != 0 ? result : a.PathID.CompareTo(b.PathID);
		});

		Write(settings, fileSystem, report);
		Log(report);
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, TextureReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, TextureReportSerializerContext.Default.TextureReport);
		stream.Flush();
	}

	private static void Log(TextureReport report)
	{
		if (report.PlaceholderCount == 0)
		{
			Logger.Info(LogCategory.Export, $"No placeholder textures were exported out of {report.TotalCount} textures.");
		}
		else
		{
			Logger.Warning(LogCategory.Export,
				$"{report.PlaceholderCount} of {report.TotalCount} textures were exported as placeholders. See '{FileName}'.");
		}
	}
}
