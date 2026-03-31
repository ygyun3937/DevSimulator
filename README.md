# DevSimulator

> 미쯔비시 PLC를 비롯한 산업 장비를 소프트웨어로 모사하는 **범용 통신 시뮬레이터**

실제 장비 없이 WPF 앱을 개발하고 테스트할 수 있습니다.
블록코딩(플로우차트) 방식으로 장비의 응답 로직과 시나리오를 직접 정의합니다.

---

## 동작 원리

```
[여러분의 WPF 앱] ──── TCP (SLMP) ──── [DevSimulator]
                                           ↕
                                     블록코딩으로 정의한
                                     응답 로직 실행
```

WPF 앱 입장에서는 실제 PLC와 구분이 없습니다.
나중에 실제 PLC로 교체할 때 **IP 주소만 바꾸면** 됩니다.

---

## 주요 기능

- 🔌 **SLMP (MC Protocol)** 기반 TCP 통신 — 미쯔비시 Q/iQ-R 시리즈 호환
- 📊 **플로우차트 블록코딩** — Set Value, Wait, Condition, End 노드
- 📡 **실시간 디바이스 모니터** — D/M/Y/X 레지스터 값 실시간 확인
- 🔧 **실행 중 편집** — 시뮬레이션 중에도 노드 속성 변경 가능
- 💾 **시나리오 저장/불러오기** — JSON 파일로 플로우차트 저장

---

## 스크린샷

```
┌──────────────────────────────────────────────────────┐
│  [▶ 시작]  [⏹ 정지]  IP: 127.0.0.1  Port: 5000      │
├──────────┬────────────────────────────┬───────────────┤
│ 노드팔레트│       캔버스               │ 디바이스모니터 │
│          │  [Set D100=100]            │ D100 │ 100   │
│✏️ Set   │       ↓                    │ D101 │  0    │
│⏱️ Wait  │  [Wait 2000ms]             │ M0   │  0    │
│🔀 Cond  │       ↓                    │               │
│🔴 End   │  [Set D100=0]              │               │
│          │       ↓                    │               │
│          │    [End]                   │               │
└──────────┴────────────────────────────┴───────────────┘
```

---

## 빠른 시작

### 요구사항
- Windows 10/11
- Visual Studio 2022 + .NET 8 데스크톱 개발 워크로드

### 실행
```bash
git clone https://github.com/ygyun3937/DevSimulator.git
```
`SimulatorProject.sln` 을 Visual Studio로 열고 **Ctrl+F5** 실행

### 테스트
앱 실행 후 ▶ 시작 클릭, 그 다음:
```bash
cd examples
python test_slmp.py
```

---

## 프로젝트 구조

```
SimulatorProject/
├── Engine/          # DeviceMemory, FlowExecutor, ScenarioManager
├── Nodes/           # SetValue, Wait, Condition, End 노드 모델
├── Protocol/        # IProtocolAdapter, TcpServer, SlmpAdapter
├── ViewModels/      # MVVM ViewModels
└── Views/           # WPF 컨트롤
SimulatorProject.Tests/
└── 단위 테스트 (xUnit + FluentAssertions)
docs/
├── user-guide.md              # 사용자 가이드 (비개발자용)
├── subagent-guide.md          # 서브에이전트 활용 가이드
└── superpowers/
    ├── specs/                 # 설계 문서
    └── plans/                 # 구현 계획
examples/
├── scenario1.json             # 샘플 시나리오
└── test_slmp.py               # Python SLMP 테스트 스크립트
```

---

## 지원 프로토콜

| 프로토콜 | 상태 |
|---|---|
| SLMP / MC Protocol (미쯔비시) | ✅ 지원 |
| Modbus TCP | 🔜 예정 |
| Custom TCP | 🔜 예정 |

---

## 문서

- [📖 사용자 가이드](docs/user-guide.md) — 비개발자도 볼 수 있는 사용 방법
- [🏗️ 설계 문서](docs/superpowers/specs/2026-04-01-devsimulator-design.md)
- [📋 구현 계획](docs/superpowers/plans/2026-04-01-devsimulator.md)
