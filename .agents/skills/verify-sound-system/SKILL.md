---
name: verify-sound-system
description: 프로젝트 전체의 사운드 구현 패턴을 검증합니다. 싱글톤(Singleton) 대신 인터페이스 주입(DI) 방식의 사용을 권장합니다.
---

# 사운드 시스템 구현 패턴 검증

## Purpose

사운드 재생 로직이 유지보수와 테스트가 용이한 의존성 주입(DI) 방식으로 구현되어 있는지 검증하고 권장합니다:

1.  **패턴 권장** — `SoundManager.Instance` 대신 `ISoundManager` 인터페이스 주입 권장
2.  **전달 무결성** — 씬 전환(`ScenePayloadDTO`) 및 초기화 시 사운드 서비스가 올바르게 전달되는지 확인
3.  **정적 호출 최소화** — 비즈니스 로직(Mob, Weapon 등) 내에서 `SoundManager.PlaySound` 정적 메서드 사용 지양 권장

## When to Run

-   사운드 재생 로직이 포함된 새로운 클래스를 생성했을 때
-   기존 싱글톤 기반 사운드 코드를 리팩토링할 때
-   사운드가 나오지 않거나 사운드 매니저 참조가 Null인 문제가 발생할 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/Data/Services/ISoundManager.cs` | 사운드 서비스 인터페이스 |
| `Assets/_Game/Scripts/Lobby/SoundManager.cs` | 사운드 매니저 구현체 |
| `Assets/_Game/Scripts/Data/DTOs/ScenePayloadDTO.cs` | 서비스 전달용 페이로드 |
| `Assets/_Game/Scripts/InGame/Manager/GameManager.cs` | 인게임 서비스 주입 주체 |
| `Assets/_Game/Scripts/Lobby/LobbyUIViewManager.cs` | 로비 서비스 주입 주체 |

## Workflow

### Step 1: 싱글톤 직접 참조 탐지 (권장사항)

클래스 내부에서 `SoundManager.Instance`를 직접 참조하는지 확인합니다. 인터페이스(`ISoundManager`)를 통한 주입 방식을 사용하도록 권장합니다.

**파일:** `Assets/_Game/Scripts/**/*.cs`

**검사:**

```bash
grep -r "SoundManager.Instance" Assets/_Game/Scripts --exclude-dir=External
```

**권장:** 
- `m_soundManager`와 같은 지역 필드를 사용하고, `Init` 또는 생성자에서 `ISoundManager`를 주입받음.
- 정적 참조가 발견될 경우, "DI 전환 권장"으로 리포트합니다.

### Step 2: 정적 재생 메서드 사용 탐지 (권장사항)

`SoundManager.PlaySound(...)`와 같은 정적 헬퍼 함수 사용을 탐색합니다.

**파일:** `Assets/_Game/Scripts/**/*.cs`

**검사:**

```bash
grep -r "SoundManager.PlaySound" Assets/_Game/Scripts --exclude-dir=External
```

**권장:** 
- 주입받은 `ISoundManager.Play(...)` 메서드 호출로 전환 권장.

### Step 3: 서비스 주입 경로 및 Navigator 검증

씬 관리자들 및 내비게이터가 `ISoundManager`를 수신하여 하위 시스템으로 전달하는지 확인합니다.

**파일:** `LobbyNavigator.cs`, `LobbyUIViewManager.cs`, `GameManager.cs`

**검사:**

```bash
# 1. LobbyNavigator 생성자에서의 주입 확인
grep "public LobbyNavigator(.*InGame.Services.ISoundManager soundManager)" Assets/_Game/Scripts/Lobby/Core/LobbyNavigator.cs

# 2. LobbyNavigator에서 팝업 초기화 시 사운드 전달 확인
grep "m_currentOptionPopup.Initialize(m_soundManager);" Assets/_Game/Scripts/Lobby/Core/LobbyNavigator.cs

# 3. LobbyUIViewManager 등에서의 페이로드로부터 할당 확인
grep "scenePayload.SoundService" Assets/_Game/Scripts/Lobby/LobbyUIViewManager.cs
```

**PASS:** 모든 주입 및 전달 경로에서 인터페이스 기반 할당이 확인됨
**FAIL:** 주입이 누락되거나 싱글톤에 직접 의존함
**수정:** 생성자 주입 및 전달 코드 수정

## Output Format

### 사운드 구현 패턴 검증 결과

| 파일 | 현재 방식 | 권장 사항 | 상태 |
|------|----------|-----------|------|
| `Example.cs` | `SoundManager.Instance` | `ISoundManager` 주입 | ⚠️ 권장 |
| `Player.cs` | `m_soundManager.Play()` | - | ✅ 통과 |

## Exceptions

-   **Initializer/Service Entry**: `SoundManager` 자체를 생성하거나 최초로 주입하는 진입점 클래스는 예외.
-   **Editor Script**: 유니티 에디터 확장 스크립트는 싱글톤 접근 허용.
-   **UI Bindings**: 단순 버튼 클릭 사운드 등 하위 호환성을 위해 남겨둔 영역은 순차적 개선 권장.
