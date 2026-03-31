using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimulatorProject.ViewModels;

namespace SimulatorProject.Views;

public partial class NodeControl : UserControl
{
    private bool _isDragging;

    public NodeControl() => InitializeComponent();

    private void NodeControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        CaptureMouse();
    }

    private void NodeControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || DataContext is not NodeViewModel vm) return;
        var pos = e.GetPosition(Parent as Canvas);
        vm.X = pos.X - 70;
        vm.Y = pos.Y - 25;
        Canvas.SetLeft(this, vm.X);
        Canvas.SetTop(this, vm.Y);

        // Refresh connection lines
        if (Window.GetWindow(this) is MainWindow win)
            win.RefreshCanvas();
    }

    private void NodeControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }
}
