using NAudio.Wave;
using static EchoPlayer.Core.Tests.TestAudio;

namespace EchoPlayer.Core.Tests;

public sealed class EchoProcessorTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    public static TheoryData<int, int, bool> Formats => new()
    {
        { 16, 1, false },
        { 16, 2, false },
        { 24, 2, false },
        { 32, 1, true },
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void ProcessFile_KeepsFormatAndAddsTail(int bits, int channels, bool isFloat)
    {
        var format = isFloat ? WaveFormat.CreateIeeeFloatWaveFormat(8000, channels) : new WaveFormat(8000, bits, channels);
        var input = WriteWav(_temp.Path, "in.wav", Impulse(800 * channels, value: 0.5f), format);
        var output = Path.Combine(_temp.Path, "out.wav");
        var settings = new EchoSettings(100, Feedback: 0, Mix: 0.5);

        EchoProcessor.ProcessFile(input, output, settings);
        var samples = ReadWav(output, out var outFormat);

        Assert.Equal(format.SampleRate, outFormat.SampleRate);
        Assert.Equal(format.Channels, outFormat.Channels);
        Assert.Equal(format.BitsPerSample, outFormat.BitsPerSample);
        Assert.Equal(format.Encoding, outFormat.Encoding);
        Assert.Equal((800 + 800) * channels, samples.Length);   // one 100 ms repeat = 800 frames of tail
        Assert.Equal(0.25f, samples[800 * channels], 3);
    }

    [Fact]
    public void ProcessFile_ReportsProgressUpToOne()
    {
        var input = WriteWav(_temp.Path, "in.wav", new float[16000], new WaveFormat(8000, 16, 1));
        var reports = new List<double>();

        EchoProcessor.ProcessFile(input, Path.Combine(_temp.Path, "out.wav"), EchoSettings.Default, new SyncProgress(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(1, reports[^1]);
        Assert.Equal(reports.Order(), reports);
    }

    [Fact]
    public void ProcessFile_CancelledDeletesPartialOutput()
    {
        var input = WriteWav(_temp.Path, "in.wav", new float[80000], new WaveFormat(8000, 16, 1));
        var output = Path.Combine(_temp.Path, "out.wav");
        using var cts = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() =>
            EchoProcessor.ProcessFile(input, output, EchoSettings.Default, new SyncProgress(_ => cts.Cancel()), cts.Token));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ProcessFile_RejectsFileThatIsNotWav()
    {
        var input = Path.Combine(_temp.Path, "notes.wav");
        File.WriteAllText(input, "this is not audio");

        Assert.Throws<FormatException>(() => EchoProcessor.ProcessFile(input, Path.Combine(_temp.Path, "out.wav"), EchoSettings.Default));
    }

    [Fact]
    public void OutputPathFor_AddsSuffixAndAvoidsOverwriting()
    {
        string first = EchoProcessor.OutputPathFor(@"C:\music\song.wav", _temp.Path);
        File.WriteAllText(first, "");
        string second = EchoProcessor.OutputPathFor(@"C:\music\song.wav", _temp.Path);

        Assert.Equal(Path.Combine(_temp.Path, "song_echo.wav"), first);
        Assert.Equal(Path.Combine(_temp.Path, "song_echo (2).wav"), second);
    }

    [Fact]
    public void AudioInfo_ReadsHeader()
    {
        var input = WriteWav(_temp.Path, "in.wav", new float[44100 * 2], new WaveFormat(44100, 24, 2));

        var info = AudioInfo.Read(input);

        Assert.Equal(TimeSpan.FromSeconds(1), info.Duration);
        Assert.Equal("44.1 kHz · 24-bit · stereo", info.Description);
    }

    [Fact]
    public void Waveform_KeepsPeakOfEachBucket()
    {
        var samples = new float[1000];
        samples[10] = 0.3f;
        samples[260] = -0.9f;
        samples[999] = 0.5f;

        var waveform = Waveform.Compute(new ArraySource(samples), samples.Length, buckets: 4);

        Assert.Equal([0.3f, 0.9f, 0f, 0.5f], waveform.Peaks);
        Assert.Equal(TimeSpan.FromSeconds(1), waveform.Duration);
    }

    [Fact]
    public void Waveform_WithEchoIsLongerThanOriginal()
    {
        var input = WriteWav(_temp.Path, "in.wav", Impulse(8000, value: 0.8f), new WaveFormat(8000, 16, 1));
        var settings = new EchoSettings(250, 0.5, 0.6);

        var original = Waveform.FromFile(input, 100);
        var echoed = Waveform.FromFileWithEcho(input, settings, 100);

        Assert.Equal(TimeSpan.FromSeconds(1), original.Duration);
        Assert.Equal(TimeSpan.FromSeconds(1 + settings.AudibleRepeats() * 0.25), echoed.Duration);
    }

    /// <summary>Reports synchronously, unlike <see cref="Progress{T}"/>.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
