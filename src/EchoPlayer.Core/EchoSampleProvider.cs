using NAudio.Wave;

namespace EchoPlayer.Core;

/// <summary>
/// Adds a feedback echo to any sample stream:
/// <code>
/// d[n] = x[n] + feedback · d[n − D]     (the delay line)
/// y[n] = x[n] + mix · d[n − D]          (what you hear)
/// </code>
/// Settings can change while it plays, and after the source ends it keeps producing
/// the echo tail until the repeats fall below −60 dB.
/// </summary>
public sealed class EchoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float[] _line;
    private readonly int _channels;
    private readonly bool _includeTail;

    private int _write;
    private int _delaySamples;
    private float _feedback;
    private float _mix;
    private EchoSettings _settings;
    private bool _sourceEnded;
    private long _tailRemaining;

    /// <param name="source">The dry signal.</param>
    /// <param name="settings">Initial echo parameters.</param>
    /// <param name="includeTail">Keep playing the echo after the source ends.</param>
    public EchoSampleProvider(ISampleProvider source, EchoSettings settings, bool includeTail = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        _source = source;
        _channels = source.WaveFormat.Channels;
        _includeTail = includeTail;
        _line = new float[(int)Math.Ceiling(EchoSettings.MaxDelayMs / 1000 * source.WaveFormat.SampleRate) * _channels];
        _settings = settings;
        Apply(settings.Validate());
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Taken while reading; hold it to reposition the source safely during playback.</summary>
    public object SyncRoot { get; } = new();

    /// <summary>Pass the input through unchanged, to compare with and without the echo.</summary>
    public bool Bypass { get; set; }

    /// <summary>Samples produced since the start or the last <see cref="Reset"/>, tail included.</summary>
    public long Position { get; private set; }

    /// <summary>Number of output samples that had to be clipped to [−1, 1].</summary>
    public long ClippedSamples { get; private set; }

    public EchoSettings Settings
    {
        get => _settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (SyncRoot)
            {
                _settings = value.Validate();
                Apply(value);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (SyncRoot)
        {
            int read = _sourceEnded ? 0 : _source.Read(buffer, offset, count);

            if (read == 0 && !_sourceEnded)
            {
                _sourceEnded = true;
                _tailRemaining = _includeTail ? _settings.TailFrames(WaveFormat.SampleRate) * _channels : 0;
            }

            if (read == 0)
            {
                read = (int)Math.Min(count - count % _channels, _tailRemaining);
                if (read <= 0)
                    return 0;

                Array.Clear(buffer, offset, read);
                _tailRemaining -= read;
            }

            Process(buffer, offset, read);
            Position += read;
            return read;
        }
    }

    /// <summary>Clears the delay line, for example after seeking the source to <paramref name="position"/>.</summary>
    public void Reset(long position = 0)
    {
        lock (SyncRoot)
        {
            Array.Clear(_line);
            _write = 0;
            _sourceEnded = false;
            _tailRemaining = 0;
            Position = position;
        }
    }

    private void Process(float[] buffer, int offset, int count)
    {
        int length = _line.Length;
        int delay = _delaySamples;
        float feedback = _feedback;
        float mix = _mix;
        bool bypass = Bypass;

        for (int i = offset; i < offset + count; i++)
        {
            int readIndex = _write - delay;
            if (readIndex < 0)
                readIndex += length;

            float delayed = _line[readIndex];
            float input = buffer[i];
            _line[_write] = input + feedback * delayed;
            if (++_write == length)
                _write = 0;

            float output = bypass ? input : input + mix * delayed;
            if (output > 1f || output < -1f)
            {
                output = Math.Clamp(output, -1f, 1f);
                ClippedSamples++;
            }

            buffer[i] = output;
        }
    }

    private void Apply(EchoSettings settings)
    {
        _delaySamples = Math.Min(_line.Length, settings.DelayFrames(WaveFormat.SampleRate) * _channels);
        _feedback = (float)settings.Feedback;
        _mix = (float)settings.Mix;
    }
}
