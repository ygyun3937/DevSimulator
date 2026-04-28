# DevSimulator — 설계 문서

**날짜:** 2026-04-01
**언어/플랫폼:** C# / WPF (.NET)
**상태:** 설계 확정

---

## 1. 개요

DevSimulator는 실제 통신 대상 장비(PLC, 로봇, 센서 등)가 없을 때 그 장비를 모사하는 **범용 통신 시뮬레이터**다.
블록코딩(플로우차트) 방식으로 응답 로직과 시나리오를 정의하며, TCP/IP 기반으로 외부 WPF 앱과 통신한다.
외부 앱 입장에서는 실제 장비와 동일한 프로토콜로 통신하므로, 실제 장비 교체 시 코드 변경이 불필요하다.

### 핵심 목적

- 실제 장비 없이 WPF 앱 개발 및 테스트
- 블록코딩으로 응답 로직 + 시나리오 정의
- 1차: 미쯔비시 Q 시리즈 SLMP 프로토콜 지원, 이후 Modbus TCP 등 확장

---

## 2. 아키텍처

### 레이어 구조

```
┌─────────────────────────────────────┐
│  WPF UI Layer                       │
│  - 플로우차트 에디터 (노드/연결선)      │
│  - 디바이스 메모리 모니터 (실시간)      │
│  - 프로토콜 설정 패널                  │
├─────────────────────────────────────┤
│  Simulator Engine                   │
│  - FlowExecutor (플로우 실행 루프)    │
│  - DeviceMemory (레지스터 저장소)     │
│  - ScenarioManager (저장/불러오기)   │
├─────────────────────────────────────┤
│  Protocol Layer (플러그인 방식)       │
│  - IProtocolAdapter 인터페이스       │
│  - SlmpAdapter (미쯔비시 SLMP)       │
│  - ModbusAdapter (추후)             │
│  - CustomTcpAdapter (추후)          │
├─────────────────────────────────────┤
│  TCP Server                         │
│  - 멀티 클라이언트 연결 지원           │
│  - 포트/IP 설정 가능                  │
└─────────────────────────────────────┘
```

### 네트워크 토폴로지

```
[외부 WPF 앱] ──── TCP (기본 Port 5000) ──── [DevSimulator]
```

- 같은 PC: `127.0.0.1:5000`
- LAN 연결: `192.168.x.x:5000`
- 실제 PLC 교체 시: IP만 변경, 외부 앱 코드 수정 없음

---

## 3. 컴포넌트 상세

### 3.1 WPF UI Layer

**플로우차트 에디터**
- 3단 레이아웃: 노드 팔레트 (좌) / 캔버스 (중) / 속성 패널 + 디바이스 모니터 (우)
- 노드를 드래그&드롭으로 캔버스에 배치, 연결선으로 흐름 연결
- 시나리오 탭: 여러 시나리오를 탭으로 관리
- 툴바: 시작/일시정지/정지, 저장/불러오기, 프로토콜 설정

**디바이스 모니터**
- 실시간 레지스터 값 표시 (D, M, Y, X 등)
- 값 변경 시 하이라이트
- 필터링 기능

**실행 중 편집**
- 노드 속성 값은 실행 중 즉시 반영
- 구조적 변경(노드 추가/삭제)은 현재 실행 노드 이후부터 적용

### 3.2 Simulator Engine

**FlowExecutor**
- 플로우차트를 해석하고 노드를 순서대로 실행하는 루프
- 현재 실행 중인 노드를 UI에 하이라이트
- 이벤트 트리거(On Request, On Write)에 반응

**DeviceMemory**
- 디바이스 레지스터를 딕셔너리로 관리: `Dictionary<string, object>`
- 키 형식: `"D100"`, `"M0"`, `"Y10"` 등
- 스레드 안전 접근 (UI 스레드 / TCP 스레드 / 플로우 실행 스레드 동시 접근)

**ScenarioManager**
- 플로우차트 정의를 JSON으로 직렬화/역직렬화
- 저장/불러오기

### 3.3 Protocol Layer

**IProtocolAdapter 인터페이스**

```csharp
public interface IProtocolAdapter
{
    string Name { get; }          // "SLMP", "Modbus TCP", "Custom"
    int DefaultPort { get; }
    Task<byte[]> HandleRequestAsync(byte[] request, DeviceMemory memory);
}
```

**SlmpAdapter (1차 개발 대상)**
- 미쯔비시 Q 시리즈 / iQ-R SLMP (3E 프레임) 지원
- 지원 명령: 디바이스 읽기(0401), 디바이스 쓰기(1401)
- 디바이스: D (데이터 레지스터), M (내부 릴레이), Y (출력), X (입력)

**향후 추가 어댑터**
- `ModbusAdapter`: Modbus TCP (Function Code 03/06/16)
- `CustomTcpAdapter`: JSON 등 사용자 정의 포맷

### 3.4 TCP Server

- `TcpListener` 기반 비동기 서버
- 멀티 클라이언트 지원 (async/await)
- 설정 가능: IP, Port

---

## 4. 플로우차트 노드 종류

| 카테고리 | 노드 | 설명 |
|---|---|---|
| 이벤트 트리거 | On Request | 외부 앱이 특정 디바이스 읽기 요청 시 발동 |
| 이벤트 트리거 | On Write | 외부 앱이 특정 디바이스에 쓰기 시 발동 |
| 흐름 제어 | Condition | 디바이스 값 조건에 따라 YES/NO 분기 |
| 흐름 제어 | Loop | 조건이 참인 동안 반복 |
| 흐름 제어 | End | 플로우 종료 |
| 액션 | Set Value | 디바이스 레지스터에 값 설정 |
| 액션 | Calculate | 사칙연산으로 레지스터 값 계산 |
| 액션 | Read Value | 레지스터 값을 변수로 읽기 |
| 타이밍 | Wait | 지정 시간(ms) 대기 |
| 타이밍 | Timer | 주기적으로 플로우 실행 |

---

## 5. 데이터 흐름

```
외부 WPF 앱
    │ TCP 요청 (SLMP 바이트 시퀀스)
    ▼
TCP Server
    │ 바이트 전달
    ▼
IProtocolAdapter.HandleRequestAsync()
    │ 파싱 → DeviceMemory 읽기/쓰기
    │ On Write 이벤트 → FlowExecutor 트리거
    ▼
DeviceMemory
    │ 값 반환
    ▼
IProtocolAdapter → SLMP 응답 직렬화
    │
    ▼
TCP Server → 외부 WPF 앱으로 응답 전송
```

---

## 6. 기술 스택

| 항목 | 선택 |
|---|---|
| 언어 | C# 10+ |
| UI 프레임워크 | WPF (.NET 8) |
| 노드 캔버스 라이브러리 | NodeNetwork 또는 직접 구현 (WPF Canvas) |
| 직렬화 | System.Text.Json |
| 비동기 TCP | TcpListener + async/await |
| 단위 테스트 | xUnit |

---

## 7. 개발 우선순위 (1차 범위)

1. TCP Server + SlmpAdapter (D 레지스터 읽기/쓰기)
2. DeviceMemory
3. 기본 플로우차트 에디터 (Set Value, Condition, Wait 노드)
4. FlowExecutor
5. 디바이스 모니터 UI
6. 시나리오 저장/불러오기

---

## 8. 향후 확장

- Modbus TCP 어댑터
- 커스텀 TCP (JSON) 어댑터
- 더 많은 SLMP 명령 지원 (랜덤 읽기/쓰기 등)
- 플로우 디버깅 (중단점, 스텝 실행)
- 시나리오 공유/임포트
