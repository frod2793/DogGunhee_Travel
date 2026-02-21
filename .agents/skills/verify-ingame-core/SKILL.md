---
name: verify-ingame-core
description: 인게임 핵심 시스템(GameManager, UIManager), 초기화 순서 안전성, UI 데이터 바인딩을 검증합니다.
---

# 인게임 핵심 시스템 검증

## Purpose

인게임 플레이의 안정성을 확보하기 위해 핵심 매니저 클래스와 UI 간의 데이터 바인딩 구조가 올바른지 검증합니다:

1.  **매니저 수명 정보** — `GameManager` 및 `UIManager`가 올바르게 초기화되고 수명 주기 내에서 안정적으로 동작하는지 확인
2.  **데이터 바인딩** — `InGameViewModel`이 `PlayerDataService` 주입을 통해 데이터를 안정적으로 수신하는지 확인
3.  **UI 갱신** — `UIManager`가 ViewModel의 상태 변화를 감지하여 뷰를 적절히 갱신하는지 확인
4.  **서비스 전파 권장** — `GameManager`가 `ISoundManager`를 `UIManager` 및 하위 개체들에 올바르게 전파하여 주입하는지 확인(권장)

## When to Run

-   인게임 로직(`GameManager.cs`, `UIManager.cs`)을 수정한 후
-   인게임 UI 컴포넌트나 ViewModel을 변경한 후
-   플레이어 데이터 처리 서비스(`PlayerDataService.cs`)가 변경되었을 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/InGame/Manager/GameManager.cs` | 인게임 전체 흐름 관리 |
| `Assets/_Game/Scripts/InGame/Manager/UIManager.cs` | 인게임 UI 전환 및 상태 관리 |
| `Assets/_Game/Scripts/InGame/UI/ViewModels/InGameViewModel.cs` | 인게임 UI용 데이터 바인딩 뷰모델 |
| `Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerBase.cs` | 플레이어 핵심 로직 및 이벤트 발생지 |
| `Assets/_Game/Scripts/Data/Services/PlayerDataService.cs` | 플레이어 데이터 비즈니스 로직 관리 |
| `Assets/_Game/Scripts/Data/DTOs/PlayerDataDTO.cs` | 플레이어 핵심 데이터 구조 |
| `Assets/_Game/Scripts/InGame/GameSceneCompositionRoot.cs` | 인게임 씬 DI 조립 진입점 |
| `Assets/_Game/Scripts/Lobby/Core/LobbySceneCompositionRoot.cs` | 로비 씬 DI 조립 진입점 |
| `Assets/_Game/Scripts/Lobby/SceneLoader.cs` | 씬 전환 로더 및 초기화 관리 |
| `Assets/_Game/Scripts/InGame/UI/Views/GameOverPopup.cs` | 게임 오버 팝업 뷰 |
| `Assets/_Game/Scripts/Lobby/SoundManager.cs` | 사운드 매니저 (DontDestroyOnLoad 대상) |
| `Assets/_Game/Scripts/InGame/JoystickSetter/JoysticSetter.cs` | 조이스틱 시각적 설정 관리 UI |
| `Assets/_Game/Scripts/InGame/JoystickSetter/JoyStickPosDragandDrop.cs` | 조이스틱 부모 영역 내 드래그 핸들러 |

## Workflow

### Step 1: 싱글톤 의존성 검사

`GameManager`나 `UIManager` 내에서 타 시스템의 싱글톤(`Instance`)을 직접 참조하는지 검사합니다. 아키텍처 원칙에 따라 주입(DI) 또는 `Init` 메서드를 통한 참조 권장.

**파일:** `Assets/_Game/Scripts/InGame/Manager/*.cs`

**검사:**

```bash
grep ".Instance" Assets/_Game/Scripts/InGame/Manager/*.cs
```

**PASS:** 발견되지 않거나, `Awake` 내 캐싱용으로만 사용됨
**FAIL:** 비즈니스 로직 내에서 전역 인스턴스 직접 사용
**수정:** 생성자나 `Init` 메서드를 통해 전달받도록 리팩토링

### Step 2: UI 데이터 바인딩 구독 및 주입 상태 확인

`InGameViewModel`이 `PlayerDataService`를 생성자 또는 초기화 메서드를 통해 올바르게 주입받고 구독하고 있는지 확인합니다.

**파일:** `Assets/_Game/Scripts/InGame/UI/ViewModels/InGameViewModel.cs`

**검사:**

```bash
grep -E "(On[A-Za-z]+Changed|\.Subscribe)" Assets/_Game/Scripts/InGame/UI/ViewModels/InGameViewModel.cs
```

**PASS:** 데이터 변경에 대한 이벤트 핸들러 또는 구독 로직 존재
**FAIL:** 단순 Getter/Setter만 존재하거나 갱신 로직 누락
**수정:** 반응형 프로퍼티(`ReactiveProperty`, `Action`) 적용

### Step 3: Null 안전성 검사

유니티 오브젝트 참조에 대한 Null 체크가 Allman Style로 구현되어 있는지 확인합니다.

**파일:** `Assets/_Game/Scripts/InGame/**/*.cs`

**검사:**

```bash
grep "?." Assets/_Game/Scripts/InGame/**/*.cs | grep -v "Assets/_Game/Scripts/InGame/External"
```

**PASS:** 결과 없음 (사용자 규칙에 따라 Unity Object에 `?.` 금지)
**FAIL:** Unity Object 참조에 `?.` 사용 중
**수정:** `if (obj != null)` 블록으로 변경

### Step 4: 서비스 전파 및 팝업 서비스 주입 확인 (권장사항)

`GameManager`에서 주입받은 `ISoundManager` 및 `IPopupService`가 하위 개체들에 올바르게 전달되는지 확인합니다.

**파일:** `GameManager.cs`, `UIManager.cs`, `OptionPopupView.cs`

**검사:**
1. `GameManager.OnInitialize` (또는 `InitializeAsync`)에서 `m_uiManager.Initialize(m_soundManager)` 호출 확인.
2. `GameManager.OpenOptionPopup`에서 `popup.Initialize(m_soundManager, m_popupService)` 호출 확인. (**핵심: IPopupService 주입 필수**)
3. `ObjectPoolSpawner.Init` 또는 유사 메서드를 통해 `ISoundManager`가 전달되는지 확인.

**권장:** 싱글톤에 의존하지 않고 주입받은 서비스를 활용하도록 구현.

### Step 5: 초기화 순서 안전성 검사

Unity 라이프사이클(`OnEnable`/`Start`)에서 DI 의존 코드를 호출하지 않는지 확인합니다.

**원칙:** `OnEnable`/`Start`에서는 자체 컴포넌트 캐싱만 수행, 외부 의존성 로직은 `Initialize()`/`Init()`에서만 실행.

**파일:** `PlayerBase.cs`, `UIManager.cs`

**검사:**

```bash
# 1. PlayerBase.OnEnable에서 Init() 호출 없음 확인
grep -A5 "void OnEnable" Assets/_Game/Scripts/InGame/Player/Player_Base/PlayerBase.cs | grep "Init()"

# 2. UIManager.Start에서 SubscribeToEvents 호출 없음 확인
grep -A10 "void Start" Assets/_Game/Scripts/InGame/Manager/UIManager.cs | grep "SubscribeToEvents"
```

**PASS:** 두 검색 모두 결과 없음
**FAIL:** 라이프사이클에서 DI 의존 메서드 호출 발견
**수정:** DI 의존 코드를 `Initialize()` 또는 `Init()`으로 이동

### Step 6: async void 이벤트 핸들러 검사

이벤트 핸들러에서 `async void`를 사용하면 예외 발생 시 전체 이벤트 체인이 중단됩니다. `void` + `UniTaskVoid.Forget()` 패턴으로 대체해야 합니다.

**파일:** `Assets/_Game/Scripts/InGame/Manager/GameManager.cs`

**검사:**

```bash
grep "async void" Assets/_Game/Scripts/InGame/Manager/GameManager.cs
```

**PASS:** 결과 없음 (모든 비동기 핸들러가 void + Forget 패턴 사용)
**FAIL:** `async void` 메서드 존재
**수정:** `void Method() { MethodAsync().Forget(); }` + `async UniTaskVoid MethodAsync()` 패턴으로 변환

### Step 7: 사망 시퀀스 추적 로그 검사

`UIManager`의 게임 오버 시퀀스에 디버깅용 추적 로그가 구현되어 있는지 확인합니다.

**파일:** `UIManager.cs`

**검사:**

```bash
grep -n "OnGameOverAsync: Delay" Assets/_Game/Scripts/InGame/Manager/UIManager.cs
```

**PASS:** 상세 추적 로그 존재
**FAIL:** 로그 누락
**수정:** `OnGameOverAsync` 내 주요 단계에 추적 로그 추가

### Step 8: 레이스 컨디션 방지 및 초기화 대기 검사 (NEW)

`CompositionRoot` 클래스들이 리모트 데이터 동기화가 완료될 때까지 안전하게 대기하는지 확인합니다.

**파일:** `LobbySceneCompositionRoot.cs`, `GameSceneCompositionRoot.cs`

**검사:**

```bash
# 1. RemoteDataUpdateManager의 IsReady 플래그 대기 확인
grep -E "DefaultSceneInitializer\.WaitForRemoteInitialization\|m_remoteDataUpdateManager\.IsReady" Assets/_Game/Scripts/**/*.cs
```

**PASS:** 리모트 데이터 초기화 완료를 대기하는 `while` 루프나 `UniTask.WaitUntil` 로직이 존재함
**FAIL:** 대기 없이 바로 초기화 진행 (데이터 불일치 위험)

### Step 9: 조이스틱 부모 기준 경계 로직 검사

조이스틱 드래그 핸들러가 화면 전체가 아닌 **부모 RectTransform 내부**로 이동 범위를 제한하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/InGame/JoystickSetter/JoyStickPosDragandDrop.cs`

**검사:**

```bash
# 1. Canvas가 아닌 parentRect.rect를 사용하는지 확인
grep "parentRect.rect" Assets/_Game/Scripts/InGame/JoystickSetter/JoyStickPosDragandDrop.cs

# 2. 앵커(Anchor) 위치를 계산에 포함하는지 확인
grep "anchorPosInParent" Assets/_Game/Scripts/InGame/JoystickSetter/JoyStickPosDragandDrop.cs
```

**PASS:** `parentRect.rect`를 기준으로 `m_minBoundary`/`m_maxBoundary`를 계산하고, `anchorPosInParent` 오프셋을 적용함
**FAIL:** 여전히 `m_canvas`를 기준으로 전체 화면 이동을 허용하거나 앵커 보정 누락
**수정:** `CalculateBoundaries` 메서드 내 경계 계산식을 부모 `rect` 기반으로 변경

### Step 10: 자동 공격 토글 동기화 및 공격 트리거 검사 (NEW)

플레이어 스폰 시 UI 토글 상태가 명확히 전달되는지, 무기 획득 시 즉시 공격이 트리거되는지 확인합니다.

**파일:** `UIManager.cs`, `GameManager.cs`

**검사:**

```bash
# 1. OnPlayerChanged에서 UI 토글 상태 동기화 여부 확인
grep -A10 "OnPlayerChanged" Assets/_Game/Scripts/InGame/Manager/UIManager.cs | grep "AutoAttackEnabledByToggle"

# 2. EquipNewWeapon에서 즉시 Attack() 호출 여부 확인
grep -A20 "EquipNewWeapon" Assets/_Game/Scripts/InGame/Manager/GameManager.cs | grep "controller.Attack"
```

**PASS:** 참조 갱신 후 토글 값 할당 로직 및 획득 시 Attack 호출 존재
**FAIL:** 참조만 갱신하거나 강제 공격 로직 누락
**수정:** `OnPlayerChanged` 및 `EquipNewWeapon`에 최신 동기화/트리거 코드 보강

## Output Format

### 검증 결과

| 검사 항목 | 상태 | 파일/상세 |
|-----------|------|-----------|
| 싱글톤 의존성 | PASS | 초기화 단계에서만 사용됨 |
| 데이터 바인딩 | PASS | ViewModel 구독 확인됨 |
| 서버 업로드 무결성 | PASS | SaveGameResult 내 absolute upload(true) 확인 |
| Null 안전성 | PASS | `?.` 사용 발견되지 않음 |
| 초기화 안전성 | PASS | CompositionRoot 내 리모트 데이터 대기 로직 확인 |
| 조이스틱 경계 | PASS | 부모 Rect 기준 Clamp 및 앵커 보정 확인 |
| 서비스 주입 | PASS | GameManager 내 IPopupService 전파 확인 |
| 토글 동기화 | PASS | OnPlayerChanged 내 토글 상태 재전달 확인 |
| 공격 트리거 | PASS | 무기 획득 시 즉시 Attack() 호출 확인 |

## Exceptions

-   외부 라이브러리(`UniTask`, `UniRx` 등) 내부 코드나 플러그인 디렉토리는 검사에서 제외합니다.
-   POCO(Plain Old C# Object) 클래스 간의 참조에서는 `?.` 사용이 허용될 수 있으나, 가급적 명시적 체크를 권장합니다.
