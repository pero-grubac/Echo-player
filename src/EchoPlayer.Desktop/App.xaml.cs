using System.Windows;
using EchoPlayer.Desktop.Services;
using EchoPlayer.Desktop.ViewModels;

namespace EchoPlayer.Desktop;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _viewModel = new MainViewModel(new DialogService(), new AudioPreview());

        // Files passed on the command line (or by "Open with") are added on start.
        if (e.Args.Length > 0)
            _viewModel.AddFiles(e.Args);

        new MainWindow(_viewModel).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
