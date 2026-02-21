---
name: verify-player-system
description: 플레이어 컨트롤러, 자동 공격(AI) 루프, 카이팅(Kiting) 이동 및 토글 연동 무결성을 검증합니다.
---

# 플레이어 핵심 시스템 검증

## Purpose

플레이어 캐릭터의 조작 반응성과 전투 AI의 안정성을 확보하기 위해 다음을 검증합니다:

1.  **조작 상호 배제** — 조이스틱 입력 시 자동 공격 시스템이 안전하게 중단되는지 확인
2.  **전투 AI (Kiting)** — 적과의 거리에 따른 이동 방향(접근/후퇴) 결정 로직의 정합성 확인
3.  **루프 무결성** — 비동기 루프(`AutoAttackLoopAsync`)가 `CancellationToken`을 정확히 준수하여 메모리 누수를 방지하는지 확인
4.  **설정 동기화** — UI 토글 설정이 `PlayerController`를 통해 하위 시스템으로 즉시 전파되는지 확인

## When to Run

-   플레이어 조작 로직(`PlayerController.cs`)을 수정한 후
-   자동 공격 AI(`PlayerAutoAttackSystem.cs`) 또는 이동 로직(`PlayerMovement.cs`)을 변경한 후
-   전투 관련 밸런스(사거리, 안전 거리 등) 상수를 조절했을 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerController.cs` | 플레이어 하위 시스템 제어 및 Facade |
| `Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerAutoAttackSystem.cs` | 적 탐지 및 추적/공격 루프 로직 |
| `Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerMovement.cs` | 캐릭터 물리/트랜스폼 이동 처리 |
| `Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerInputHandler.cs` | 조이스틱 및 키보드 입력 해석 |

## Workflow

### Step 1: 자동 공격 토글 연동 및 즉시 반응 검사

토글 값이 설정될 때 즉시 공격 루프를 가동하는지 확인합니다.

**파일:** `PlayerController.cs`

**검사:**

```bash
grep -A15 "public bool AutoAttackEnabledByToggle" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerController.cs | grep "m_autoAttack.Enable()"
```

**PASS:** Setter 내에서 `value`가 true일 때 `Enable()`을 즉시 호출함
**FAIL:** 단순히 필드 값만 저장하고 다음 프레임을 기다림
**수정:** `AutoAttackEnabledByToggle` setter에 즉시 활성화 로직 추가

### Step 2: 카이팅(Kiting) AI 로직 정합성 검사

안전 거리(`m_safeDistance`) 계산 및 후퇴 이동 방향이 올바른지 확인합니다.

**파일:** `PlayerAutoAttackSystem.cs`

**검사:**

```bash
# 1. 안전 거리 필드 존재 확인
grep "m_safeDistance" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerAutoAttackSystem.cs

# 2. 후퇴 이동 로직 (-dirToTarget) 확인
grep "AutoMoveDirection = -dirToTarget" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerAutoAttackSystem.cs
```

**PASS:** `m_safeDistance` 상수가 존재하며, 거리 미달 시 타겟 반대 방향으로 이동 벡터 설정
**FAIL:** 후퇴 로직이 없거나 방향 계산 오류
**수정:** `AutoAttackLoopAsync` 내 거리 비교 로직 보강

### Step 3: 비동기 루프 및 취소 토큰 검사

무한 루프가 오브젝트 파괴 시 적절히 종료되는지 확인합니다.

**파일:** `PlayerAutoAttackSystem.cs`

**검사:**

```bash
# 1. 루프 시작 시 연동된 토큰(LinkedToken) 사용 확인
grep "CreateLinkedTokenSource" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerAutoAttackSystem.cs

# 2. Yield 시 토큰 전달 확인
grep "await UniTask.Yield" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerAutoAttackSystem.cs | grep "token"
```

**PASS:** `GetCancellationTokenOnDestroy()`와 연동된 토큰을 루프와 Yield에 사용함
**FAIL:** 토큰 없이 루프를 돌리거나 Yield 시 토큰 누락 (메모리 누수 위험)
**수정:** 비동기 메서드 인자로 `CancellationToken`을 전달하고 상시 확인

### Step 4: 조이스틱-자동 공격 상호 배제 검사

사용자가 직접 조종할 때 AI 이동이 중단되는지 확인합니다.

**파일:** `PlayerController.cs`

**검사:**

```bash
grep -A10 "if (isJoystickActive)" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerController.cs | grep "m_autoAttack.Disable()"
```

**PASS:** `isJoystickActive` 조건문 내부에서 `m_autoAttack.Disable()` 호출 존재
**FAIL:** 조작 중에도 AI 이동 벡터가 간섭함
**수정:** `HandleControlLogic` 내 이동 우선순위 로직 수정

## Output Format

### 검증 결과

| 검사 항목 | 상태 | 파일/상세 |
|-----------|------|-----------|
| 토글 즉시 반응 | PASS | Setter 내 Enable() 호출 확인 |
| Kiting 로직 | PASS | 안전 거리 기반 후퇴 벡터 확인 |
| 루프 안정성 | PASS | CancellationToken 연동 확인 |
| 조작 상호 배제 | PASS | 조이스틱 활성 시 AI 중단 확인 |

## Exceptions

-   수동 조작 모드에서도 적이 사거리 내에 있다면 '공격(무기 발사)'은 수행될 수 있습니다. (조향만 조이스틱 우선)
-   `PlayerLoopTiming` 변경으로 인해 `Yield` 대신 `Delay`를 사용하는 구간이 생길 수 있으나, 항상 토큰 취소 여부를 확인해야 합니다.
