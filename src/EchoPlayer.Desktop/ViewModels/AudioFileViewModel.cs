using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using EchoPlayer.Core;

namespace EchoPlayer.Desktop.ViewModels;

public enum FileStatus
{
    Ready,
    Queued,
    Processing,
    Done,
    Failed,
}

/// <summary>One file in the list, and how far its processing has come.</summary>
public sealed partial class AudioFileViewModel(string path, AudioInfo info) : ObservableObject
{
    public string Path { get; } = path;

    public string Name { get; } = System.IO.Path.GetFileName(path);

    public AudioInfo Info { get; } = info;

    public string Details => $"{Format.Time(Info.Duration)} · {Info.Description}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(IsBusy), nameof(IsDone), nameof(IsFailed))]
    private FileStatus _status;

    [ObservableProperty]
    private double _progress;

    /// <summary>The written file, or why processing failed.</summary>
    [ObservableProperty]
    private string? _message;

    public bool IsReady => Status == FileStatus.Ready;

    public bool IsBusy => Status is FileStatus.Queued or FileStatus.Processing;

    public bool IsDone => Status == FileStatus.Done;

    public bool IsFailed => Status == FileStatus.Failed;

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";

    public static bool IsWav(string path) =>
        string.Equals(System.IO.Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
}
