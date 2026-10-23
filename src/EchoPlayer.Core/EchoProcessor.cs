using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EchoPlayer.Core;

/// <summary>Applies an echo to WAV files.</summary>
public static class EchoProcessor
{
    public const string OutputSuffix = "_echo";

    /// <summary>
    /// Writes <paramref name="inputPath"/> with the echo, and its tail, to <paramref name="outputPath"/>.
    /// The output keeps the sample rate, channels and bit depth of the input (8-bit input is written as 16-bit).
    /// </summary>
    /// <param name="progress">Receives the finished fraction, 0 to 1.</param>
    /// <returns>How many output samples had to be clipped.</returns>
    /// <exception cref="OperationCanceledException">Cancelled; the partial output file is deleted.</exception>
    public static long ProcessFile(string inputPath, string outputPath, EchoSettings settings,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        try
        {
            using var reader = new WaveFileReader(inputPath);
            AudioInfo.EnsureSupported(reader.WaveFormat);

            var echo = new EchoSampleProvider(reader.ToSampleProvider(), settings, includeTail: true);
            IWaveProvider output = OutputFor(reader.WaveFormat, echo);

            int channels = reader.WaveFormat.Channels;
            long totalBytes = (reader.SampleCount + settings.TailFrames(reader.WaveFormat.SampleRate))
                              * channels * output.WaveFormat.BitsPerSample / 8;

            using (var writer = new WaveFileWriter(outputPath, output.WaveFormat))
            {
                var buffer = new byte[output.WaveFormat.AverageBytesPerSecond / 4 / output.WaveFormat.BlockAlign * output.WaveFormat.BlockAlign];
                long written = 0;
                int read;
                while ((read = output.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.Write(buffer, 0, read);
                    written += read;
                    progress?.Report(Math.Min(1, (double)written / totalBytes));
                }
            }

            progress?.Report(1);
            return echo.ClippedSamples;
        }
        catch (OperationCanceledException)
        {
            TryDelete(outputPath);
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            TryDelete(outputPath);
            throw new FormatException($"{Path.GetFileName(inputPath)} is not a valid WAV file.", ex);
        }
    }

    /// <summary>
    /// The output path for an input: <c>song.wav</c> becomes <c>song_echo.wav</c> in <paramref name="folder"/>,
    /// or <c>song_echo (2).wav</c> and so on if that file already exists.
    /// </summary>
    public static string OutputPathFor(string inputPath, string folder)
    {
        string name = Path.GetFileNameWithoutExtension(inputPath) + OutputSuffix;
        string path = Path.Combine(folder, name + ".wav");
        for (int i = 2; File.Exists(path); i++)
            path = Path.Combine(folder, $"{name} ({i}).wav");
        return path;
    }

    private static IWaveProvider OutputFor(WaveFormat input, ISampleProvider samples) =>
        input.Encoding == WaveFormatEncoding.IeeeFloat ? new SampleToWaveProvider(samples)
        : input.BitsPerSample >= 24 ? new SampleToWaveProvider24(samples)
        : new SampleToWaveProvider16(samples);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort: a locked partial file is left for the user to remove.
        }
    }
}
