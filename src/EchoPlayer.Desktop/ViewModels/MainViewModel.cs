using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoPlayer.Core;
using EchoPlayer.Desktop.Services;

namespace EchoPlayer.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int WaveformBuckets = 900;

    private readonly IDialogService _dialogs;
    private readonly IAudioPreview _preview;
    private readonly DispatcherTimer _clock;
    private CancellationTokenSource? _waveformCancellation;

    public MainViewModel(IDialogService dialogs, IAudioPreview preview)
    {
        _dialogs = dialogs;
        _preview = preview;
        _preview.Settings = Settings;
        _preview.PlaybackEnded += (_, _) => UpdatePlayback();
        _clock = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Render, (_, _) => UpdatePlayback(), Dispatcher.CurrentDispatcher);
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            ProcessAllCommand.NotifyCanExecuteChanged();
            ClearFilesCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<AudioFileViewModel> Files { get; } = [];

    public IReadOnlyList<EchoPreset> Presets => EchoPreset.All;

    public bool HasFiles => Files.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand), nameof(StopCommand))]
    private AudioFileViewModel? _selectedFile;

    public bool HasSelection => SelectedFile is not null;

    // ---- Echo settings ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DelayText))]
    private double _delayMs = EchoSettings.Default.DelayMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FeedbackText))]
    private double _feedback = EchoSettings.Default.Feedback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MixText))]
    private double _mix = EchoSettings.Default.Mix;

    public double MinDelayMs => EchoSettings.MinDelayMs;
    public double MaxDelayMs => EchoSettings.MaxDelayMs;
    public double MaxFeedback => EchoSettings.MaxFeedback;

    public string DelayText => $"{DelayMs:0} ms";
    public string FeedbackText => Format.Percent(Feedback);
    public string MixText => Format.Percent(Mix);

    public EchoSettings Settings => new(Math.Round(DelayMs), Math.Round(Feedback, 2), Math.Round(Mix, 2));

    public string EchoSummary
    {
        get
        {
            int repeats = Settings.AudibleRepeats();
            string tail = Format.Seconds(TimeSpan.FromSeconds((double)Settings.TailFrames(48000) / 48000));
            return repeats switch
            {
                0 => "The echo is silent.",
                1 => $"1 audible repeat · tail {tail}",
                _ => $"{repeats} audible repeats · tail {tail}",
            };
        }
    }

    /// <summary>The preset that matches the current settings, if any.</summary>
    public EchoPreset? ActivePreset => Presets.FirstOrDefault(p => p.Settings == Settings);

    partial void OnDelayMsChanged(double value) => OnSettingsChanged();
    partial void OnFeedbackChanged(double value) => OnSettingsChanged();
    partial void OnMixChanged(double value) => OnSettingsChanged();

    [RelayCommand]
    private void ApplyPreset(EchoPreset preset)
    {
        DelayMs = preset.Settings.DelayMs;
        Feedback = preset.Settings.Feedback;
        Mix = preset.Settings.Mix;
    }

    private void OnSettingsChanged()
    {
        _preview.Settings = Settings;
        OnPropertyChanged(nameof(EchoSummary));
        OnPropertyChanged(nameof(ActivePreset));
        UpdateTimeline();
        _ = RefreshEchoWaveformAsync(debounce: true);
    }

    // ---- Preview ----

    /// <summary>Hear the echo (true) or the original (false).</summary>
    [ObservableProperty]
    private bool _isEchoOn = true;

    partial void OnIsEchoOnChanged(bool value) => _preview.Bypass = !value;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private Waveform _originalWaveform = Waveform.Empty;

    [ObservableProperty]
    private Waveform _echoWaveform = Waveform.Empty;

    /// <summary>Length of the timeline: the file plus the echo tail.</summary>
    [ObservableProperty]
    private TimeSpan _timelineDuration;

    /// <summary>Where the original ends on the timeline (0–1).</summary>
    [ObservableProperty]
    private double _originalFraction = 1;

    [ObservableProperty]
    private double _playheadFraction;

    [ObservableProperty]
    private string _positionText = Format.Time(TimeSpan.Zero);

    public string DurationText => Format.Time(TimelineDuration);

    partial void OnTimelineDurationChanged(TimeSpan value) => OnPropertyChanged(nameof(DurationText));

    async partial void OnSelectedFileChanged(AudioFileViewModel? value)
    {
        _preview.Stop();
        _preview.Unload();
        OriginalWaveform = Waveform.Empty;
        EchoWaveform = Waveform.Empty;
        UpdatePlayback();

        if (value is null)
            return;

        try
        {
            _preview.Load(value.Path);
        }
        catch (FormatException ex)
        {
            _dialogs.ShowError("Could not play the file", ex.Message);
            return;
        }

        UpdateTimeline();
        var original = await Task.Run(() => Waveform.FromFile(value.Path, WaveformBuckets));
        if (SelectedFile == value)
            OriginalWaveform = original;

        await RefreshEchoWaveformAsync(debounce: false);
    }

    private bool CanControlPlayback() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanControlPlayback))]
    private void PlayPause()
    {
        if (_preview.IsPlaying)
            _preview.Pause();
        else
            _preview.Play();
        UpdatePlayback();
    }

    [RelayCommand(CanExecute = nameof(CanControlPlayback))]
    private void Stop()
    {
        _preview.Stop();
        UpdatePlayback();
    }

    /// <summary>Jumps to a point on the timeline, 0 to 1.</summary>
    [RelayCommand]
    private void Seek(double fraction)
    {
        if (!_preview.IsLoaded)
            return;

        _preview.Seek(TimelineDuration * Math.Clamp(fraction, 0, 1));
        UpdatePlayback();
    }

    private void UpdateTimeline()
    {
        var source = _preview.SourceDuration;
        TimelineDuration = source + TimeSpan.FromSeconds(SelectedFile is null ? 0
            : (double)Settings.TailFrames(SelectedFile.Info.SampleRate) / SelectedFile.Info.SampleRate);
        OriginalFraction = TimelineDuration > TimeSpan.Zero ? source / TimelineDuration : 1;
    }

    private void UpdatePlayback()
    {
        IsPlaying = _preview.IsPlaying;
        if (IsPlaying)
            _clock.Start();
        else
            _clock.Stop();

        var position = _preview.Position;
        if (position > TimelineDuration)
            position = TimelineDuration;
        PositionText = Format.Time(position);
        PlayheadFraction = TimelineDuration > TimeSpan.Zero ? position / TimelineDuration : 0;
    }

    private async Task RefreshEchoWaveformAsync(bool debounce)
    {
        _waveformCancellation?.Cancel();
        if (SelectedFile is not { } file)
            return;

        var cancellation = _waveformCancellation = new CancellationTokenSource();
        var settings = Settings;
        try
        {
            if (debounce)
                await Task.Delay(120, cancellation.Token);

            var waveform = await Task.Run(() => Waveform.FromFileWithEcho(file.Path, settings, WaveformBuckets, cancellation.Token), cancellation.Token);
            if (!cancellation.IsCancellationRequested && SelectedFile == file)
                EchoWaveform = waveform;
        }
        catch (OperationCanceledException)
        {
            // A newer setting or file replaced this one.
        }
    }

    // ---- Files ----

    [RelayCommand]
    private void AddFiles() => AddFiles(_dialogs.PickWavFiles());

    /// <summary>Adds WAV files, skipping duplicates and reporting the ones that cannot be read.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        var problems = new List<string>();
        foreach (var path in paths)
        {
            if (Files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (!AudioFileViewModel.IsWav(path))
            {
                problems.Add($"{Path.GetFileName(path)} is not a .wav file.");
                continue;
            }

            try
            {
                Files.Add(new AudioFileViewModel(path, AudioInfo.Read(path)));
            }
            catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
            {
                problems.Add(ex.Message);
            }
        }

        SelectedFile ??= Files.FirstOrDefault();

        if (problems.Count > 0)
            _dialogs.ShowError("Some files were skipped", string.Join(Environment.NewLine, problems));
    }

    [RelayCommand]
    private void RemoveFile(AudioFileViewModel file)
    {
        if (file.IsBusy)
            return;

        int index = Files.IndexOf(file);
        Files.Remove(file);
        if (SelectedFile == file || SelectedFile is null)
            SelectedFile = Files.Count == 0 ? null : Files[Math.Min(index, Files.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(CanClearFiles))]
    private void ClearFiles()
    {
        SelectedFile = null;
        Files.Clear();
        Summary = null;
    }

    private bool CanClearFiles() => HasFiles && !ProcessAllCommand.IsRunning;

    // ---- Output ----

    /// <summary>Where processed files go; <see langword="null"/> means next to each original.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputFolderText), nameof(HasCustomOutputFolder))]
    private string? _outputFolder;

    public string OutputFolderText => OutputFolder ?? "Next to each original file";

    public bool HasCustomOutputFolder => OutputFolder is not null;

    [RelayCommand]
    private void ChooseOutputFolder()
    {
        if (_dialogs.PickFolder(OutputFolder ?? SelectedFile?.Folder) is { } folder)
            OutputFolder = folder;
    }

    [RelayCommand]
    private void ResetOutputFolder() => OutputFolder = null;

    /// <summary>Result of the last run, for example "3 files saved".</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    private string? _summary;

    private string? _lastOutputFolder;

    [RelayCommand(CanExecute = nameof(CanOpenOutputFolder))]
    private void OpenOutputFolder()
    {
        if (_lastOutputFolder is not null)
            _dialogs.OpenFolder(_lastOutputFolder);
    }

    private bool CanOpenOutputFolder() => _lastOutputFolder is not null && Summary is not null;

    [RelayCommand(CanExecute = nameof(HasFiles), IncludeCancelCommand = true)]
    private async Task ProcessAllAsync(CancellationToken cancellationToken)
    {
        ClearFilesCommand.NotifyCanExecuteChanged();
        Summary = null;

        var settings = Settings;
        var files = Files.ToList();
        foreach (var file in files)
        {
            file.Status = FileStatus.Queued;
            file.Progress = 0;
            file.Message = null;
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            CancellationToken = cancellationToken,
        };

        try
        {
            await Parallel.ForEachAsync(files, options, async (file, token) =>
            {
                string folder = OutputFolder ?? file.Folder;
                string output = EchoProcessor.OutputPathFor(file.Path, folder);
                var progress = new Progress<double>(p => file.Progress = p);

                file.Status = FileStatus.Processing;
                try
                {
                    long clipped = await Task.Run(() => EchoProcessor.ProcessFile(file.Path, output, settings, progress, token), token);
                    file.Status = FileStatus.Done;
                    file.Message = clipped > 0
                        ? $"Saved as {Path.GetFileName(output)} · {clipped:N0} samples clipped, try a lower mix"
                        : $"Saved as {Path.GetFileName(output)}";
                    _lastOutputFolder = folder;
                }
                catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
                {
                    file.Status = FileStatus.Failed;
                    file.Message = ex.Message;
                }
            });
        }
        catch (OperationCanceledException)
        {
            foreach (var file in files.Where(f => f.IsBusy))
            {
                file.Status = FileStatus.Ready;
                file.Progress = 0;
            }
        }
        finally
        {
            ClearFilesCommand.NotifyCanExecuteChanged();
        }

        int done = files.Count(f => f.IsDone);
        int failed = files.Count(f => f.IsFailed);
        Summary = cancellationToken.IsCancellationRequested ? $"Stopped. {done} of {files.Count} files saved."
            : failed > 0 ? $"{done} saved, {failed} failed."
            : done == 1 ? "1 file saved." : $"{done} files saved.";
    }

    public void Dispose()
    {
        _clock.Stop();
        _waveformCancellation?.Cancel();
        _preview.Dispose();
    }
}
