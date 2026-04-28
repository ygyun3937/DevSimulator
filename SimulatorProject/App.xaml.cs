using System.Windows;
using SimulatorProject.ViewModels;

namespace SimulatorProject;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mainVm = new MainViewModel();
        new MainWindow { DataContext = mainVm }.Show();
    }
}
