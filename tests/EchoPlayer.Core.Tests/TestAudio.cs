using NAudio.Wave;

namespace EchoPlayer.Core.Tests;

/// <summary>Small in-memory signals and temporary WAV files for the tests.</summary>
internal static class TestAudio
{
    public const int SampleRate = 1000;

    /// <summary>A sample provider over a fixed array, read in chunks of <paramref name="chunk"/> samples.</summary>
    public sealed class ArraySource(float[] samples, int channels = 1, int sampleRate = SampleRate, int chunk = int.MaxValue) : ISampleProvider
    {
        private int _position;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(Math.Min(count, chunk), samples.Length - _position);
            Array.Copy(samples, _position, buffer, offset, n);
            _position += n;
            return n;
        }
    }

    public static float[] Impulse(int length, int at = 0, float value = 1)
    {
        var samples = new float[length];
        samples[at] = value;
        return samples;
    }

    public static float[] ReadAll(ISampleProvider provider, int chunk = 64)
    {
        var all = new List<float>();
        var buffer = new float[chunk];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            all.AddRange(buffer.Take(read));
        return all.ToArray();
    }

    /// <summary>Writes a WAV file in the given format and returns its path.</summary>
    public static string WriteWav(string folder, string name, float[] samples, WaveFormat format)
    {
        string path = Path.Combine(folder, name);
        using var writer = new WaveFileWriter(path, format);
        foreach (float sample in samples)
            writer.WriteSample(sample);
        return path;
    }

    public static float[] ReadWav(string path, out WaveFormat format)
    {
        using var reader = new WaveFileReader(path);
        format = reader.WaveFormat;
        return ReadAll(reader.ToSampleProvider(), 4096);
    }
}

/// <summary>A folder that is deleted after the test.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("echo-tests-").FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
