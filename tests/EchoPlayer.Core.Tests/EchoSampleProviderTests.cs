using static EchoPlayer.Core.Tests.TestAudio;

namespace EchoPlayer.Core.Tests;

public class EchoSampleProviderTests
{
    // 20 ms at 1 kHz = a delay of 20 samples.
    private const int D = 20;

    [Fact]
    public void ImpulseResponse_IsDecayingRepeats()
    {
        var echo = new EchoSampleProvider(new ArraySource(Impulse(10)), new EchoSettings(20, Feedback: 0.5, Mix: 0.8));

        var output = ReadAll(echo);

        Assert.Equal(1f, output[0]);
        Assert.Equal(0.8f, output[D], 6);
        Assert.Equal(0.8f * 0.5f, output[2 * D], 6);
        Assert.Equal(0.8f * 0.25f, output[3 * D], 6);
        Assert.All(output.Where((_, i) => i % D != 0), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void ZeroFeedback_GivesSingleRepeat()
    {
        var echo = new EchoSampleProvider(new ArraySource(Impulse(5)), new EchoSettings(20, Feedback: 0, Mix: 0.5));

        var output = ReadAll(echo);

        Assert.Equal(5 + D, output.Length);
        Assert.Equal(0.5f, output[D]);
        Assert.Equal(1, output.Count(s => s != 0) - 1);
    }

    [Fact]
    public void Tail_HasExpectedLength()
    {
        var settings = new EchoSettings(20, Feedback: 0.45, Mix: 0.6);
        var echo = new EchoSampleProvider(new ArraySource(Impulse(50)), settings);

        var output = ReadAll(echo);

        // Repeat j of the impulse lands on sample j·D: the 9th is the last one above −60 dB.
        Assert.Equal(50 + settings.TailFrames(SampleRate), output.Length);
        Assert.True(output[9 * D] >= EchoSettings.SilenceThreshold);
        Assert.True(output[10 * D] < EchoSettings.SilenceThreshold);
    }

    [Fact]
    public void WithoutTail_StopsWithSource()
    {
        var echo = new EchoSampleProvider(new ArraySource(Impulse(50)), new EchoSettings(20, 0.5, 0.5), includeTail: false);

        Assert.Equal(50, ReadAll(echo).Length);
    }

    [Fact]
    public void Stereo_EchoesEachChannelSeparately()
    {
        // Interleaved L R L R ...: an impulse only in the right channel.
        var samples = new float[20];
        samples[1] = 1;
        var echo = new EchoSampleProvider(new ArraySource(samples, channels: 2), new EchoSettings(20, Feedback: 0, Mix: 0.5));

        var output = ReadAll(echo);

        Assert.Equal(0.5f, output[2 * D + 1]);
        Assert.Equal(0f, output[2 * D]);
        Assert.Equal(20 + 2 * D, output.Length);
    }

    [Fact]
    public void ResultDoesNotDependOnChunkSize()
    {
        var input = Enumerable.Range(0, 300).Select(i => MathF.Sin(i * 0.1f) * 0.5f).ToArray();
        var settings = new EchoSettings(20, 0.6, 0.7);

        var whole = ReadAll(new EchoSampleProvider(new ArraySource(input), settings), chunk: 4096);
        var pieces = ReadAll(new EchoSampleProvider(new ArraySource(input, chunk: 7), settings), chunk: 13);

        Assert.Equal(whole, pieces);
    }

    [Fact]
    public void Output_IsClippedAndCounted()
    {
        var input = Enumerable.Repeat(0.9f, 100).ToArray();
        var echo = new EchoSampleProvider(new ArraySource(input), new EchoSettings(20, 0, 1), includeTail: false);

        var output = ReadAll(echo);

        Assert.All(output, s => Assert.InRange(s, -1f, 1f));
        Assert.Equal(100 - D, echo.ClippedSamples);
    }

    [Fact]
    public void Bypass_PassesInputThrough()
    {
        var input = Impulse(60);
        var echo = new EchoSampleProvider(new ArraySource(input), new EchoSettings(20, 0.5, 1), includeTail: false) { Bypass = true };

        Assert.Equal(input, ReadAll(echo));
    }

    [Fact]
    public void Settings_CanChangeWhilePlaying()
    {
        var echo = new EchoSampleProvider(new ArraySource(Impulse(200)), new EchoSettings(20, 0, 1), includeTail: false);
        var buffer = new float[10];
        echo.Read(buffer, 0, 10);

        echo.Settings = new EchoSettings(40, 0, 0.25);
        var rest = ReadAll(echo);

        // The impulse at 0 now comes back 40 samples later, at a quarter of the level.
        Assert.Equal(0.25f, rest[40 - 10]);
        Assert.Equal(0f, rest[20 - 10]);
    }

    [Fact]
    public void Reset_ClearsDelayLineAndSetsPosition()
    {
        var echo = new EchoSampleProvider(new ArraySource(Impulse(100)), new EchoSettings(20, 0.5, 1), includeTail: false);
        echo.Read(new float[10], 0, 10);

        echo.Reset(position: 500);

        Assert.Equal(500, echo.Position);
        Assert.All(ReadAll(echo), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void Position_CountsSamplesIncludingTail()
    {
        var settings = new EchoSettings(20, 0, 1);
        var echo = new EchoSampleProvider(new ArraySource(Impulse(30)), settings);

        ReadAll(echo);

        Assert.Equal(30 + settings.TailFrames(SampleRate), echo.Position);
    }
}
