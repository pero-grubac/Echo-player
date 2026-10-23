using NAudio.Wave;

namespace EchoPlayer.Core;

/// <summary>A downsampled outline of a sound: the loudest sample in each of a fixed number of buckets.</summary>
public sealed record Waveform(IReadOnlyList<float> Peaks, TimeSpan Duration)
{
    public static Waveform Empty { get; } = new([], TimeSpan.Zero);

    /// <summary>Peaks of the original file.</summary>
    public static Waveform FromFile(string path, int buckets, CancellationToken cancellationToken = default)
    {
        using var reader = new WaveFileReader(path);
        return Compute(reader.ToSampleProvider(), reader.SampleCount, buckets, cancellationToken);
    }

    /// <summary>Peaks of the file with the echo applied, tail included.</summary>
    public static Waveform FromFileWithEcho(string path, EchoSettings settings, int buckets, CancellationToken cancellationToken = default)
    {
        using var reader = new WaveFileReader(path);
        var echo = new EchoSampleProvider(reader.ToSampleProvider(), settings, includeTail: true);
        return Compute(echo, reader.SampleCount + settings.TailFrames(reader.WaveFormat.SampleRate), buckets, cancellationToken);
    }

    /// <summary>Reads <paramref name="source"/> to the end and keeps the peak of every bucket.</summary>
    /// <param name="totalFrames">Expected length in frames, used to size the buckets.</param>
    public static Waveform Compute(ISampleProvider source, long totalFrames, int buckets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(buckets, 1);

        int channels = source.WaveFormat.Channels;
        long framesPerBucket = Math.Max(1, (long)Math.Ceiling(totalFrames / (double)buckets));
        var peaks = new float[buckets];
        var buffer = new float[source.WaveFormat.SampleRate * channels / 10];

        long frame = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int i = 0; i < read; i++)
            {
                long bucket = (frame + i / channels) / framesPerBucket;
                if (bucket >= buckets)
                    break;

                float magnitude = Math.Abs(buffer[i]);
                if (magnitude > peaks[bucket])
                    peaks[bucket] = magnitude;
            }

            frame += read / channels;
        }

        return new Waveform(peaks, TimeSpan.FromSeconds((double)frame / source.WaveFormat.SampleRate));
    }
}
