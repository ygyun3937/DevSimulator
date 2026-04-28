# 서브에이전트 활용 가이드

Claude Code에서 복잡한 구현 작업을 서브에이전트에게 분산하는 방법을 설명합니다.

---

## 1. 서브에이전트란?

서브에이전트(Subagent)는 Claude Code 세션 안에서 **독립적으로 실행되는 또 다른 Claude 인스턴스**입니다.

```
[나 (Controller Claude)]
    │
    ├──▶ [서브에이전트: Implementer]  → 코드 작성, 테스트, 커밋
    ├──▶ [서브에이전트: Spec Reviewer] → 스펙 준수 검토
    └──▶ [서브에이전트: Code Reviewer] → 코드 품질 검토
```

**핵심 장점:**
- 각 서브에이전트는 **깨끗한 컨텍스트**로 시작 (이전 대화 오염 없음)
- Controller(나)는 정확히 필요한 정보만 서브에이전트에게 전달
- 역할이 분리되어 품질 게이트 역할을 함

---

## 2. 전체 워크플로우

하나의 태스크를 완료하는 흐름:

```
Controller가 구현 계획 읽기
       ↓
[Implementer 서브에이전트 투입]
       ↓
   질문 있으면 답변
       ↓
   코드 작성 + 테스트 + 커밋 + 자체 검토
       ↓
[Spec Reviewer 서브에이전트 투입]
       ↓
   ❌ 스펙 미준수 → Implementer가 수정 → 재검토
   ✅ 스펙 준수
       ↓
[Code Quality Reviewer 서브에이전트 투입]
       ↓
   ❌ 품질 문제 → Implementer가 수정 → 재검토
   ✅ 품질 승인
       ↓
태스크 완료 → 다음 태스크
```

---

## 3. Claude Code에서 서브에이전트 호출 방법

Claude Code에서 서브에이전트는 **`Agent` 도구**를 통해 호출합니다.

### 3-1. Implementer 서브에이전트 투입

```
Agent 도구 호출:
  subagent_type: "general-purpose"
  description: "Implement Task 1: 솔루션 설정"
  prompt: |
    You are implementing Task 1: 솔루션 설정

    ## Task Description
    [구현 계획에서 해당 태스크 전문을 그대로 붙여넣기]

    ## Context
    [이 태스크가 전체 프로젝트에서 어떤 위치인지 설명]
    예: "이것은 DevSimulator WPF 프로젝트의 첫 번째 태스크입니다.
         이후 태스크들이 이 설정을 기반으로 빌드됩니다."

    ## Before You Begin
    궁금한 점이 있으면 지금 질문하세요.

    ## Your Job
    1. 태스크에 명시된 것만 구현
    2. 테스트 작성 (TDD 방식)
    3. 구현 검증
    4. 커밋
    5. 자체 검토 후 보고

    Work from: /path/to/project

    ## Report Format
    - Status: DONE | DONE_WITH_CONCERNS | BLOCKED | NEEDS_CONTEXT
    - 구현한 내용
    - 테스트 결과
    - 변경된 파일
    - 자체 검토 결과
```

### 3-2. Spec Reviewer 서브에이전트 투입

```
Agent 도구 호출:
  subagent_type: "general-purpose"
  description: "Review spec compliance for Task 1"
  prompt: |
    You are reviewing whether an implementation matches its specification.

    ## What Was Requested
    [태스크 요구사항 전문]

    ## What Implementer Claims They Built
    [Implementer의 보고 내용]

    ## Your Job
    implementer의 보고를 믿지 말고 실제 코드를 직접 읽어서:
    - 누락된 요구사항 확인
    - 불필요하게 추가된 기능 확인
    - 요구사항 오해 여부 확인

    Report:
    - ✅ Spec compliant
    - ❌ Issues found: [구체적으로 파일:라인 참조]
```

### 3-3. Code Quality Reviewer 서브에이전트 투입

```
Agent 도구 호출:
  subagent_type: "superpowers:code-reviewer"
  description: "Code quality review for Task 1"
  prompt: |
    WHAT_WAS_IMPLEMENTED: [Implementer 보고 내용]
    PLAN_OR_REQUIREMENTS: Task 1 from docs/superpowers/plans/2026-04-01-devsimulator.md
    BASE_SHA: [태스크 시작 전 git commit SHA]
    HEAD_SHA: [태스크 완료 후 git commit SHA]
    DESCRIPTION: [태스크 한 줄 요약]
```

---

## 4. Implementer 응답 처리

Implementer는 4가지 상태 중 하나를 보고합니다:

| 상태 | 의미 | Controller 대응 |
|---|---|---|
| **DONE** | 완료 | Spec Reviewer 투입 |
| **DONE_WITH_CONCERNS** | 완료했지만 우려사항 있음 | 우려사항 검토 후 Spec Reviewer 투입 |
| **NEEDS_CONTEXT** | 정보가 부족함 | 필요한 정보 제공 후 재투입 |
| **BLOCKED** | 진행 불가 | 이유 파악 후 컨텍스트 추가 또는 더 강력한 모델로 재투입 |

---

## 5. 모델 선택 가이드

비용을 아끼면서 품질을 유지하려면 적절한 모델을 선택하세요:

| 역할 | 권장 모델 | 이유 |
|---|---|---|
| Implementer (단순, 1~2 파일) | `haiku` | 명확한 스펙, 기계적 작업 |
| Implementer (복잡, 여러 파일) | `sonnet` | 통합 판단 필요 |
| Spec Reviewer | `sonnet` | 요구사항 대조 |
| Code Quality Reviewer | `opus` | 아키텍처 판단 |

Agent 도구에서 `model` 파라미터로 지정:
```
model: "haiku"   # claude-haiku-4-5
model: "sonnet"  # claude-sonnet-4-6 (기본값)
model: "opus"    # claude-opus-4-6
```

---

## 6. 실전 예시 — DevSimulator Task 2 실행

### Step 1: Implementer 투입

```
Agent 호출:
  subagent_type: "general-purpose"
  model: "sonnet"
  description: "Implement Task 2: DeviceMemory"
  prompt: |
    You are implementing Task 2: DeviceMemory — 스레드 안전 레지스터 저장소

    ## Task Description
    [docs/superpowers/plans/2026-04-01-devsimulator.md 에서 Task 2 전문 붙여넣기]

    ## Context
    이것은 DevSimulator WPF 프로젝트 (C#/.NET 8)의 핵심 엔진 컴포넌트입니다.
    DeviceMemory는 PLC 레지스터(D100, M0 등)를 메모리에 저장하고,
    여러 스레드(TCP 서버 스레드, 플로우 실행 스레드, UI 스레드)가 동시 접근합니다.

    Task 1 (프로젝트 설정)은 이미 완료되어 있습니다.
    솔루션 구조:
    - SimulatorProject/ (WPF 앱)
    - SimulatorProject.Tests/ (xUnit 테스트)

    Work from: /Users/yg/project/cc-project/simulator-project
```

### Step 2: Implementer 보고 수신 후 Spec Reviewer 투입

```
Agent 호출:
  subagent_type: "general-purpose"
  model: "sonnet"
  description: "Review spec compliance for Task 2: DeviceMemory"
  prompt: |
    [spec-reviewer-prompt.md 내용 + Task 2 요구사항 + Implementer 보고 내용]
```

### Step 3: Spec ✅ 후 Code Quality Reviewer 투입

```
Agent 호출:
  subagent_type: "superpowers:code-reviewer"
  description: "Code quality review for Task 2: DeviceMemory"
  prompt: |
    WHAT_WAS_IMPLEMENTED: DeviceMemory.cs (ConcurrentDictionary 기반 레지스터 저장소)
    BASE_SHA: abc1234
    HEAD_SHA: def5678
    DESCRIPTION: Task 2 - DeviceMemory with thread-safe register storage
```

---

## 7. 자주 하는 실수

### ❌ 서브에이전트에게 계획 파일을 직접 읽으라고 하기
```
# 나쁜 예
prompt: "docs/superpowers/plans/plan.md 파일을 읽고 Task 2를 구현하세요"

# 좋은 예
prompt: "## Task Description\n[Task 2 전문을 여기에 직접 붙여넣기]"
```
서브에이전트는 컨텍스트가 없어서 파일을 찾고 해석하는 데 시간을 낭비합니다.
Controller가 필요한 정보를 **미리 추출해서** 전달해야 합니다.

### ❌ Spec 검토 전에 Code Quality 검토하기
반드시 순서를 지켜야 합니다:
```
Implementer → Spec Reviewer → Code Quality Reviewer
```

### ❌ 검토에서 문제 발견 시 그냥 넘어가기
Reviewer가 문제를 찾으면 Implementer(동일한 서브에이전트)가 수정하고,
Reviewer가 **재검토**해야 합니다. 재검토 없이 넘어가면 안 됩니다.

### ❌ 여러 Implementer 서브에이전트를 병렬 투입하기
태스크 간 의존성(파일 충돌, git 충돌)이 생깁니다.
**Implementer는 항상 순차적으로 한 번에 하나씩** 투입합니다.

---

## 8. 서브에이전트 투입 시 제공해야 할 정보 체크리스트

Implementer 투입 전 확인:

- [ ] 태스크 전문 (계획 파일에서 복사)
- [ ] 프로젝트 작업 디렉터리 경로
- [ ] 이 태스크가 전체에서 어떤 위치인지 (컨텍스트)
- [ ] 이전 태스크에서 완료된 것 (의존성)
- [ ] 기술 스택 정보 (언어, 프레임워크, 버전)

Spec Reviewer 투입 전 확인:

- [ ] 태스크 요구사항 전문
- [ ] Implementer의 보고 내용

Code Quality Reviewer 투입 전 확인:

- [ ] Implementer의 보고 내용
- [ ] BASE_SHA (태스크 시작 전 커밋)
- [ ] HEAD_SHA (태스크 완료 후 커밋)

---

## 9. 전체 태스크 진행 흐름 (DevSimulator 기준)

```
Controller가 할 일:
1. docs/superpowers/plans/2026-04-01-devsimulator.md 읽기
2. Task 1~11 전문 추출 및 메모
3. TodoWrite로 태스크 목록 생성
4. Task 1부터 순서대로:
   a. Implementer 투입
   b. (질문 있으면 답변)
   c. Spec Reviewer 투입 → 문제 있으면 수정 루프
   d. Code Quality Reviewer 투입 → 문제 있으면 수정 루프
   e. 태스크 완료 표시
5. 모든 태스크 완료 후 최종 코드 리뷰
6. finishing-a-development-branch 스킬로 마무리
```

---

## 10. 참고 — Agent 도구 파라미터

```
Agent(
  subagent_type: str,    # "general-purpose", "superpowers:code-reviewer" 등
  description: str,      # 3~5단어 요약 (로그에 표시됨)
  prompt: str,           # 서브에이전트에게 전달할 전체 지시
  model: str,            # "haiku" | "sonnet" | "opus" (선택사항)
  run_in_background: bool # True면 백그라운드 실행 (결과 나중에 수신)
)
```

`run_in_background=True`는 **Implementer에게 쓰지 마세요** — 결과를 기다려야 다음 단계로 넘어갈 수 있습니다.

---

*이 가이드는 `superpowers:subagent-driven-development` 스킬 기반으로 작성되었습니다.*
