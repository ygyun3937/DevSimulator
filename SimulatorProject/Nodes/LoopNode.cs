namespace SimulatorProject.Nodes;

public class LoopNode : NodeBase
{
    public override string DisplayName => "반복";

    /// <summary>돌아갈 단계 번호 (1-based). 0이면 시나리오 종료.</summary>
    public int TargetStep { get; set; } = 1;

    /// <summary>반복 횟수. 0이면 무한 반복.</summary>
    public int MaxCount { get; set; } = 0;

    /// <summary>돌아갈 노드 ID (GetGraph에서 TargetStep으로부터 자동 설정)</summary>
    public Guid? TargetNodeId { get; set; }
}
