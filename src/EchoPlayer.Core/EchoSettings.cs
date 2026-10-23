namespace EchoPlayer.Core;

/// <summary>Parameters of a feedback echo.</summary>
/// <param name="DelayMs">Time between the sound and each repeat, in milliseconds.</param>
/// <param name="Feedback">How much of each repeat is fed back into the next one (0 = a single repeat).</param>
/// <param name="Mix">Loudness of the first repeat relative to the original sound.</param>
public sealed record EchoSettings(double DelayMs = 350, double Feedback = 0.45, double Mix = 0.6)
{
    public const double MinDelayMs = 20;
    public const double MaxDelayMs = 2000;
    public const double MaxFeedback = 0.9;
    public const double MaxMix = 1;

    /// <summary>Repeats quieter than this (−60 dB) are treated as silence when the tail is cut.</summary>
    public const double SilenceThreshold = 0.001;

    /// <summary>The tail never gets longer than this, however slowly the echo decays.</summary>
    public static readonly TimeSpan MaxTail = TimeSpan.FromSeconds(30);

    public static EchoSettings Default { get; } = new();

    /// <summary>Throws if a parameter is outside its allowed range.</summary>
    public EchoSettings Validate()
    {
        if (!(DelayMs >= MinDelayMs && DelayMs <= MaxDelayMs))
            throw new ArgumentOutOfRangeException(nameof(DelayMs), DelayMs, $"Delay must be between {MinDelayMs} and {MaxDelayMs} ms.");
        if (!(Feedback >= 0 && Feedback <= MaxFeedback))
            throw new ArgumentOutOfRangeException(nameof(Feedback), Feedback, $"Feedback must be between 0 and {MaxFeedback}.");
        if (!(Mix >= 0 && Mix <= MaxMix))
            throw new ArgumentOutOfRangeException(nameof(Mix), Mix, $"Mix must be between 0 and {MaxMix}.");
        return this;
    }

    /// <summary>Delay in frames (one sample per channel) at the given sample rate.</summary>
    public int DelayFrames(int sampleRate) => Math.Max(1, (int)Math.Round(DelayMs / 1000 * sampleRate));

    /// <summary>
    /// Number of repeats louder than <see cref="SilenceThreshold"/>.
    /// Repeat <c>j</c> has the amplitude <c>Mix · Feedbackʲ⁻¹</c>.
    /// </summary>
    public int AudibleRepeats()
    {
        if (Mix < SilenceThreshold)
            return 0;
        if (Feedback == 0)
            return 1;

        return 1 + (int)Math.Floor(Math.Log(SilenceThreshold / Mix) / Math.Log(Feedback));
    }

    /// <summary>How long the echo keeps sounding after the input ends, in frames.</summary>
    public long TailFrames(int sampleRate)
    {
        long tail = (long)AudibleRepeats() * DelayFrames(sampleRate);
        return Math.Min(tail, (long)(MaxTail.TotalSeconds * sampleRate));
    }
}
