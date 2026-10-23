using NAudio.Wave;

namespace EchoPlayer.Core;

/// <summary>Format and length of a WAV file.</summary>
public sealed record AudioInfo(TimeSpan Duration, int SampleRate, int BitsPerSample, int Channels, bool IsFloat)
{
    /// <summary>Reads the header of a WAV file.</summary>
    /// <exception cref="FormatException">The file is not a WAV file this app can read.</exception>
    public static AudioInfo Read(string path)
    {
        try
        {
            using var reader = new WaveFileReader(path);
            var format = reader.WaveFormat;
            EnsureSupported(format);
            return new AudioInfo(reader.TotalTime, format.SampleRate, format.BitsPerSample, format.Channels,
                format.Encoding == WaveFormatEncoding.IeeeFloat);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or EndOfStreamException)
        {
            throw new FormatException($"{Path.GetFileName(path)} is not a valid WAV file.", ex);
        }
    }

    /// <summary>For example "44.1 kHz · 16-bit · stereo".</summary>
    public string Description
    {
        get
        {
            string rate = (SampleRate / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string depth = IsFloat ? "32-bit float" : $"{BitsPerSample}-bit";
            string channels = Channels switch { 1 => "mono", 2 => "stereo", _ => $"{Channels} channels" };
            return $"{rate} kHz · {depth} · {channels}";
        }
    }

    internal static void EnsureSupported(WaveFormat format)
    {
        bool pcm = format.Encoding is WaveFormatEncoding.Pcm or WaveFormatEncoding.Extensible
                   && format.BitsPerSample is 8 or 16 or 24 or 32;
        bool ieee = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
        if (!pcm && !ieee)
            throw new FormatException($"Unsupported WAV encoding: {format.Encoding}, {format.BitsPerSample}-bit. Use PCM (8/16/24/32-bit) or 32-bit float.");
    }
}
