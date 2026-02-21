---
name: verify-weapon-system
description: 무기 시스템의 의존성 주입(DI), POCO 전략 패턴 설계, 팩토리 생성 검증을 수행합니다. 무기 시스템 스크립트 수정 후 사용.
---

# 무기 시스템 검증

## Purpose

최근 리팩토링된 무기 시스템(Weapon System) 아키텍처 규칙이 잘 지켜지고 있는지 확인합니다.

1. **POCO 기반 Strategy** — `IWeaponStrategy` 구현체들이 `MonoBehaviour`를 상속받지 않는 일반 C# 클래스인지 검증
2. **의존성 주입 (DI)** — `WeaponController`가 게임 매니저, 컴뱃 컨텍스트 등 핵심 서비스를 주입(Inject)받는지 검증
3. **Null 체크 룰** — 무기 시스템 내에서 Unity Object에 대한 `?.` 사용 금지 원칙 준수 여부 검증
4. **올바른 팩토리 사용** — 새로운 무기를 생성할 때 직접 `new`나 `Instantiate`가 아닌 `WeaponFactory`를 통하는지 여부 검증

## When to Run

- 새로운 무기(Weapon) 패턴이나 컨트롤러를 추가한 후
- `IWeaponStrategy` 인터페이스 기반 동작을 수정한 후
- 무기 풀링 시스템 및 팩트로 로직이 변경된 후

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/InGame/Weapon/Base/IWeaponController.cs` | 무기 컨트롤러 인터페이스 |
| `Assets/_Game/Scripts/InGame/Weapon/Base/WeaponControllerBase.cs` | 무기 컨트롤러 추상 기본 클래스 |
| `Assets/_Game/Scripts/InGame/Weapon/Strategies/IWeaponStrategy.cs` | 무기 동작 전략 (Strategy) 인터페이스 |
| `Assets/_Game/Scripts/InGame/Weapon/Strategies/*.cs` | 모든 무기 전략 구현체 |
| `Assets/_Game/Scripts/InGame/Weapon/Base/WeaponFactory.cs` | 무기 생성 관리 팩토리 |
| `Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerBase.cs` | 플레이어의 무기 장착 로직 부분 |

## Workflow

### Step 1: POCO (Plain Old C# Object) 전략 패턴 강제

전략 계층(`Strategies`) 스크립트들이 `MonoBehaviour`를 상속받지 않도록 검증합니다.

**파일:** `Assets/_Game/Scripts/InGame/Weapon/Strategies/*.cs`

**검사:**
```bash
grep -n ": MonoBehaviour" Assets/_Game/Scripts/InGame/Weapon/Strategies/*.cs
```

**PASS:** 아무것도 검색되지 않음.
**FAIL:** 전략 스크립트가 `MonoBehaviour`를 상속함.
**수정:** 의존성은 `OnUpdate`나 `Attack` 등을 통해 전달받도록 수정.

### Step 2: 무기 컨트롤러 DI 캡슐화 확인

`WeaponControllerBase`의 파생 클래스들이 직접 `GameManager.Instance` 등을 참조하지 않는지 검증합니다. 초기화(`Init` 등)에 Context가 주입되는지 확인합니다.

**파일:** `Assets/_Game/Scripts/InGame/Weapon/Controllers/*.cs`

**검사:**
```bash
grep -n "\.Instance" Assets/_Game/Scripts/InGame/Weapon/Controllers/*.cs
```

**PASS:** 싱글톤 사용 내역 없음 (또는 초기화 단계 캐싱 외에 무관한 곳만 있음)
**FAIL:** 비즈니스/공격 루프에서 전역 매니저 인스턴스 사용
**수정:** `WeaponControllerBase`의 Protected 멤버(`m_combatCtx` 등)로 대체

### Step 3: 팩토리 사용 및 객체 직접 생성 금지

플레이어나 매니저에서 무기 객체를 가져올 때 직접 `Instantiate` 하거나 `new WeaponController()`를 남용하는지 검증. `WeaponFactory.CreateController`를 통해야 합니다.

**파일:** `Assets/_Game/Scripts/InGame/Manager/GameManager.cs` 및 `Assets/_Game/Scripts/InGame/Player/Player_Base/*.cs`

**검사:**
무기 획득/생성 부근 검사 (GameManager의 `EquipNewWeapon` 위주)
```bash
grep -n "WeaponFactory.CreateController" Assets/_Game/Scripts/InGame/Manager/GameManager.cs
```

**PASS:** 무기 장착부에서 `WeaponFactory.CreateController`가 감지됨
**FAIL:** `CreateController`를 통하지 않고 강제로 스크립트를 추가

### Step 4: 무기 객체들의 Unity Object Null-Conditional 사용 감시

Unity 생명주기 관련 문제(Fake Null)를 막기 위해 무기 관련 스크립트 전반에서 Unity Object에 대한 `?.` 연산자 제한.
*(단, Action, Tween 등 순수 C# 객체에 대해서는 허용되므로 결과 눈으로 확인 필요)*

**검사:**
```bash
grep -n "\?\." Assets/_Game/Scripts/InGame/Weapon/**/*.cs
```

**PASS:** `?.` 가 순수 기능(`Kill()`, `Invoke()`)에만 사용됨
**FAIL:** `gameObject?.activeSelf` 나 유사한 컴포넌트 호출에 사용

## Output Format

### 컴포넌트 상태

| 컴포넌트 | 상태 | 상세 내역 |
|----------|------|-----------|
| Strategy POCO 유지 | PASS / FAIL | |
| Controller DI 적용 | PASS / FAIL | |
| 팩토리 집중 관리 | PASS / FAIL | |
| Null 안전성 규칙 | PASS / 주의 | (순수 C# 객체에 쓰였는지 확인 후 판정) |

## Exceptions

- `MonoBehaviour` 상속이 불가피한 발사체(Projectile 뷰) 클래스와 `Aura` 등은 `Weapon/Controllers`나 `Weapon/Strategies` 밖의 디렉토리에 배치되므로 예외입니다. (예: `BoneBullet.cs`)
- 타 컴포넌트에서 `?.` 을 딜리게이트 콜업이나 `Tween` 해제용(`m_rotateTween?.Kill()`)으로 사용하는 건 안전하며 예외 조항입니다.
