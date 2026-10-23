namespace EchoPlayer.Core.Tests;

public class EchoSettingsTests
{
    [Fact]
    public void DelayFrames_ConvertsMilliseconds()
    {
        Assert.Equal(15435, new EchoSettings(DelayMs: 350).DelayFrames(44100));
        Assert.Equal(20, new EchoSettings(DelayMs: 20).DelayFrames(1000));
    }

    [Theory]
    [InlineData(0.6, 0.0, 1)]      // a single repeat
    [InlineData(0.6, 0.45, 9)]     // 0.6 · 0.45⁸ ≈ 0.001009 is the last one above −60 dB
    [InlineData(1.0, 0.5, 10)]     // 0.5⁹ ≈ 0.00195, 0.5¹⁰ ≈ 0.00098
    [InlineData(0.0, 0.8, 0)]      // a silent echo has no tail
    public void AudibleRepeats_CountsRepeatsAboveThreshold(double mix, double feedback, int expected)
    {
        Assert.Equal(expected, new EchoSettings(100, feedback, mix).AudibleRepeats());
    }

    [Fact]
    public void TailFrames_IsRepeatsTimesDelay()
    {
        var settings = new EchoSettings(DelayMs: 100, Feedback: 0.45, Mix: 0.6);

        Assert.Equal(9 * 100, settings.TailFrames(1000));
    }

    [Fact]
    public void TailFrames_IsCapped()
    {
        var settings = new EchoSettings(DelayMs: 2000, Feedback: 0.9, Mix: 1);

        Assert.Equal(30 * 1000, settings.TailFrames(1000));
    }

    [Theory]
    [InlineData(10, 0.5, 0.5)]
    [InlineData(2500, 0.5, 0.5)]
    [InlineData(100, -0.1, 0.5)]
    [InlineData(100, 0.95, 0.5)]
    [InlineData(100, 0.5, 1.5)]
    [InlineData(double.NaN, 0.5, 0.5)]
    public void Validate_RejectsOutOfRange(double delay, double feedback, double mix)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EchoSettings(delay, feedback, mix).Validate());
    }

    [Fact]
    public void Default_IsValid()
    {
        Assert.Same(EchoSettings.Default, EchoSettings.Default.Validate());
    }
}
