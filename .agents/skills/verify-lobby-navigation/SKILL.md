---
name: verify-lobby-navigation
description: 로비 UI의 팝업 관리 및 뒤로가기(ESC) 로직을 검증합니다. PopupManager 등록 및 중복 입력 처리 여부를 확인합니다.
---

# 로비 내비게이션 검증

## Purpose

로비 UI 시스템에서 팝업의 열기/닫기 동작이 중앙화된 `PopupManager`를 통해 관리되는지 검증합니다:

1.  **팝업 등록** — 모든 팝업이 열릴 때 `PopupManager.RegisterPopup`을 호출하는지 확인
2.  **팝업 닫기** — 닫기 로직이 `PopupManager.CloseTopPopup`과 연동되는지 확인
3.  **입력 중복 방지** — 개별 UI 스크립트에서 `Input.GetKeyDown(KeyCode.Escape)`를 직접 처리하지 않는지 확인
4.  **초기화 정합성** — `LobbyUIViewManager`가 `ISceneInitializer`를 통해 `PlayerDataDTO`를 정상적으로 주입받는지 확인
5.  **서비스 주입 권장** — 팝업 및 내비게이터 초기화 시 `ISoundManager`를 주입받아 사운드를 제어하는지 확인(권장)

## When to Run

-   로비 UI 관련 스크립트(`View`, `ViewModel`)를 수정한 후
-   새로운 팝업 UI를 추가한 후
-   뒤로가기 입력이 동작하지 않거나 팝업이 꼬이는 현상이 발생할 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/Lobby/Core/UI/PopupManager.cs` | 팝업 스택 및 입력 처리 관리자 |
| `Assets/_Game/Scripts/Lobby/LobbyUIViewManager.cs` | 로비 UI 전체 관리자 |
| `Assets/_Game/Scripts/Lobby/Core/LobbyNavigator.cs` | 로비 내비게이션 구현체 |
| `Assets/_Game/Scripts/Lobby/Core/LobbySubSystemService.cs` | 서브시스템 초기화 서비스 |
| `Assets/_Game/Scripts/Lobby/LobbyViewModel.cs` | 로비 메인 뷰모델 |
| `Assets/_Game/Scripts/Lobby/UI/Popups/InventoryView.cs` | 인벤토리 팝업 뷰 |
| `Assets/_Game/Scripts/Lobby/UI/Popups/PostView.cs` | 우편함 팝업 뷰 |
| `Assets/_Game/Scripts/Lobby/UI/Popups/QuestInfoView.cs` | 퀘스트 정보 팝업 뷰 |
| `Assets/_Game/Scripts/Lobby/UI/Popups/StoreView.cs` | 상점 팝업 뷰 |
| `Assets/_Game/Scripts/Lobby/OptionPopupView.cs` | 옵션 팝업 뷰 (PopupManager 연동) |
| `Assets/_Game/Scripts/Data/DTOs/ScenePayloadDTO.cs` | 씬 전환 통합 페이로드 DTO |
| `Assets/_Game/Scripts/Lobby/ChoosegamePopup.cs` | 게임 선택 팝업 (중복 로딩 방지 적용) |

## Workflow

### Step 1: 개별 ESC 입력 처리 검사

모든 로비 UI 스크립트에서 `Input.GetKeyDown(KeyCode.Escape)`를 직접 호출하는지 검사합니다. 이는 `PopupManager`와 충돌을 일으킬 수 있으므로 제거해야 합니다.

**파일:** `Assets/_Game/Scripts/Lobby/**/*.cs`

**검사:**

```bash
grep -r "Input.GetKeyDown(KeyCode.Escape)" Assets/_Game/Scripts/Lobby
```

**PASS:** 검색 결과가 없어야 함 (단, `PopupManager` 제외)
**FAIL:** 검색 결과가 존재함
**수정:** 해당 로직을 제거하고 `PopupManager`에 위임하거나, `PopupManager`의 이벤트를 구독하도록 변경

### Step 2: 팝업 등록 검사

주요 팝업 뷰가 열릴 때 `PopupManager.RegisterPopup`을 호출하는지 검사합니다.

**파일:** `InventoryView.cs`, `PostView.cs`, `QuestInfoView.cs`, `StoreView.cs` 등

**검사:**

```bash
grep -r "PopupManager.Instance.RegisterPopup" Assets/_Game/Scripts/Lobby/UI/Popups
```

**PASS:** 각 팝업 스크립트 파일 내에서 `RegisterPopup` 호출이 확인됨 (또는 팝업을 생성하는 매니저(`GameManager`, `LobbyNavigator`)에서 호출됨)
**FAIL:** 특정 팝업 스크립트에서 호출이 누락되어 ESC 키로 닫히지 않음
**수정:** `Open` 관련 메서드 내에 `RegisterPopup(CloseMethod)` 추가 또는 생성 주체에서 등록 수행

### Step 3: 코어 아키텍처 인터페이스 주입 검사

`LobbyNavigator` 및 `LobbySubSystemService`가 인터페이스 기반으로 구현되고 주입되는지 확인합니다.

**파일:** `LobbyNavigator.cs`, `LobbySubSystemService.cs`, `LobbyViewModel.cs`

**검사:**

```bash
# 1. LobbyNavigator가 ILobbyNavigator를 구현하는지 확인
grep "class LobbyNavigator : ILobbyNavigator" Assets/_Game/Scripts/Lobby/Core/LobbyNavigator.cs

# 2. LobbyViewModel이 생성자에서 인터페이스를 주입받는지 확인
grep "public LobbyViewModel(ILobbyNavigator lobbyNavigator, ILobbySubSystemService lobbySubSystemService)" Assets/_Game/Scripts/Lobby/LobbyViewModel.cs
```

**PASS:** 위 구문들이 정확히 일치함
**FAIL:** 구체 클래스에 의존하거나 주입 방식이 아님
**수정:** 인터페이스 추상화 및 Constructor Injection 적용

### Step 4: 통합 페이로드 초기화 검사

`LobbyUIViewManager`가 `ScenePayloadDTO`를 통해 플레이어 및 서버 세션 데이터를 정상적으로 수신하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Lobby/LobbyUIViewManager.cs`

**검사:**

```bash
# OnInitialize에서 ScenePayloadDTO 캐스팅 및 데이터 할당 확인
grep "payload is InGame.Data.ScenePayloadDTO scenePayload" Assets/_Game/Scripts/Lobby/LobbyUIViewManager.cs
```

**PASS:** `ScenePayloadDTO`를 사용하여 `m_playerData`와 `m_serverSession`을 초기화함
**FAIL:** 여전히 `PlayerDataDTO` 단일 객체만 기대하거나 수동으로 싱글톤 참조
**수정:** `ScenePayloadDTO` 기반의 초기화 로직으로 변경

### Step 5: 사운드 서비스 주입 확인 (권장사항)

`LobbyNavigator` 및 하위 팝업(`OptionPopupView` 등)이 `ISoundManager`를 주입받아 사용하는지 확인합니다.

**파일:** `LobbyNavigator.cs`, `OptionPopupView.cs`

**검사:**
1. `LobbyNavigator` 생성자에서 `ISoundManager`를 인자로 받는지 확인.
2. `OptionPopupView.Initialize`에서 `ISoundManager`를 전달받는지 확인.

**권장:** 싱글톤(`SoundManager.Instance`)에 의존하지 않고 주입받은 인스턴스 사용 권장.

### Step 6: 씬 로딩 정밀성 및 서버 데이터 로딩 검사

`SceneLoader`가 정확한 씬 범위를 검색하는지, 로비 진입 시 서버 데이터 동기화를 대기하는지 확인합니다.

**파일:** `SceneLoader.cs`, `LobbySceneCompositionRoot.cs`

**검사:**

```bash
# 1. SceneLoader의 RootGameObjects 기반 검색 로직 확인
grep "loadedScene.GetRootGameObjects()" Assets/_Game/Scripts/Lobby/SceneLoader.cs

# 2. LobbySceneCompositionRoot에서 서버 데이터 로딩(await) 대기 확인
grep "await playerService.LoadFromServerAsync()" Assets/_Game/Scripts/Lobby/Core/LobbySceneCompositionRoot.cs
```

**PASS:** `GetRootGameObjects()`를 통한 정밀 검색 확인, 로비 진입 전 서버 데이터 완결 대기(`await`) 확인
**FAIL:** 전체 객체 검색(`FindObjectsByType`) 잔존 또는 서버 데이터 로드 대기 누락
**수정:** 씬 로더 루프 개선 및 `OnInitialize` 내 `await` 추가

### Step 7: OptionPopupView NRE 방지 및 폴백 검사

옵션 팝업 종료 시 `m_popupService`가 없을 때(에디터 단독 실행 등) NRE가 발생하지 않고 폴백 로직이 작동하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Lobby/OptionPopupView.cs`

**검사:**

```bash
# m_popupService null 체크 후 CloseTopPopup() 호출 및 else 블록의 Destroy 확인
grep -A 5 "if (m_popupService != null)" Assets/_Game/Scripts/Lobby/OptionPopupView.cs | grep -E "CloseTopPopup|Destroy"
```

**PASS:** `if (m_popupService != null)` 체크와 함께 `CloseTopPopup()` 및 폴백 `Destroy`가 모두 확인됨
**FAIL:** Null 체크 없이 바로 `CloseTopPopup()` 호출
**수정:** `m_popupService` 호출 전 Null 체크 및 자체 파괴 로직 추가

### Step 8: SceneLoader 전역 로딩 가드 검사

씬 로딩이 한 번에 하나만 진행되도록 `SceneLoader`에 전역 가드가 구현되어 있는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Lobby/SceneLoader.cs`

**검사:**

```bash
# m_isLoading 플래그 선언 및 LoadSceneAsync에서의 체크 로직 확인
grep -E "bool m_isLoading|if (m_isLoading)" Assets/_Game/Scripts/Lobby/SceneLoader.cs
```

**PASS:** `m_isLoading` 필드와 이를 통한 중복 로딩 조기 리턴(LogWarning 포함) 로직 존재
**FAIL:** 전역 가드 누락 (중복 호출 시 예외 발생 또는 중첩 로딩 가능성)
**수정:** `bool m_isLoading` 변수 추가 및 `LoadSceneAsync` 시작 부분에 체크 로직 삽입

## Output Format

### 검증 결과

| 검사 항목 | 상태 | 파일/상세 |
|-----------|------|-----------|
| 개별 ESC 입력 | PASS | 발견되지 않음 |
| 팝업 등록 | PASS | InventoryView, OptionPopupView 등 확인됨 |
| 씬 로딩 정밀성 | PASS | RootObjects 기반 타겟팅 확인 |
| 서버 데이터 동기화 | PASS | 로비 진입 전 LoadFromServerAsync 대기 |
| NRE 방지 가드 | PASS | OptionPopupView 폴백 로직 확인 |
| 전역 로딩 가드 | PASS | SceneLoader m_isLoading 가드 확인 |

## Exceptions

-   `PopupManager.cs`: 입력 처리를 담당하는 주체이므로 `Input.GetKeyDown` 사용 가능.
-   일시적인 테스트 코드나 디버그용 스크립트는 예외 (단, 주석으로 명시되어야 함).
