using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimulatorProject.Nodes;
using SimulatorProject.ViewModels;

namespace SimulatorProject;

public partial class MainWindow : Window
{
    private DeviceGroupViewModel? _subscribedGroup;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.SelectedGroup))
                        SubscribeToSelectedGroup(vm.SelectedGroup);
                };
                SubscribeToSelectedGroup(vm.SelectedGroup);
            }
        };
    }

    private void SubscribeToSelectedGroup(DeviceGroupViewModel? group)
    {
        if (_subscribedGroup != null)
            _subscribedGroup.ScenarioEditor.ExecutingBlockChanged -= OnExecutingBlockChanged;

        _subscribedGroup = group;

        if (_subscribedGroup != null)
            _subscribedGroup.ScenarioEditor.ExecutingBlockChanged += OnExecutingBlockChanged;
    }

    // --- 실행 중인 블록으로 자동 스크롤 ---

    private void OnExecutingBlockChanged(int blockIndex)
    {
        if (blockIndex < 0) return;

        Dispatcher.InvokeAsync(() =>
        {
            var container = BlockListControl.ItemContainerGenerator
                .ContainerFromIndex(blockIndex) as FrameworkElement;
            container?.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    // --- 그룹 탭 ---

    private void GroupTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is FrameworkElement el && el.Tag is DeviceGroupViewModel group)
            vm.SelectedGroup = group;
    }

    private async void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Button btn && btn.Tag is DeviceGroupViewModel group)
            await vm.RemoveGroupCommand.ExecuteAsync(group);
    }

    // --- Toolbox drag start ---

    private void PaletteButton_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var btn = (Button)sender;
        DragDrop.DoDragDrop(btn, btn.Tag.ToString()!, DragDropEffects.Copy);
    }

    // --- Block list drop ---

    private void BlockList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void BlockList_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedGroup == null) return;
        var tag = e.Data.GetData(DataFormats.StringFormat) as string;
        var node = CreateNodeFromTag(tag);
        if (node != null)
            vm.SelectedGroup.ScenarioEditor.AddBlock(node);
    }

    // --- Block delete / move ---

    private void DeleteBlock_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedGroup == null) return;
        if (sender is Button btn && btn.Tag is BlockViewModel block)
            vm.SelectedGroup.ScenarioEditor.RemoveBlock(block);
    }

    private void MoveBlockUp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedGroup == null) return;
        if (sender is Button btn && btn.Tag is BlockViewModel block)
        {
            int idx = vm.SelectedGroup.ScenarioEditor.Blocks.IndexOf(block);
            if (idx > 0)
                vm.SelectedGroup.ScenarioEditor.MoveBlock(idx, idx - 1);
        }
    }

    private void MoveBlockDown_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedGroup == null) return;
        if (sender is Button btn && btn.Tag is BlockViewModel block)
        {
            int idx = vm.SelectedGroup.ScenarioEditor.Blocks.IndexOf(block);
            if (idx >= 0 && idx < vm.SelectedGroup.ScenarioEditor.Blocks.Count - 1)
                vm.SelectedGroup.ScenarioEditor.MoveBlock(idx, idx + 1);
        }
    }

    // --- 블록 사이 삽입 ---

    private void InsertPoint_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            e.Effects = DragDropEffects.Copy;
            if (sender is Border border)
                border.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x7C, 0x7C, 0xFF));
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void InsertPoint_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedGroup == null) return;
        if (sender is not Border border) return;

        border.Background = Brushes.Transparent;

        var tag = e.Data.GetData(DataFormats.StringFormat) as string;
        NodeBase? node = CreateNodeFromTag(tag);
        if (node == null) return;

        if (border.Tag is BlockViewModel block)
        {
            int idx = vm.SelectedGroup.ScenarioEditor.Blocks.IndexOf(block);
            vm.SelectedGroup.ScenarioEditor.InsertBlock(idx + 1, node);
        }

        e.Handled = true;
    }

    // --- Help window ---

    private void OpenHelp_Click(object sender, RoutedEventArgs e)
    {
        var win = new SimulatorProject.Help.HelpWindow { Owner = this };
        win.Show();
    }

    // --- Signal input Enter key ---

    private void SignalInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm && vm.SelectedGroup != null)
        {
            vm.SelectedGroup.SendSignalCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static NodeBase? CreateNodeFromTag(string? tag) => tag switch
    {
        "SendSignal"        => new SendSignalNode(),
        "WaitSignal"        => new WaitSignalNode(),
        "Wait"              => new WaitNode(),
        "DeviceStateChange" => new DeviceStateChangeNode(),
        "ConditionBranch"   => new ConditionBranchNode(),
        "Loop"              => new LoopNode(),
        "SetValue"          => new SetValueNode(),
        "Condition"         => new ConditionNode(),
        "WaitCondition"     => new WaitConditionNode(),
        "End"               => new EndNode(),
        _                   => null
    };
}
