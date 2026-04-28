using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SimulatorProject.ViewModels;

namespace SimulatorProject.Views;

public partial class NodeControl : UserControl
{
    private Storyboard? _pulseStoryboard;

    public NodeControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;

        if (e.NewValue is NodeViewModel vm)
            UpdateAnimation(vm.IsExecuting);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.IsExecuting) && sender is NodeViewModel vm)
            UpdateAnimation(vm.IsExecuting);
    }

    private void UpdateAnimation(bool isExecuting)
    {
        if (isExecuting)
            StartPulse();
        else
            StopPulse();
    }

    private void StartPulse()
    {
        GlowBorder.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));

        var opacityAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(600),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };

        var borderOpacityAnim = new DoubleAnimation
        {
            From = 0.7,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(600),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };

        _pulseStoryboard = new Storyboard();

        Storyboard.SetTarget(opacityAnim, GlowBorder);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));

        Storyboard.SetTarget(borderOpacityAnim, MainBorder);
        Storyboard.SetTargetProperty(borderOpacityAnim, new PropertyPath(OpacityProperty));

        _pulseStoryboard.Children.Add(opacityAnim);
        _pulseStoryboard.Children.Add(borderOpacityAnim);
        _pulseStoryboard.Begin();
    }

    private void StopPulse()
    {
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
        GlowBorder.Opacity = 0;
        GlowBorder.Background = Brushes.Transparent;
        MainBorder.Opacity = 1.0;
    }
}
