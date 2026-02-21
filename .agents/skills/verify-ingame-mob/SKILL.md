---
name: verify-ingame-mob
description: 인게임 몹(Mob) 시스템의 오브젝트 풀 반환, 컴포넌트 분리 설계, 화면 밖 이탈 방지 로직(MaxWanderDistance)을 검증합니다.
---

# 인게임 몹 생태계 검증

## Purpose

최근 추가된 몹 배회 경계 규칙 및 몹 데이터/로직 아키텍처가 올바르게 사용되고 있는지 확인합니다.

1. **상태 분리 및 컴포넌트화** — 거대한 `MobBase.cs` 하나에 모든 게 들어있지 않고 로직(`MobLogic`), 행동(`MobBrain`), 시각(`MobView`) 등으로 역할 분리가 유지되는지 검증
2. **화면 밖 이탈 제어 로직** — 거리에 따라 `MaxWanderDistance` 초과 시 복귀 처리가 구현되어 있는지 확인
3. **오브젝트 풀링 호환 사망 처리** — 몹 파괴 시 `Destroy()`가 아닌 반환형 메서드 활성화(`gameObject.SetActive(false)` 또는 커스텀 풀 반환)를 지키는지 검증

## When to Run

- 새로운 타입의 몬스터(Mob)를 추가할 때
- 몹의 배회, 추적, 사망 관련 코어 로직(`MobLogic`, `MobBrain`)을 수정했을 때
- 몹 스포너 및 웨이브 시스템의 반환 처리 방식이 바뀌었을 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/InGame/Mob/MobBase/*.cs` | 몹의 최상위 베이스 스크립트 |
| `Assets/_Game/Scripts/InGame/Mob/Systems/*.cs` | 브레인(FSM), 로직 데이터, 뷰 컴포넌트 |
| `Assets/_Game/Scripts/InGame/Mob/NormalMob.cs` | 기본 잡몹 객체 |
| `Assets/_Game/Scripts/InGame/ObjectPool/SpawnPositionSolver.cs` | 화면 밖 이탈 관련 생성/제한 수학 로직 |

## Workflow

### Step 1: 몹 배회/이동 제한 컴플라이언스 검사

`MobLogic.cs` 나 `NormalMobBrain.cs` 내부에서 배회 시 화면 중심이나 플레이어와의 거리를 판별하는 코드가 존재하는지 확인합니다. 무제한으로 뻗어나가 메모리에 남는 걸 방지해야 합니다.

**파일:** `Assets/_Game/Scripts/InGame/Mob/Systems/*.cs`

**검사:**
```bash
grep -n "MaxWanderDistance\|sqrMagnitude\|distance" Assets/_Game/Scripts/InGame/Mob/Systems/*.cs
```

**PASS:** 거리 제한 연산 로직(`MaxWanderDistance` 등)이 관측됨.
**FAIL:** 거리 비교 코드가 없음. (무한 맵 밖으로 이동할 위험).

### Step 2: 몹 오브젝트의 Destroy 남용 금지 검사

사망 처리는 오브젝트 풀링을 거쳐야 하므로, 몹 생명주기 관리 스크립트에서 일반 `Destroy(gameObject)`가 직접 호출되지 않아야 합니다.

**파일:** `Assets/_Game/Scripts/InGame/Mob/**/*.cs`

**검사:**
```bash
grep -n "Destroy(gameObject)" Assets/_Game/Scripts/InGame/Mob/**/*.cs
```

**PASS:** 아무것도 검색되지 않음.
**FAIL:** `Destroy(gameObject)` 발견.
**수정:** `gameObject.SetActive(false)` 나 풀 매니저의 `Release` 메서드 등으로 교체하세요.

### Step 3: MobBase 단일 책임 분리 점검

`MobBase`나 하위 스크립트들이 `Awake` 구간에서 책임 컴포넌트들을 찾아서 주입(Cache/Composition)하는지 확인하여 단일 God Object 형태를 방지하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/InGame/Mob/MobBase/MobBase.cs`

**검사:**
```bash
grep -n "GetComponentInChildren<MobBrain>\|GetComponentInChildren<MobLogic>" Assets/_Game/Scripts/InGame/Mob/MobBase/MobBase.cs
```

**PASS:** 컴포넌트 분리 후 캐싱하는 로직이나 변수가 발견됨.
**FAIL:** 명확한 분리 캐싱 없이 `MobBase` 하나로 모든 것을 다 처리함.

### Step 4: 몹 피격 효과(Hit Flash) 트리거 검증

`MobBase`나 하위 몹에서 데미지를 입을 때 `EffectManager`를 통해 피격 점멸 효과를 호출하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/InGame/Mob/**/*.cs`

**검사:**
```bash
grep -n "m_effectService?.PlayHitFlash" Assets/_Game/Scripts/InGame/Mob/**/*.cs
```

**PASS:** 피격 시 `PlayHitFlash` 호출 로직이 확인됨.
**FAIL:** 피격 효과 트리거 누락.

## Output Format

### 컴포넌트 상태

| 검증 항목 | 상태 | 상세 내역 |
|----------|------|-----------|
| 배회 이탈 제약 | PASS / FAIL | 거리 기반 제한 로직 존재 여부 |
| 풀링 반환 준수 | PASS / FAIL | `Destroy` 사용 안함 여부 |
| 컴포넌트 분리 설계 | PASS / 주의 | Base 클래스의 컴포넌트 캐싱 여부 |

## Exceptions

- 시각적 이펙트(이펙트 파티클 매니저가 관리하지 않는 일회성 이펙트 등) 한정으로 붙은 컴포넌트나, OnDestroy 주기에서의 참조 해제 `null` 처리는 예외.
