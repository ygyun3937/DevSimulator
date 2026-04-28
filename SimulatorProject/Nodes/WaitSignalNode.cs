namespace SimulatorProject.Nodes;

public class WaitSignalNode : NodeBase
{
    public override string DisplayName => "신호 수신 대기";
    public string ExpectedSignal { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
}
