namespace SimulatorProject.Nodes;

public class SendSignalNode : NodeBase
{
    public override string DisplayName => "신호 전송";
    public string Message { get; set; } = "";
}
