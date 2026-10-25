using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.NativeEnums.Fmod;
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using Fmod5Sharp.Util;

namespace AssetRipper.Export.Modules.Audio;

public static class AudioClipDecoder
{
	/// <summary>
	/// Information about an audio clip's source container and codec, obtained without decoding it.
	/// </summary>
	/// <param name="Container">The source container extension, for example <c>fsb</c>, <c>ogg</c>, or <c>it</c>.</param>
	/// <param name="Codec">The codec name reported by the container.</param>
	/// <param name="DecodedExtension">The file extension produced by <see cref="TryDecode"/>.</param>
	/// <param name="CanDecode">Whether <see cref="TryDecode"/> can convert this clip.</param>
	/// <param name="Message">A diagnostic message when the clip cannot be decoded.</param>
	public readonly record struct AudioClipProbe(string? Container, string? Codec, string? DecodedExtension, bool CanDecode, string? Message);

	public static AudioClipProbe Probe(IAudioClip audioClip)
	{
		byte[] rawData = audioClip.GetAudioData();
		if (rawData.Length == 0)
		{
			return new(null, null, null, false, $"Audio data for '{audioClip.Name}' could not be found.");
		}

		if (audioClip.Has_Type())
		{
			FmodSoundType soundType = audioClip.GetSoundType();
			return new(soundType.ToRawExtension(), soundType.ToString(), soundType.ToRawExtension(), true, null);
		}

		if (CheckMagic(rawData, "FSB5"u8))
		{
			try
			{
				if (FsbLoader.TryLoadFsbFromByteArray(rawData, out FmodSoundBank? fsbData))
				{
					FmodAudioType audioType = fsbData!.Header.AudioType;
					bool canDecode = audioType.IsSupported() && fsbData.Samples.Count == 1;
					string? message = canDecode
						? null
						: $"Fmod5Sharp cannot decode '{audioType}' or multi-sample banks.";
					return new("fsb", audioType.ToString(), canDecode ? GetDecodedExtension(audioType) : null, canDecode, message);
				}
			}
			catch (Exception ex)
			{
				return new("fsb", null, null, false, ex.Message);
			}
			return new("fsb", null, null, false, "Failed to parse the FSB5 header.");
		}

		if (CheckMagic(rawData, "IMPM"u8))
		{
			return new(FmodSoundType.It.ToRawExtension(), FmodSoundType.It.ToString(), FmodSoundType.It.ToRawExtension(), true, null);
		}
		if (CheckMagic(rawData, "Extended Module: "u8))
		{
			return new(FmodSoundType.Xm.ToRawExtension(), FmodSoundType.Xm.ToString(), FmodSoundType.Xm.ToRawExtension(), true, null);
		}
		if (CheckMagic(rawData, "SCRM"u8, 156))
		{
			return new(FmodSoundType.S3m.ToRawExtension(), FmodSoundType.S3m.ToString(), FmodSoundType.S3m.ToRawExtension(), true, null);
		}
		if (CheckMagic(rawData, "M.K."u8, 1080)
			|| CheckMagic(rawData, "M!K!"u8, 1080)
			|| CheckMagic(rawData, "FLT4"u8, 1080)
			|| CheckMagic(rawData, "FLT8"u8, 1080)
			|| CheckMagic(rawData, "4CHN"u8, 1080)
			|| CheckMagic(rawData, "6CHN"u8, 1080)
			|| CheckMagic(rawData, "8CHN"u8, 1080))
		{
			return new(FmodSoundType.Mod.ToRawExtension(), FmodSoundType.Mod.ToString(), FmodSoundType.Mod.ToRawExtension(), true, null);
		}

		Span<char> asciiCharacters = stackalloc char[4];
		CopyPrintable(rawData, asciiCharacters);
		Span<char> hexCharacters = stackalloc char[8];
		CopyHex(rawData, hexCharacters);
		return new(null, null, null, false, $"Unrecognized audio container starting with '{asciiCharacters}' ({hexCharacters})");
	}

	public static bool TryDecode(
		IAudioClip audioClip,
		[NotNullWhen(true)] out byte[]? decodedData,
		[NotNullWhen(true)] out string? fileExtension,
		[NotNullWhen(false)] out string? message)
	{
		byte[] rawData = audioClip.GetAudioData();

		if (rawData.Length == 0)
		{
			decodedData = null;
			fileExtension = null;
			message = $"Can't decode audio clip '{audioClip.Name}' with default decoder because its audio data could not be found.";
			return false;
		}

		if (audioClip.Has_Type())
		{
			fileExtension = audioClip.GetSoundType().ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		else if (CheckMagic(rawData, "FSB5"u8))
		{
			FmodAudioType audioType = (FmodAudioType)uint.MaxValue;
			try
			{
				if (FsbLoader.TryLoadFsbFromByteArray(rawData, out FmodSoundBank? fsbData))
				{
					audioType = fsbData!.Header.AudioType;
					if (audioType.IsSupported() && fsbData.Samples.Single().RebuildAsStandardFileFormat(out decodedData, out fileExtension))
					{
						message = null;
						return true;
					}
					else
					{
						decodedData = null;
						fileExtension = null;
						message = $"Can't decode audio clip '{audioClip.Name}' with Fmod5Sharp because it's '{audioType}' encoded.";
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				decodedData = null;
				fileExtension = null;
				message = $"Failed to convert audio ({audioType})\n{ex}";
				return false;
			}
		}
		else if (CheckMagic(rawData, "IMPM"u8))
		{
			fileExtension = FmodSoundType.It.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		else if (CheckMagic(rawData, "Extended Module: "u8))
		{
			fileExtension = FmodSoundType.Xm.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		// https://moddingwiki.shikadi.net/wiki/S3M_Format
		else if (CheckMagic(rawData, "SCRM"u8, 156))
		{
			fileExtension = FmodSoundType.S3m.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		// https://www.aes.id.au/modformat.html
		else if (
			CheckMagic(rawData, "M.K."u8, 1080) ||
			CheckMagic(rawData, "M!K!"u8, 1080) ||
			CheckMagic(rawData, "FLT4"u8, 1080) ||
			CheckMagic(rawData, "FLT8"u8, 1080) ||
			CheckMagic(rawData, "4CHN"u8, 1080) ||
			CheckMagic(rawData, "6CHN"u8, 1080) ||
			CheckMagic(rawData, "8CHN"u8, 1080))
		{
			fileExtension = FmodSoundType.Mod.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}

		decodedData = null;
		fileExtension = null;
		Span<char> asciiCharacters = stackalloc char[4];
		CopyPrintable(rawData, asciiCharacters);
		Span<char> hexCharacters = stackalloc char[8];
		CopyHex(rawData, hexCharacters);
		message = $"Failed to convert audio starting with '{asciiCharacters}' ({hexCharacters})";
		return false;
	}

	private static string GetDecodedExtension(FmodAudioType audioType)
	{
		return audioType == FmodAudioType.VORBIS ? "ogg" : "wav";
	}

	private static bool CheckMagic(byte[] data, ReadOnlySpan<byte> magic, int startIndex = 0)
	{
		if (data.Length < magic.Length + startIndex)
		{
			return false;
		}
		return data.AsSpan(startIndex, magic.Length).SequenceEqual(magic);
	}

	private static void CopyPrintable(ReadOnlySpan<byte> data, Span<char> characters)
	{
		const char FillCharacter = 'Ø';
		characters.Fill(FillCharacter);
		for (int i = 0; i < data.Length && i < characters.Length; i++)
		{
			char c = (char)data[i];
			if (IsAsciiPrintable(c) && c != '\t')
			{
				characters[i] = c;
			}
		}

		static bool IsAsciiPrintable(char c) => c >= (char)32 && c <= (char)126;
	}

	private static void CopyHex(ReadOnlySpan<byte> data, Span<char> characters)
	{
		ReadOnlySpan<char> hexCharacterLookup = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];
		const char FillCharacter = '0';
		characters.Fill(FillCharacter);
		for (int i = 0; i < data.Length && i < characters.Length / 2; i++)
		{
			byte b = data[i];
			int upper = (b & 0xF0) >> 4;
			characters[2 * i] = hexCharacterLookup[upper];
			int lower = b & 0x0F;
			characters[2 * i + 1] = hexCharacterLookup[lower];
		}
	}
}
