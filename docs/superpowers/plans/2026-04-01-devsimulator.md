# DevSimulator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WPF/C# 기반 범용 통신 시뮬레이터 — SLMP(MC Protocol)로 미쯔비시 Q 시리즈 PLC를 모사하고, 플로우차트 블록코딩으로 응답 로직과 시나리오를 정의한다.

**Architecture:** 4개 레이어 (UI → SimulatorEngine → ProtocolAdapter → TcpServer). 프로토콜은 `IProtocolAdapter` 인터페이스를 통해 플러그인 방식으로 교체 가능. UI는 MVVM (CommunityToolkit.Mvvm).

**Tech Stack:** C# 12, .NET 8, WPF, CommunityToolkit.Mvvm, System.Text.Json, xUnit, FluentAssertions

---

## 파일 구조

```
SimulatorProject.sln
├── SimulatorProject/                          (WPF App)
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── Engine/
│   │   ├── DeviceMemory.cs                   # 스레드 안전 레지스터 저장소
│   │   ├── FlowExecutor.cs                   # 플로우차트 실행 엔진
│   │   └── ScenarioManager.cs                # JSON 저장/불러오기
│   ├── Nodes/
│   │   ├── NodeBase.cs                       # 추상 기반 클래스
│   │   ├── SetValueNode.cs                   # 레지스터 값 설정
│   │   ├── WaitNode.cs                       # ms 단위 대기
│   │   ├── ConditionNode.cs                  # 조건 분기 (YES/NO)
│   │   └── EndNode.cs                        # 플로우 종료
│   ├── Protocol/
│   │   ├── IProtocolAdapter.cs               # 프로토콜 인터페이스
│   │   ├── SlmpAdapter.cs                    # SLMP 3E 프레임 파싱/직렬화
│   │   └── TcpServer.cs                      # 비동기 TCP 리스너
│   ├── ViewModels/
│   │   ├── MainViewModel.cs                  # 시작/정지/상태
│   │   ├── FlowChartViewModel.cs             # 노드 목록 + 연결선
│   │   ├── NodeViewModel.cs                  # 개별 노드 (위치, 속성)
│   │   ├── ConnectionViewModel.cs            # 노드 간 연결선
│   │   └── DeviceMonitorViewModel.cs         # 실시간 레지스터 표시
│   └── Views/
│       ├── FlowChartEditorView.xaml / .cs    # 노드 캔버스 편집기
│       └── DeviceMonitorView.xaml / .cs      # 실시간 모니터 패널
└── SimulatorProject.Tests/                    (xUnit)
    ├── DeviceMemoryTests.cs
    ├── SlmpAdapterTests.cs
    ├── FlowExecutorTests.cs
    └── ScenarioManagerTests.cs
```

---

## Task 1: 솔루션 및 프로젝트 설정

**Files:**
- Create: `SimulatorProject.sln`
- Create: `SimulatorProject/SimulatorProject.csproj`
- Create: `SimulatorProject.Tests/SimulatorProject.Tests.csproj`

- [ ] **Step 1: 솔루션 생성**

```bash
cd /path/to/simulator-project
dotnet new sln -n SimulatorProject
dotnet new wpf -n SimulatorProject -f net8.0-windows
dotnet new xunit -n SimulatorProject.Tests -f net8.0
dotnet sln add SimulatorProject/SimulatorProject.csproj
dotnet sln add SimulatorProject.Tests/SimulatorProject.Tests.csproj
```

- [ ] **Step 2: 패키지 추가**

```bash
cd SimulatorProject
dotnet add package CommunityToolkit.Mvvm

cd ../SimulatorProject.Tests
dotnet add package FluentAssertions
dotnet add reference ../SimulatorProject/SimulatorProject.csproj
```

- [ ] **Step 3: SimulatorProject.csproj 확인 — UseWPF 활성화**

`SimulatorProject/SimulatorProject.csproj` 파일을 열어 아래와 같은지 확인:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: 빌드 확인**

```bash
cd ..
dotnet build SimulatorProject.sln
```

Expected: `Build succeeded.`

- [ ] **Step 5: 디렉터리 구조 생성**

```bash
mkdir -p SimulatorProject/Engine
mkdir -p SimulatorProject/Nodes
mkdir -p SimulatorProject/Protocol
mkdir -p SimulatorProject/ViewModels
mkdir -p SimulatorProject/Views
```

- [ ] **Step 6: 커밋**

```bash
git init
git add .
git commit -m "chore: initial solution setup"
```

---

## Task 2: DeviceMemory — 스레드 안전 레지스터 저장소

**Files:**
- Create: `SimulatorProject/Engine/DeviceMemory.cs`
- Create: `SimulatorProject.Tests/DeviceMemoryTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`SimulatorProject.Tests/DeviceMemoryTests.cs`:

```csharp
using FluentAssertions;
using SimulatorProject.Engine;

namespace SimulatorProject.Tests;

public class DeviceMemoryTests
{
    [Fact]
    public void SetAndGet_Word_ReturnsValue()
    {
        var mem = new DeviceMemory();
        mem.SetWord("D100", 1234);
        mem.GetWord("D100").Should().Be(1234);
    }

    [Fact]
    public void GetWord_Uninitialized_ReturnsZero()
    {
        var mem = new DeviceMemory();
        mem.GetWord("D999").Should().Be(0);
    }

    [Fact]
    public void SetAndGet_Bit_ReturnsValue()
    {
        var mem = new DeviceMemory();
        mem.SetBit("M0", true);
        mem.GetBit("M0").Should().BeTrue();
    }

    [Fact]
    public void GetBit_Uninitialized_ReturnsFalse()
    {
        var mem = new DeviceMemory();
        mem.GetBit("M99").Should().BeFalse();
    }

    [Fact]
    public void SetWord_RaisesChanged_Event()
    {
        var mem = new DeviceMemory();
        string? changedKey = null;
        mem.ValueChanged += (key, _) => changedKey = key;

        mem.SetWord("D10", 42);

        changedKey.Should().Be("D10");
    }

    [Fact]
    public void ConcurrentWrites_DoNotThrow()
    {
        var mem = new DeviceMemory();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => mem.SetWord($"D{i}", i)))
            .ToArray();
        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: 테스트 실행 — FAIL 확인**

```bash
dotnet test SimulatorProject.Tests --filter "DeviceMemoryTests" -v
```

Expected: FAIL — `DeviceMemory` not found.

- [ ] **Step 3: DeviceMemory 구현**

`SimulatorProject/Engine/DeviceMemory.cs`:

```csharp
using System.Collections.Concurrent;

namespace SimulatorProject.Engine;

public class DeviceMemory
{
    private readonly ConcurrentDictionary<string, short> _words = new();
    private readonly ConcurrentDictionary<string, bool> _bits = new();

    public event Action<string, object>? ValueChanged;

    public void SetWord(string key, short value)
    {
        _words[key] = value;
        ValueChanged?.Invoke(key, value);
    }

    public short GetWord(string key) =>
        _words.TryGetValue(key, out var v) ? v : (short)0;

    public void SetBit(string key, bool value)
    {
        _bits[key] = value;
        ValueChanged?.Invoke(key, value);
    }

    public bool GetBit(string key) =>
        _bits.TryGetValue(key, out var v) && v;

    public IReadOnlyDictionary<string, short> GetAllWords() => _words;
    public IReadOnlyDictionary<string, bool> GetAllBits() => _bits;

    public void Clear()
    {
        _words.Clear();
        _bits.Clear();
    }
}
```

- [ ] **Step 4: 테스트 실행 — PASS 확인**

```bash
dotnet test SimulatorProject.Tests --filter "DeviceMemoryTests" -v
```

Expected: All 6 tests PASS.

- [ ] **Step 5: 커밋**

```bash
git add SimulatorProject/Engine/DeviceMemory.cs SimulatorProject.Tests/DeviceMemoryTests.cs
git commit -m "feat: add DeviceMemory with thread-safe register storage"
```

---

## Task 3: IProtocolAdapter + TcpServer

**Files:**
- Create: `SimulatorProject/Protocol/IProtocolAdapter.cs`
- Create: `SimulatorProject/Protocol/TcpServer.cs`

- [ ] **Step 1: IProtocolAdapter 인터페이스 작성**

`SimulatorProject/Protocol/IProtocolAdapter.cs`:

```csharp
using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public interface IProtocolAdapter
{
    string Name { get; }
    int DefaultPort { get; }

    /// <summary>
    /// 클라이언트 요청 바이트를 받아 DeviceMemory를 읽고/쓰고 응답 바이트를 반환한다.
    /// 파싱 실패 시 null 반환 (연결 유지).
    /// </summary>
    Task<byte[]?> HandleRequestAsync(byte[] request, DeviceMemory memory);
}
```

- [ ] **Step 2: TcpServer 구현**

`SimulatorProject/Protocol/TcpServer.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public class TcpServer : IAsyncDisposable
{
    private readonly IProtocolAdapter _adapter;
    private readonly DeviceMemory _memory;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<Task> _clientTasks = new();

    public int ConnectedClients { get; private set; }
    public event Action<int>? ClientCountChanged;
    public event Action<string>? LogMessage;

    public TcpServer(IProtocolAdapter adapter, DeviceMemory memory)
    {
        _adapter = adapter;
        _memory = memory;
    }

    public async Task StartAsync(string ip, int port)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Parse(ip), port);
        _listener.Start();
        LogMessage?.Invoke($"[TcpServer] Listening on {ip}:{port} ({_adapter.Name})");

        await AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                var task = HandleClientAsync(client, ct);
                _clientTasks.Add(task);
                ConnectedClients++;
                ClientCountChanged?.Invoke(ConnectedClients);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[TcpServer] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, ct);
                    if (read == 0) break;

                    var request = buffer[..read];
                    var response = await _adapter.HandleRequestAsync(request, _memory);
                    if (response != null)
                        await stream.WriteAsync(response, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[TcpServer] Client error: {ex.Message}");
            }
            finally
            {
                ConnectedClients--;
                ClientCountChanged?.Invoke(ConnectedClients);
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        await Task.WhenAll(_clientTasks);
        _clientTasks.Clear();
        LogMessage?.Invoke("[TcpServer] Stopped.");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
```

- [ ] **Step 3: 빌드 확인**

```bash
dotnet build SimulatorProject/SimulatorProject.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: 커밋**

```bash
git add SimulatorProject/Protocol/
git commit -m "feat: add IProtocolAdapter interface and async TcpServer"
```

---

## Task 4: SlmpAdapter — 디바이스 읽기 (Command 0x0401)

**Files:**
- Create: `SimulatorProject/Protocol/SlmpAdapter.cs`
- Create: `SimulatorProject.Tests/SlmpAdapterTests.cs`

SLMP 3E 프레임 구조 (요청):
```
[50 00]          - Subheader
[00]             - Network No
[FF]             - PC No
[FF 03]          - I/O No (little-endian)
[00]             - Station No
[XX XX]          - Request data length (little-endian, CPU timer ~ end)
[10 00]          - CPU monitor timer
[01 04]          - Command 0x0401 (읽기, little-endian)
[00 00]          - Sub command
[XX XX XX]       - Starting device number (3 bytes, little-endian)
[XX]             - Device code (ASCII: 'D'=0x44, 'M'=0x4D, 'Y'=0x59, 'X'=0x58)
[XX XX]          - Number of points (little-endian)
```

SLMP 3E 프레임 구조 (응답):
```
[D0 00]          - Subheader
[00] [FF] [FF 03] [00]  - Network, PC, I/O, Station (echo)
[XX XX]          - Response data length
[00 00]          - End code (0x0000 = success)
[XX XX ...]      - Data (2 bytes per word register)
```

- [ ] **Step 1: 실패하는 테스트 작성**

`SimulatorProject.Tests/SlmpAdapterTests.cs`:

```csharp
using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;

namespace SimulatorProject.Tests;

public class SlmpAdapterTests
{
    private static byte[] BuildReadRequest(int deviceNo, char deviceCode, ushort points)
    {
        // Command 0x0401, SubCommand 0x0000
        var data = new List<byte>
        {
            0x10, 0x00,                          // CPU monitor timer
            0x01, 0x04,                          // Command 0x0401 (little-endian)
            0x00, 0x00,                          // SubCommand
            (byte)(deviceNo & 0xFF),             // Device No (3 bytes LE)
            (byte)((deviceNo >> 8) & 0xFF),
            (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,                    // Device code ASCII
            (byte)(points & 0xFF),               // No. of points LE
            (byte)((points >> 8) & 0xFF),
        };

        ushort dataLen = (ushort)data.Count;
        var frame = new List<byte>
        {
            0x50, 0x00,       // Subheader
            0x00,             // Network No
            0xFF,             // PC No
            0xFF, 0x03,       // I/O No
            0x00,             // Station No
            (byte)(dataLen & 0xFF), (byte)((dataLen >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }

    [Fact]
    public async Task Read_DRegister_ReturnsParsedValue()
    {
        var mem = new DeviceMemory();
        mem.SetWord("D100", 1234);
        var adapter = new SlmpAdapter();

        var request = BuildReadRequest(100, 'D', 1);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        // Response subheader: D0 00
        response![0].Should().Be(0xD0);
        response[1].Should().Be(0x00);
        // End code at offset 9: 00 00
        response[9].Should().Be(0x00);
        response[10].Should().Be(0x00);
        // Data at offset 11: 1234 = 0x04D2 (LE)
        short value = (short)(response[11] | (response[12] << 8));
        value.Should().Be(1234);
    }

    [Fact]
    public async Task Read_MultipleRegisters_ReturnsAllValues()
    {
        var mem = new DeviceMemory();
        mem.SetWord("D0", 10);
        mem.SetWord("D1", 20);
        mem.SetWord("D2", 30);
        var adapter = new SlmpAdapter();

        var request = BuildReadRequest(0, 'D', 3);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        short v0 = (short)(response![11] | (response[12] << 8));
        short v1 = (short)(response[13] | (response[14] << 8));
        short v2 = (short)(response[15] | (response[16] << 8));
        v0.Should().Be(10);
        v1.Should().Be(20);
        v2.Should().Be(30);
    }

    [Fact]
    public async Task InvalidFrame_ReturnsNull()
    {
        var adapter = new SlmpAdapter();
        var mem = new DeviceMemory();
        var response = await adapter.HandleRequestAsync(new byte[] { 0x00, 0x01 }, mem);
        response.Should().BeNull();
    }
}
```

- [ ] **Step 2: 테스트 실행 — FAIL 확인**

```bash
dotnet test SimulatorProject.Tests --filter "SlmpAdapterTests" -v
```

Expected: FAIL — `SlmpAdapter` not found.

- [ ] **Step 3: SlmpAdapter 구현 (읽기만)**

`SimulatorProject/Protocol/SlmpAdapter.cs`:

```csharp
using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public class SlmpAdapter : IProtocolAdapter
{
    public string Name => "SLMP (Q Series)";
    public int DefaultPort => 5000;

    private const ushort CMD_READ  = 0x0401;
    private const ushort CMD_WRITE = 0x1401;

    public async Task<byte[]?> HandleRequestAsync(byte[] request, DeviceMemory memory)
    {
        await Task.CompletedTask; // 동기 처리지만 인터페이스 맞춤
        try
        {
            return ParseAndRespond(request, memory);
        }
        catch
        {
            return null;
        }
    }

    private byte[]? ParseAndRespond(byte[] req, DeviceMemory memory)
    {
        // 최소 프레임 크기: 헤더 9 + CPU timer 2 + command 2 + subcmd 2 + device 3 + code 1 + points 2 = 21
        if (req.Length < 21) return null;
        if (req[0] != 0x50 || req[1] != 0x00) return null;

        int offset = 9; // CPU monitor timer 시작
        // offset+0,1: CPU timer (skip)
        ushort command    = (ushort)(req[offset + 2] | (req[offset + 3] << 8));
        // ushort subCommand = (ushort)(req[offset + 4] | (req[offset + 5] << 8)); // 현재 미사용
        int deviceNo      = req[offset + 6] | (req[offset + 7] << 8) | (req[offset + 8] << 16);
        char deviceCode   = (char)req[offset + 9];
        ushort points     = (ushort)(req[offset + 10] | (req[offset + 11] << 8));

        return command switch
        {
            CMD_READ  => BuildReadResponse(req, memory, deviceNo, deviceCode, points),
            CMD_WRITE => BuildWriteResponse(req, memory, deviceNo, deviceCode, points, offset + 12),
            _         => null
        };
    }

    private byte[] BuildReadResponse(byte[] req, DeviceMemory memory,
        int startNo, char code, ushort points)
    {
        var data = new List<byte>();
        for (int i = 0; i < points; i++)
        {
            string key = $"{code}{startNo + i}";
            short val = memory.GetWord(key);
            data.Add((byte)(val & 0xFF));
            data.Add((byte)((val >> 8) & 0xFF));
        }

        return BuildResponseFrame(req, 0x0000, data.ToArray());
    }

    private byte[] BuildWriteResponse(byte[] req, DeviceMemory memory,
        int startNo, char code, ushort points, int dataOffset)
    {
        if (req.Length < dataOffset + points * 2) return BuildResponseFrame(req, 0x0055, Array.Empty<byte>());

        for (int i = 0; i < points; i++)
        {
            string key = $"{code}{startNo + i}";
            short val = (short)(req[dataOffset + i * 2] | (req[dataOffset + i * 2 + 1] << 8));
            memory.SetWord(key, val);
        }

        return BuildResponseFrame(req, 0x0000, Array.Empty<byte>());
    }

    private static byte[] BuildResponseFrame(byte[] req, ushort endCode, byte[] data)
    {
        // Response data length = endCode(2) + data
        ushort responseDataLen = (ushort)(2 + data.Length);

        var frame = new List<byte>
        {
            0xD0, 0x00,       // Subheader
            req[2],           // Network No (echo)
            req[3],           // PC No (echo)
            req[4], req[5],   // I/O No (echo)
            req[6],           // Station No (echo)
            (byte)(responseDataLen & 0xFF),
            (byte)((responseDataLen >> 8) & 0xFF),
            (byte)(endCode & 0xFF),
            (byte)((endCode >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }
}
```

- [ ] **Step 4: 테스트 실행 — PASS 확인**

```bash
dotnet test SimulatorProject.Tests --filter "SlmpAdapterTests" -v
```

Expected: All 3 tests PASS.

- [ ] **Step 5: 커밋**

```bash
git add SimulatorProject/Protocol/SlmpAdapter.cs SimulatorProject.Tests/SlmpAdapterTests.cs
git commit -m "feat: add SlmpAdapter with device read/write (SLMP 3E frame)"
```

---

## Task 5: 노드 모델 클래스

**Files:**
- Create: `SimulatorProject/Nodes/NodeBase.cs`
- Create: `SimulatorProject/Nodes/SetValueNode.cs`
- Create: `SimulatorProject/Nodes/WaitNode.cs`
- Create: `SimulatorProject/Nodes/ConditionNode.cs`
- Create: `SimulatorProject/Nodes/EndNode.cs`

- [ ] **Step 1: NodeBase 작성**

`SimulatorProject/Nodes/NodeBase.cs`:

```csharp
using System.Text.Json.Serialization;

namespace SimulatorProject.Nodes;

[JsonDerivedType(typeof(SetValueNode), "SetValue")]
[JsonDerivedType(typeof(WaitNode), "Wait")]
[JsonDerivedType(typeof(ConditionNode), "Condition")]
[JsonDerivedType(typeof(EndNode), "End")]
public abstract class NodeBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double X { get; set; }
    public double Y { get; set; }
    public abstract string DisplayName { get; }

    // 다음 노드 ID (ConditionNode는 오버라이드)
    public Guid? NextNodeId { get; set; }
}
```

- [ ] **Step 2: SetValueNode 작성**

`SimulatorProject/Nodes/SetValueNode.cs`:

```csharp
namespace SimulatorProject.Nodes;

public class SetValueNode : NodeBase
{
    public override string DisplayName => "Set Value";
    public string DeviceKey { get; set; } = "D0";
    public short Value { get; set; }
}
```

- [ ] **Step 3: WaitNode 작성**

`SimulatorProject/Nodes/WaitNode.cs`:

```csharp
namespace SimulatorProject.Nodes;

public class WaitNode : NodeBase
{
    public override string DisplayName => "Wait";
    public int DelayMs { get; set; } = 1000;
}
```

- [ ] **Step 4: ConditionNode 작성**

`SimulatorProject/Nodes/ConditionNode.cs`:

```csharp
namespace SimulatorProject.Nodes;

public enum ConditionOperator { Equal, NotEqual, GreaterThan, LessThan }

public class ConditionNode : NodeBase
{
    public override string DisplayName => "Condition";
    public string DeviceKey { get; set; } = "M0";
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equal;
    public short CompareValue { get; set; }
    public Guid? YesNodeId { get; set; }
    public Guid? NoNodeId { get; set; }
}
```

- [ ] **Step 5: EndNode 작성**

`SimulatorProject/Nodes/EndNode.cs`:

```csharp
namespace SimulatorProject.Nodes;

public class EndNode : NodeBase
{
    public override string DisplayName => "End";
}
```

- [ ] **Step 6: 빌드 확인**

```bash
dotnet build SimulatorProject/SimulatorProject.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 7: 커밋**

```bash
git add SimulatorProject/Nodes/
git commit -m "feat: add flowchart node model classes"
```

---

## Task 6: FlowExecutor — 플로우차트 실행 엔진

**Files:**
- Create: `SimulatorProject/Engine/FlowExecutor.cs`
- Create: `SimulatorProject.Tests/FlowExecutorTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`SimulatorProject.Tests/FlowExecutorTests.cs`:

```csharp
using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Nodes;

namespace SimulatorProject.Tests;

public class FlowExecutorTests
{
    private static Dictionary<Guid, NodeBase> MakeGraph(params NodeBase[] nodes)
        => nodes.ToDictionary(n => n.Id);

    [Fact]
    public async Task Execute_SetValueNode_SetsRegister()
    {
        var mem = new DeviceMemory();
        var setNode = new SetValueNode { DeviceKey = "D100", Value = 99 };
        var endNode = new EndNode();
        setNode.NextNodeId = endNode.Id;

        var graph = MakeGraph(setNode, endNode);
        var executor = new FlowExecutor(graph, mem);

        await executor.RunFromAsync(setNode.Id, CancellationToken.None);

        mem.GetWord("D100").Should().Be(99);
    }

    [Fact]
    public async Task Execute_ConditionNode_TakeYesBranch_WhenTrue()
    {
        var mem = new DeviceMemory();
        mem.SetBit("M0", true);

        var cond = new ConditionNode
        {
            DeviceKey = "M0",
            Operator = ConditionOperator.Equal,
            CompareValue = 1
        };
        var yesSet = new SetValueNode { DeviceKey = "D0", Value = 1 };
        var noSet  = new SetValueNode { DeviceKey = "D0", Value = 2 };
        var end    = new EndNode();

        cond.YesNodeId = yesSet.Id;
        cond.NoNodeId  = noSet.Id;
        yesSet.NextNodeId = end.Id;
        noSet.NextNodeId  = end.Id;

        var graph = MakeGraph(cond, yesSet, noSet, end);
        var executor = new FlowExecutor(graph, mem);
        await executor.RunFromAsync(cond.Id, CancellationToken.None);

        mem.GetWord("D0").Should().Be(1);
    }

    [Fact]
    public async Task Execute_WaitNode_DelaysExecution()
    {
        var mem = new DeviceMemory();
        var wait = new WaitNode { DelayMs = 100 };
        var set  = new SetValueNode { DeviceKey = "D0", Value = 5 };
        var end  = new EndNode();
        wait.NextNodeId = set.Id;
        set.NextNodeId  = end.Id;

        var graph = MakeGraph(wait, set, end);
        var executor = new FlowExecutor(graph, mem);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await executor.RunFromAsync(wait.Id, CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(90);
        mem.GetWord("D0").Should().Be(5);
    }

    [Fact]
    public async Task Execute_Cancellation_StopsExecution()
    {
        var mem = new DeviceMemory();
        var wait = new WaitNode { DelayMs = 10000 };
        var end  = new EndNode();
        wait.NextNodeId = end.Id;

        var graph = MakeGraph(wait, end);
        var executor = new FlowExecutor(graph, mem);

        var cts = new CancellationTokenSource(200);
        var act = async () => await executor.RunFromAsync(wait.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

- [ ] **Step 2: 테스트 실행 — FAIL 확인**

```bash
dotnet test SimulatorProject.Tests --filter "FlowExecutorTests" -v
```

Expected: FAIL — `FlowExecutor` not found.

- [ ] **Step 3: FlowExecutor 구현**

`SimulatorProject/Engine/FlowExecutor.cs`:

```csharp
using SimulatorProject.Nodes;

namespace SimulatorProject.Engine;

public class FlowExecutor
{
    private readonly Dictionary<Guid, NodeBase> _graph;
    private readonly DeviceMemory _memory;

    public event Action<Guid>? NodeExecuting;

    public FlowExecutor(Dictionary<Guid, NodeBase> graph, DeviceMemory memory)
    {
        _graph = graph;
        _memory = memory;
    }

    public async Task RunFromAsync(Guid startId, CancellationToken ct)
    {
        Guid? currentId = startId;

        while (currentId.HasValue && !ct.IsCancellationRequested)
        {
            if (!_graph.TryGetValue(currentId.Value, out var node)) break;

            NodeExecuting?.Invoke(node.Id);
            currentId = await ExecuteNodeAsync(node, ct);
        }

        ct.ThrowIfCancellationRequested();
    }

    private async Task<Guid?> ExecuteNodeAsync(NodeBase node, CancellationToken ct)
    {
        switch (node)
        {
            case SetValueNode sv:
                _memory.SetWord(sv.DeviceKey, sv.Value);
                return sv.NextNodeId;

            case WaitNode w:
                await Task.Delay(w.DelayMs, ct);
                return w.NextNodeId;

            case ConditionNode cond:
                return EvaluateCondition(cond) ? cond.YesNodeId : cond.NoNodeId;

            case EndNode:
                return null;

            default:
                return node.NextNodeId;
        }
    }

    private bool EvaluateCondition(ConditionNode cond)
    {
        short actual = _memory.GetWord(cond.DeviceKey);
        // 비트 디바이스(M, X, Y)는 bool → short 변환
        if (cond.DeviceKey.StartsWith('M') || cond.DeviceKey.StartsWith('X') || cond.DeviceKey.StartsWith('Y'))
            actual = _memory.GetBit(cond.DeviceKey) ? (short)1 : (short)0;

        return cond.Operator switch
        {
            ConditionOperator.Equal       => actual == cond.CompareValue,
            ConditionOperator.NotEqual    => actual != cond.CompareValue,
            ConditionOperator.GreaterThan => actual >  cond.CompareValue,
            ConditionOperator.LessThan    => actual <  cond.CompareValue,
            _                             => false
        };
    }
}
```

- [ ] **Step 4: 테스트 실행 — PASS 확인**

```bash
dotnet test SimulatorProject.Tests --filter "FlowExecutorTests" -v
```

Expected: All 4 tests PASS.

- [ ] **Step 5: 커밋**

```bash
git add SimulatorProject/Engine/FlowExecutor.cs SimulatorProject.Tests/FlowExecutorTests.cs
git commit -m "feat: add FlowExecutor with SetValue/Wait/Condition/End node support"
```

---

## Task 7: ScenarioManager — JSON 저장/불러오기

**Files:**
- Create: `SimulatorProject/Engine/ScenarioManager.cs`
- Create: `SimulatorProject.Tests/ScenarioManagerTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`SimulatorProject.Tests/ScenarioManagerTests.cs`:

```csharp
using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Nodes;

namespace SimulatorProject.Tests;

public class ScenarioManagerTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesNodes()
    {
        var set  = new SetValueNode { DeviceKey = "D100", Value = 42, X = 100, Y = 200 };
        var end  = new EndNode { X = 200, Y = 200 };
        set.NextNodeId = end.Id;

        var graph = new Dictionary<Guid, NodeBase> { [set.Id] = set, [end.Id] = end };
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        await ScenarioManager.SaveAsync(graph, path);
        var loaded = await ScenarioManager.LoadAsync(path);

        loaded.Should().HaveCount(2);
        loaded[set.Id].Should().BeOfType<SetValueNode>()
            .Which.DeviceKey.Should().Be("D100");
        ((SetValueNode)loaded[set.Id]).Value.Should().Be(42);
        loaded[set.Id].NextNodeId.Should().Be(end.Id);
    }
}
```

- [ ] **Step 2: 테스트 실행 — FAIL 확인**

```bash
dotnet test SimulatorProject.Tests --filter "ScenarioManagerTests" -v
```

- [ ] **Step 3: ScenarioManager 구현**

`SimulatorProject/Engine/ScenarioManager.cs`:

```csharp
using System.Text.Json;
using SimulatorProject.Nodes;

namespace SimulatorProject.Engine;

public static class ScenarioManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task SaveAsync(Dictionary<Guid, NodeBase> graph, string filePath)
    {
        var nodes = graph.Values.ToList();
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, nodes, Options);
    }

    public static async Task<Dictionary<Guid, NodeBase>> LoadAsync(string filePath)
    {
        await using var fs = File.OpenRead(filePath);
        var nodes = await JsonSerializer.DeserializeAsync<List<NodeBase>>(fs, Options)
                    ?? new List<NodeBase>();
        return nodes.ToDictionary(n => n.Id);
    }
}
```

- [ ] **Step 4: 테스트 실행 — PASS 확인**

```bash
dotnet test SimulatorProject.Tests --filter "ScenarioManagerTests" -v
```

Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add SimulatorProject/Engine/ScenarioManager.cs SimulatorProject.Tests/ScenarioManagerTests.cs
git commit -m "feat: add ScenarioManager for JSON save/load"
```

---

## Task 8: ViewModels — MVVM 연결

**Files:**
- Create: `SimulatorProject/ViewModels/DeviceMonitorViewModel.cs`
- Create: `SimulatorProject/ViewModels/NodeViewModel.cs`
- Create: `SimulatorProject/ViewModels/ConnectionViewModel.cs`
- Create: `SimulatorProject/ViewModels/FlowChartViewModel.cs`
- Create: `SimulatorProject/ViewModels/MainViewModel.cs`

- [ ] **Step 1: DeviceMonitorViewModel 작성**

`SimulatorProject/ViewModels/DeviceMonitorViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Engine;

namespace SimulatorProject.ViewModels;

public partial class DeviceEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _value = "0";
}

public class DeviceMonitorViewModel : ObservableObject
{
    public ObservableCollection<DeviceEntryViewModel> Entries { get; } = new();

    public void Subscribe(DeviceMemory memory)
    {
        memory.ValueChanged += (key, value) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var entry = Entries.FirstOrDefault(e => e.Key == key);
                if (entry == null)
                {
                    entry = new DeviceEntryViewModel { Key = key };
                    Entries.Add(entry);
                }
                entry.Value = value?.ToString() ?? "0";
            });
        };
    }
}
```

- [ ] **Step 2: NodeViewModel 작성**

`SimulatorProject/ViewModels/NodeViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Nodes;

namespace SimulatorProject.ViewModels;

public partial class NodeViewModel : ObservableObject
{
    public NodeBase Model { get; }

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private bool _isSelected;

    public string DisplayName => Model.DisplayName;

    public NodeViewModel(NodeBase model)
    {
        Model = model;
        _x = model.X;
        _y = model.Y;
    }

    partial void OnXChanged(double value) => Model.X = value;
    partial void OnYChanged(double value) => Model.Y = value;
}
```

- [ ] **Step 3: ConnectionViewModel 작성**

`SimulatorProject/ViewModels/ConnectionViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimulatorProject.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    public NodeViewModel From { get; }
    public NodeViewModel To { get; }
    public string Label { get; }  // "", "YES", "NO"

    public ConnectionViewModel(NodeViewModel from, NodeViewModel to, string label = "")
    {
        From = from;
        To = to;
        Label = label;
    }
}
```

- [ ] **Step 4: FlowChartViewModel 작성**

`SimulatorProject/ViewModels/FlowChartViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulatorProject.Engine;
using SimulatorProject.Nodes;

namespace SimulatorProject.ViewModels;

public partial class FlowChartViewModel : ObservableObject
{
    private readonly DeviceMemory _memory;
    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();

    [ObservableProperty] private NodeViewModel? _selectedNode;

    public FlowChartViewModel(DeviceMemory memory)
    {
        _memory = memory;
    }

    public void AddNode(NodeBase node, double x, double y)
    {
        node.X = x; node.Y = y;
        var vm = new NodeViewModel(node);
        Nodes.Add(vm);
        RebuildConnections();
    }

    public void MarkExecuting(Guid nodeId)
    {
        foreach (var vm in Nodes)
            vm.IsExecuting = vm.Model.Id == nodeId;
    }

    public Dictionary<Guid, NodeBase> GetGraph() =>
        Nodes.ToDictionary(vm => vm.Model.Id, vm => vm.Model);

    public void RebuildConnections()
    {
        Connections.Clear();
        var lookup = Nodes.ToDictionary(n => n.Model.Id);
        foreach (var vm in Nodes)
        {
            if (vm.Model is ConditionNode cond)
            {
                if (cond.YesNodeId.HasValue && lookup.TryGetValue(cond.YesNodeId.Value, out var yes))
                    Connections.Add(new ConnectionViewModel(vm, yes, "YES"));
                if (cond.NoNodeId.HasValue && lookup.TryGetValue(cond.NoNodeId.Value, out var no))
                    Connections.Add(new ConnectionViewModel(vm, no, "NO"));
            }
            else if (vm.Model.NextNodeId.HasValue && lookup.TryGetValue(vm.Model.NextNodeId.Value, out var next))
            {
                Connections.Add(new ConnectionViewModel(vm, next));
            }
        }
    }
}
```

- [ ] **Step 5: MainViewModel 작성**

`SimulatorProject/ViewModels/MainViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;

namespace SimulatorProject.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DeviceMemory _memory = new();
    private TcpServer? _server;
    private CancellationTokenSource? _flowCts;

    public FlowChartViewModel FlowChart { get; }
    public DeviceMonitorViewModel Monitor { get; }

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _ip = "127.0.0.1";
    [ObservableProperty] private int _port = 5000;
    [ObservableProperty] private string _statusText = "정지";
    [ObservableProperty] private int _connectedClients;

    public MainViewModel()
    {
        FlowChart = new FlowChartViewModel(_memory);
        Monitor = new DeviceMonitorViewModel();
        Monitor.Subscribe(_memory);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning) return;
        IsRunning = true;
        StatusText = "실행 중";

        var adapter = new SlmpAdapter();
        _server = new TcpServer(adapter, _memory);
        _server.ClientCountChanged += count => App.Current.Dispatcher.Invoke(
            () => ConnectedClients = count);

        _ = _server.StartAsync(Ip, Port);

        _flowCts = new CancellationTokenSource();
        var graph = FlowChart.GetGraph();
        var firstNode = graph.Values.FirstOrDefault();
        if (firstNode != null)
        {
            var executor = new FlowExecutor(graph, _memory);
            executor.NodeExecuting += id =>
                App.Current.Dispatcher.Invoke(() => FlowChart.MarkExecuting(id));

            try { await executor.RunFromAsync(firstNode.Id, _flowCts.Token); }
            catch (OperationCanceledException) { }
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsRunning) return;
        _flowCts?.Cancel();
        if (_server != null) await _server.StopAsync();
        IsRunning = false;
        StatusText = "정지";
        FlowChart.MarkExecuting(Guid.Empty);
    }
}
```

- [ ] **Step 6: 빌드 확인**

```bash
dotnet build SimulatorProject/SimulatorProject.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 7: 커밋**

```bash
git add SimulatorProject/ViewModels/
git commit -m "feat: add MVVM ViewModels (Main, FlowChart, DeviceMonitor)"
```

---

## Task 9: WPF UI — MainWindow + 기본 레이아웃

**Files:**
- Modify: `SimulatorProject/MainWindow.xaml`
- Modify: `SimulatorProject/MainWindow.xaml.cs`
- Modify: `SimulatorProject/App.xaml.cs`

- [ ] **Step 1: App.xaml.cs에 ViewModel 바인딩**

`SimulatorProject/App.xaml.cs`:

```csharp
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
```

- [ ] **Step 2: MainWindow.xaml 작성**

`SimulatorProject/MainWindow.xaml`:

```xml
<Window x:Class="SimulatorProject.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:SimulatorProject.ViewModels"
        Title="DevSimulator" Height="700" Width="1200"
        Background="#1e1e1e">

    <Window.Resources>
        <Style TargetType="Button">
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="10,4"/>
            <Setter Property="Margin" Value="2"/>
        </Style>
        <Style TargetType="TextBlock">
            <Setter Property="Foreground" Value="#cccccc"/>
        </Style>
    </Window.Resources>

    <DockPanel>
        <!-- Toolbar -->
        <ToolBar DockPanel.Dock="Top" Background="#323233">
            <Button Command="{Binding StartCommand}" Background="#107c10">▶ 시작</Button>
            <Button Command="{Binding StopCommand}"  Background="#d83b01">⏹ 정지</Button>
            <Separator/>
            <TextBlock Text="IP:" VerticalAlignment="Center"/>
            <TextBox Text="{Binding Ip}" Width="120" Margin="4,0"/>
            <TextBlock Text="Port:" VerticalAlignment="Center"/>
            <TextBox Text="{Binding Port}" Width="60" Margin="4,0"/>
            <Separator/>
            <TextBlock Text="{Binding StatusText}" VerticalAlignment="Center" Margin="8,0"/>
            <TextBlock Text="{Binding ConnectedClients, StringFormat='클라이언트: {0}'}"
                       VerticalAlignment="Center" Margin="8,0"/>
        </ToolBar>

        <!-- Main area: FlowChart (left) + Monitor (right) -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="150"/>   <!-- Node Palette -->
                <ColumnDefinition Width="*"/>      <!-- Canvas -->
                <ColumnDefinition Width="200"/>   <!-- Monitor -->
            </Grid.ColumnDefinitions>

            <!-- Node Palette -->
            <StackPanel Grid.Column="0" Background="#252526" Margin="0">
                <TextBlock Text="노드 팔레트" Margin="8,8,8,4"
                           FontSize="10" Foreground="#888888"/>
                <Button Tag="SetValue" Click="PaletteButton_Click"
                        Background="#ca5010" Margin="6,2">✏️ Set Value</Button>
                <Button Tag="Wait" Click="PaletteButton_Click"
                        Background="#4a1942" Margin="6,2">⏱️ Wait</Button>
                <Button Tag="Condition" Click="PaletteButton_Click"
                        Background="#5c2d91" Margin="6,2">🔀 Condition</Button>
                <Button Tag="End" Click="PaletteButton_Click"
                        Background="#d83b01" Margin="6,2">🔴 End</Button>
            </StackPanel>

            <!-- FlowChart Canvas (Task 10에서 구현) -->
            <Border Grid.Column="1" Background="#1e1e1e" x:Name="CanvasBorder">
                <Canvas x:Name="FlowCanvas" Background="Transparent"
                        AllowDrop="True" Drop="FlowCanvas_Drop"
                        DragOver="FlowCanvas_DragOver"/>
            </Border>

            <!-- Device Monitor -->
            <Border Grid.Column="2" Background="#252526">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="디바이스 모니터"
                               Margin="8,8,8,4" FontSize="10" Foreground="#888888"/>
                    <DataGrid ItemsSource="{Binding Monitor.Entries}"
                              AutoGenerateColumns="False"
                              Background="#1e1e1e" Foreground="#cccccc"
                              GridLinesVisibility="Horizontal"
                              HeadersVisibility="Column"
                              CanUserAddRows="False">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Key"   Binding="{Binding Key}"   Width="80"/>
                            <DataGridTextColumn Header="Value" Binding="{Binding Value}"  Width="*"/>
                        </DataGrid.Columns>
                    </DataGrid>
                </DockPanel>
            </Border>
        </Grid>
    </DockPanel>
</Window>
```

- [ ] **Step 3: MainWindow.xaml.cs 작성**

`SimulatorProject/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using SimulatorProject.Nodes;
using SimulatorProject.ViewModels;

namespace SimulatorProject;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void PaletteButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (System.Windows.Controls.Button)sender;
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
        var tag  = e.Data.GetData(DataFormats.StringFormat) as string;
        var pos  = e.GetPosition(FlowCanvas);

        NodeBase? node = tag switch
        {
            "SetValue"  => new SetValueNode(),
            "Wait"      => new WaitNode(),
            "Condition" => new ConditionNode(),
            "End"       => new EndNode(),
            _           => null
        };

        if (node != null)
            vm.FlowChart.AddNode(node, pos.X, pos.Y);

        // Task 10에서 실제 WPF 노드 컨트롤 렌더링 추가
    }
}
```

- [ ] **Step 4: 빌드 및 앱 실행 확인**

```bash
dotnet build SimulatorProject/SimulatorProject.csproj
dotnet run --project SimulatorProject/SimulatorProject.csproj
```

Expected: 앱이 열리고 툴바, 팔레트, 빈 캔버스, 모니터 패널이 보임.

- [ ] **Step 5: 커밋**

```bash
git add SimulatorProject/MainWindow.xaml SimulatorProject/MainWindow.xaml.cs SimulatorProject/App.xaml.cs
git commit -m "feat: add main WPF window with toolbar, palette, and device monitor"
```

---

## Task 10: 플로우차트 캔버스 — 노드 렌더링 및 연결선

**Files:**
- Create: `SimulatorProject/Views/NodeControl.xaml`
- Create: `SimulatorProject/Views/NodeControl.xaml.cs`
- Modify: `SimulatorProject/MainWindow.xaml.cs` (캔버스 렌더링 로직)

- [ ] **Step 1: NodeControl.xaml 작성**

`SimulatorProject/Views/NodeControl.xaml`:

```xml
<UserControl x:Class="SimulatorProject.Views.NodeControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="140" Height="50"
             MouseLeftButtonDown="NodeControl_MouseLeftButtonDown"
             MouseMove="NodeControl_MouseMove"
             MouseLeftButtonUp="NodeControl_MouseLeftButtonUp">
    <Border BorderThickness="2" CornerRadius="6" Padding="8,4"
            Background="{Binding Background}"
            BorderBrush="{Binding BorderColor}">
        <TextBlock Text="{Binding DisplayName}" Foreground="White"
                   FontSize="11" VerticalAlignment="Center" HorizontalAlignment="Center"/>
    </Border>
</UserControl>
```

- [ ] **Step 2: NodeControl.xaml.cs 작성**

`SimulatorProject/Views/NodeControl.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimulatorProject.ViewModels;

namespace SimulatorProject.Views;

public partial class NodeControl : UserControl
{
    private bool _isDragging;
    private Point _dragStart;

    public NodeControl() => InitializeComponent();

    private void NodeControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(Parent as Canvas);
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
    }

    private void NodeControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }
}
```

- [ ] **Step 3: MainWindow에 캔버스 렌더링 메서드 추가**

`SimulatorProject/MainWindow.xaml.cs`에 아래 메서드 추가:

```csharp
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SimulatorProject.Views;
using SimulatorProject.ViewModels;

// MainWindow 클래스 안에 추가:

private void RenderFlowChart(FlowChartViewModel flowVm)
{
    FlowCanvas.Children.Clear();

    // 연결선 먼저 (노드 아래에 그려짐)
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
                Text = conn.Label, Foreground = Brushes.LightGreen, FontSize = 10
            };
            Canvas.SetLeft(label, (fromCenter.X + toCenter.X) / 2);
            Canvas.SetTop(label,  (fromCenter.Y + toCenter.Y) / 2);
            FlowCanvas.Children.Add(label);
        }
    }

    // 노드 컨트롤
    foreach (var nodeVm in flowVm.Nodes)
    {
        var ctrl = new NodeControl { DataContext = nodeVm };
        Canvas.SetLeft(ctrl, nodeVm.X);
        Canvas.SetTop(ctrl,  nodeVm.Y);
        FlowCanvas.Children.Add(ctrl);
    }
}
```

`FlowCanvas_Drop` 메서드 끝에 추가:

```csharp
RenderFlowChart(vm.FlowChart);
```

- [ ] **Step 4: NodeViewModel에 색상 속성 추가**

`SimulatorProject/ViewModels/NodeViewModel.cs`에 추가:

```csharp
using System.Windows.Media;

// NodeViewModel 클래스 안에 추가:
public Brush Background => Model switch
{
    SetValueNode  => new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10)),
    WaitNode      => new SolidColorBrush(Color.FromRgb(0x4A, 0x19, 0x42)),
    ConditionNode => new SolidColorBrush(Color.FromRgb(0x5C, 0x2D, 0x91)),
    EndNode       => new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01)),
    _             => Brushes.Gray
};

public Brush BorderColor => IsExecuting
    ? Brushes.Yellow
    : new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));
```

- [ ] **Step 5: 빌드 및 동작 확인**

```bash
dotnet build SimulatorProject/SimulatorProject.csproj
dotnet run --project SimulatorProject/SimulatorProject.csproj
```

Expected: 팔레트에서 노드를 캔버스로 드래그하면 색상 블록이 나타남.

- [ ] **Step 6: 커밋**

```bash
git add SimulatorProject/Views/ SimulatorProject/MainWindow.xaml.cs SimulatorProject/ViewModels/NodeViewModel.cs
git commit -m "feat: add flowchart canvas with node drag-drop and connection lines"
```

---

## Task 11: 전체 통합 테스트

**Files:**
- 없음 (기존 코드 통합 확인)

- [ ] **Step 1: 전체 테스트 실행**

```bash
dotnet test SimulatorProject.Tests -v
```

Expected: 모든 테스트 PASS.

- [ ] **Step 2: 통합 시나리오 수동 테스트**

1. 앱 실행: `dotnet run --project SimulatorProject/SimulatorProject.csproj`
2. `Set Value` 노드를 캔버스에 드롭 → 속성: D100 = 100
3. `End` 노드 드롭
4. ▶ 시작 클릭 (IP: 127.0.0.1, Port: 5000)
5. 별도 터미널에서 SLMP 읽기 요청 전송:

```python
# test_slmp.py — 파이썬으로 간단히 테스트
import socket, struct

sock = socket.socket()
sock.connect(('127.0.0.1', 5000))

# D100 읽기 요청 (SLMP 3E)
req = bytes([
    0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,  # 헤더
    0x0C, 0x00,                                  # 데이터 길이 12
    0x10, 0x00,                                  # CPU timer
    0x01, 0x04,                                  # Command READ
    0x00, 0x00,                                  # SubCommand
    0x64, 0x00, 0x00,                            # Device No 100 (D100)
    0x44,                                        # Device code 'D'
    0x01, 0x00,                                  # 1 point
])
sock.send(req)
resp = sock.recv(256)
value = struct.unpack_from('<h', resp, 11)[0]
print(f"D100 = {value}")  # Expected: 100
sock.close()
```

```bash
python test_slmp.py
```

Expected: `D100 = 100`

- [ ] **Step 3: 최종 커밋**

```bash
git add .
git commit -m "feat: complete Phase 1 — DevSimulator with SLMP + flowchart editor"
```

---

## 스펙 커버리지 체크

| 스펙 요구사항 | 구현 태스크 |
|---|---|
| TCP Server (멀티 클라이언트) | Task 3 |
| SLMP 읽기/쓰기 | Task 4 |
| DeviceMemory (스레드 안전) | Task 2 |
| 플로우차트 노드 모델 | Task 5 |
| FlowExecutor (Set/Wait/Condition/End) | Task 6 |
| ScenarioManager (JSON 저장/불러오기) | Task 7 |
| MVVM (CommunityToolkit) | Task 8 |
| WPF UI (팔레트 + 캔버스 + 모니터) | Task 9, 10 |
| 실행 중 노드 하이라이트 (노란 테두리) | Task 10 |
| IProtocolAdapter 플러그인 구조 | Task 3 |
