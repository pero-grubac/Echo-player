using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace EchoPlayer.Desktop.Services;

public interface IDialogService
{
    /// <returns>The chosen files, or an empty list if the user cancelled.</returns>
    IReadOnlyList<string> PickWavFiles();

    /// <returns>The chosen folder, or <see langword="null"/> if the user cancelled.</returns>
    string? PickFolder(string? initialFolder);

    void ShowError(string title, string message);

    void OpenFolder(string folder);
}

public sealed class DialogService : IDialogService
{
    public IReadOnlyList<string> PickWavFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add WAV files",
            Filter = "WAV audio (*.wav)|*.wav|All files (*.*)|*.*",
            Multiselect = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = "Save processed files to", InitialDirectory = initialFolder ?? "" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowError(string title, string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void OpenFolder(string folder) =>
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
}
