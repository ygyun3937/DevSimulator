namespace SimulatorProject.Engine;

public enum PeriodicMode
{
    Toggle,     // 0↔1 토글 (송신)
    Increment,  // 1씩 증가 (송신)
    Fixed,      // 고정값 반복 쓰기 (송신)
    Monitor     // 수신 감시 — 값 변화 없으면 타임아웃
}

public class PeriodicTaskDef
{
    public string DeviceKey { get; set; } = "D0";
    public PeriodicMode Mode { get; set; } = PeriodicMode.Toggle;
    public short FixedValue { get; set; } = 1;
    public int IntervalMs { get; set; } = 1000;

    /// <summary>Monitor 모드 전용: 타임아웃 판정 시간 (ms). 이 시간 동안 값 변화 없으면 타임아웃.</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>Monitor 모드 전용: 타임아웃 시 상태를 기록할 디바이스 키 (빈값이면 가상 상태에 기록)</summary>
    public string StatusKey { get; set; } = "";
}

public class PeriodicTaskRunner : IDisposable
{
    private readonly DeviceMemory _memory;
    private readonly VirtualDeviceState? _virtualState;
    private readonly ExecutionLogger? _logger;
    private readonly List<(PeriodicTaskDef Def, CancellationTokenSource Cts, Task Task)> _running = new();

    public PeriodicTaskRunner(DeviceMemory memory, ExecutionLogger? logger = null,
        VirtualDeviceState? virtualState = null)
    {
        _memory = memory;
        _logger = logger;
        _virtualState = virtualState;
    }

    public void Start(PeriodicTaskDef def)
    {
        var cts = new CancellationTokenSource();
        var task = def.Mode == PeriodicMode.Monitor
            ? RunMonitorAsync(def, cts.Token)
            : RunAsync(def, cts.Token);
        _running.Add((def, cts, task));

        var modeLabel = def.Mode == PeriodicMode.Monitor
            ? $"Monitor, timeout={def.TimeoutMs}ms"
            : $"{def.Mode}";
        _logger?.Log($"[주기 태스크] 시작: {def.DeviceKey} ({modeLabel}, {def.IntervalMs}ms)");
    }

    private async Task RunAsync(PeriodicTaskDef def, CancellationToken ct)
    {
        short counter = 0;
        bool toggleState = false;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(def.IntervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            switch (def.Mode)
            {
                case PeriodicMode.Toggle:
                    toggleState = !toggleState;
                    _memory.SetWord(def.DeviceKey, toggleState ? (short)1 : (short)0);
                    break;

                case PeriodicMode.Increment:
                    counter++;
                    if (counter < 0) counter = 0;
                    _memory.SetWord(def.DeviceKey, counter);
                    break;

                case PeriodicMode.Fixed:
                    _memory.SetWord(def.DeviceKey, def.FixedValue);
                    break;
            }
        }
    }

    /// <summary>
    /// Monitor 모드: 디바이스 값을 주기적으로 읽고, 일정 시간 변화가 없으면 타임아웃 판정.
    /// 결과를 가상 상태(StatusKey 또는 "{DeviceKey}_alive")에 "OK" / "TIMEOUT"으로 기록.
    /// </summary>
    private async Task RunMonitorAsync(PeriodicTaskDef def, CancellationToken ct)
    {
        short lastValue = _memory.GetWord(def.DeviceKey);
        DateTime lastChangedAt = DateTime.UtcNow;
        bool wasAlive = true;

        string statusKey = !string.IsNullOrWhiteSpace(def.StatusKey)
            ? def.StatusKey
            : $"{def.DeviceKey}_alive";

        _virtualState?.Set(statusKey, "WAIT");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(def.IntervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            short currentValue = _memory.GetWord(def.DeviceKey);

            if (currentValue != lastValue)
            {
                lastValue = currentValue;
                lastChangedAt = DateTime.UtcNow;

                if (!wasAlive)
                {
                    wasAlive = true;
                    _virtualState?.Set(statusKey, "OK");
                    _logger?.Log($"[수신 감시] {def.DeviceKey} 신호 복구 (값={currentValue})");
                }
                else if (_virtualState?.Get(statusKey) != "OK")
                {
                    _virtualState?.Set(statusKey, "OK");
                }
            }
            else
            {
                var elapsed = (DateTime.UtcNow - lastChangedAt).TotalMilliseconds;
                if (elapsed >= def.TimeoutMs && wasAlive)
                {
                    wasAlive = false;
                    _virtualState?.Set(statusKey, "TIMEOUT");
                    _logger?.Log($"[수신 감시] {def.DeviceKey} 타임아웃! ({def.TimeoutMs}ms 동안 변화 없음)");
                }
            }
        }
    }

    public void StopAll()
    {
        foreach (var (def, cts, _) in _running)
        {
            cts.Cancel();
            _logger?.Log($"[주기 태스크] 정지: {def.DeviceKey}");
        }
        _running.Clear();
    }

    public void Dispose() => StopAll();
}
