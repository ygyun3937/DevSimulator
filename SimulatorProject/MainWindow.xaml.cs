using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SimulatorProject.Nodes;
using SimulatorProject.ViewModels;

namespace SimulatorProject;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void PaletteButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        DragDrop.DoDragDrop(btn, btn.Tag.ToString()!, DragDropEffects.Copy);
    }

    private void FlowCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void FlowCanvas_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var tag = e.Data.GetData(DataFormats.StringFormat) as string;
        var pos = e.GetPosition(FlowCanvas);

        NodeBase? node = tag switch
        {
            "SetValue"  => new SetValueNode(),
            "Wait"      => new WaitNode(),
            "Condition" => new ConditionNode(),
            "End"       => new EndNode(),
            _           => null
        };

        if (node != null)
        {
            vm.FlowChart.AddNode(node, pos.X, pos.Y);
            RenderFlowChart(vm.FlowChart);
        }
    }

    private void RenderFlowChart(FlowChartViewModel flowVm)
    {
        FlowCanvas.Children.Clear();

        foreach (var conn in flowVm.Connections)
        {
            var fromCenter = new Point(conn.From.X + 70, conn.From.Y + 50);
            var toCenter   = new Point(conn.To.X + 70,   conn.To.Y);

            var line = new Line
            {
                X1 = fromCenter.X, Y1 = fromCenter.Y,
                X2 = toCenter.X,   Y2 = toCenter.Y,
                Stroke = Brushes.Gray, StrokeThickness = 2
            };
            FlowCanvas.Children.Add(line);

            if (!string.IsNullOrEmpty(conn.Label))
            {
                var label = new TextBlock
                {
                    Text = conn.Label,
                    Foreground = Brushes.LightGreen,
                    FontSize = 10
                };
                Canvas.SetLeft(label, (fromCenter.X + toCenter.X) / 2);
                Canvas.SetTop(label,  (fromCenter.Y + toCenter.Y) / 2);
                FlowCanvas.Children.Add(label);
            }
        }

        foreach (var nodeVm in flowVm.Nodes)
        {
            var ctrl = new Views.NodeControl { DataContext = nodeVm };
            Canvas.SetLeft(ctrl, nodeVm.X);
            Canvas.SetTop(ctrl,  nodeVm.Y);
            FlowCanvas.Children.Add(ctrl);
        }
    }

    public void RefreshCanvas()
    {
        if (DataContext is MainViewModel vm)
            RenderFlowChart(vm.FlowChart);
    }
}
