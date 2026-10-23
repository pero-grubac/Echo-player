using EchoPlayer.Core;
using NAudio.Wave;

namespace EchoPlayer.Desktop.Services;

/// <summary>Plays one file through the echo, with settings that can change while it plays.</summary>
public interface IAudioPreview : IDisposable
{
    /// <summary>Raised on the UI thread when playback reaches the end of the echo tail.</summary>
    event EventHandler? PlaybackEnded;

    bool IsLoaded { get; }

    bool IsPlaying { get; }

    /// <summary>Length of the file itself, without the echo tail.</summary>
    TimeSpan SourceDuration { get; }

    /// <summary>What is being heard right now, tail included.</summary>
    TimeSpan Position { get; }

    /// <summary>Hear the original instead of the echo.</summary>
    bool Bypass { get; set; }

    EchoSettings Settings { get; set; }

    /// <exception cref="FormatException">The file cannot be read.</exception>
    void Load(string path);

    void Unload();

    void Play();

    void Pause();

    void Stop();

    void Seek(TimeSpan position);
}

public sealed class AudioPreview : IAudioPreview
{
    private WaveFileReader? _reader;
    private EchoSampleProvider? _echo;
    private WaveOutEvent? _output;
    private EchoSettings _settings = EchoSettings.Default;
    private bool _bypass;

    // WaveOutEvent counts the bytes it has played; this maps that count back to a time in the file.
    private TimeSpan _seekBase;
    private long _playedAtSeek;

    public event EventHandler? PlaybackEnded;

    public bool IsLoaded => _reader is not null;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    public TimeSpan SourceDuration => _reader?.TotalTime ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get
        {
            if (_output is null)
                return TimeSpan.Zero;

            long played = _output.GetPosition() - _playedAtSeek;
            return _seekBase + TimeSpan.FromSeconds((double)played / _output.OutputWaveFormat.AverageBytesPerSecond);
        }
    }

    public bool Bypass
    {
        get => _bypass;
        set
        {
            _bypass = value;
            _echo?.Bypass = value;
        }
    }

    public EchoSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            _echo?.Settings = value;
        }
    }

    public void Load(string path)
    {
        Unload();
        try
        {
            _reader = new WaveFileReader(path);
            _echo = new EchoSampleProvider(_reader.ToSampleProvider(), _settings, includeTail: true) { Bypass = _bypass };
            _output = new WaveOutEvent { DesiredLatency = 120 };
            _output.Init(_echo);
            _output.PlaybackStopped += OnPlaybackStopped;
            _seekBase = TimeSpan.Zero;
            _playedAtSeek = 0;
        }
        catch (Exception ex) when (ex is not FormatException)
        {
            Unload();
            throw new FormatException($"Could not open {System.IO.Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    public void Unload()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Dispose();
        }

        _reader?.Dispose();
        _output = null;
        _echo = null;
        _reader = null;
    }

    public void Play() => _output?.Play();

    public void Pause() => _output?.Pause();

    public void Stop()
    {
        if (_output is null)
            return;

        _output.PlaybackStopped -= OnPlaybackStopped;
        _output.Stop();
        _output.PlaybackStopped += OnPlaybackStopped;
        Rewind();
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null || _echo is null || _output is null)
            return;

        var target = position < TimeSpan.Zero ? TimeSpan.Zero : position > _reader.TotalTime ? _reader.TotalTime : position;
        lock (_echo.SyncRoot)
        {
            _reader.CurrentTime = target;
            _echo.Reset((long)(target.TotalSeconds * _reader.WaveFormat.SampleRate) * _reader.WaveFormat.Channels);
        }

        _seekBase = target;
        _playedAtSeek = _output.GetPosition();
    }

    public void Dispose() => Unload();

    // WaveOutEvent raises this on the thread that created it, which is the UI thread here.
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Rewind();
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    // After a stop the device position counts from zero again.
    private void Rewind()
    {
        Seek(TimeSpan.Zero);
        _playedAtSeek = 0;
    }
}
