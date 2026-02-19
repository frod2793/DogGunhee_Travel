---
name: verify-inventory-system
description: 인벤토리의 데이터 구조(ItemDatabaseSO)와 환전(판매) 로직의 구현 상태를 검증합니다.
---

# 인벤토리 시스템 검증

## Purpose

인벤토리 시스템 리팩토링의 핵심 목표인 '환전(판매)' 기능과 '레거시 제거(데이터베이스화)'가 올바르게 수행되었는지 검증합니다:

1.  **데이터 구조** — `ItemDatabaseSO`가 존재하고, 기존 `InventoryDataManager`의 역할을 대체하는지 확인
2.  **환전 로직** — `InventorySystem.SellItem` 메서드가 구현되어 있고, `PlayerDataService.AddCurrency`와 연결되는지 확인
3.  **UI 연동** — `InventoryViewModel`에서 판매 명령(`SellItem`)을 호출하고 UI와 바인딩되는지 확인
4.  **레거시 청산** — `InventoryDataManager`의 사용처가 제거되었거나, 파일 자체가 삭제되었는지 확인

## When to Run

-   인벤토리 환전 기능을 구현한 후
-   `InventoryDataManager`의 의존성을 제거하는 리팩토링을 수행한 후
-   상점이나 인벤토리 관련 데이터 로직을 변경한 후

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/ScriptableObjects/ItemDatabaseSO.cs` | 신규 아이템 데이터베이스 |
| `Assets/_Game/Scripts/ScriptableObjects/InventorySystem.cs` | 인벤토리 비즈니스 로직 (판매 기능 추가) |
| `Assets/_Game/Scripts/ScriptableObjects/InventoryDataSO.cs` | 인벤토리 데이터 구조 (SO) |
| `Assets/_Game/Scripts/ScriptableObjects/InventoryPersistence.cs` | 인벤토리 데이터 영속성 (서버 통신) |
| `Assets/_Game/Scripts/Lobby/InventoryManager.cs` | 전역 인벤토리 관리자 (DI 적용) |
| `Assets/_Game/Scripts/Lobby/ViewModels/InventoryViewModel.cs` | 인벤토리 뷰모델 (판매 명령) |
| `Assets/_Game/Scripts/Lobby/UI/Popups/InventoryView.cs` | 인벤토리 팝업 UI (View) |

## Workflow

### Step 1: 데이터베이스 및 데이터 SO 존재 확인

신규 데이터 구조인 `ItemDatabaseSO.cs`와 `InventoryDataSO.cs` 파일이 생성되었는지 확인합니다.

**파일:** `Assets/_Game/Scripts/ScriptableObjects/`

**검사:**

```bash
ls Assets/_Game/Scripts/ScriptableObjects/ItemDatabaseSO.cs
ls Assets/_Game/Scripts/ScriptableObjects/InventoryDataSO.cs
```

**PASS:** 모든 파일 존재
**FAIL:** 특정 파일 없음
**수정:** 누락된 SO 클래스 생성 및 구현

### Step 2: 판매 로직 구현 확인

`InventorySystem` 클래스 내에 `SellItem` 메서드가 존재하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/ScriptableObjects/InventorySystem.cs`

**검사:**

```bash
grep "SellItem" Assets/_Game/Scripts/ScriptableObjects/InventorySystem.cs
```

**PASS:** `SellItem` 메서드 정의 확인
**FAIL:** 메서드 없음
**수정:** `InventorySystem`에 아이템 판매 및 재화 가산 로직 구현

### Step 3: 레거시 매니저 잔존 여부 확인

리팩토링 전 사용되었던 레거시 매니저 클래스들이 정해진 위치에서 제거되었는지 확인합니다.

**검사:**

```bash
# 1. 이전 경로의 파일 존재 여부 확인
ls Assets/InventoryDataManager.cs 2>/dev/null
ls Assets/_Game/Scripts/ScriptableOJB/InventoryDataManagerDontdestory.cs 2>/dev/null

# 2. 코드 내 참조 확인
grep -r "InventoryDataManager" Assets/_Game/Scripts/
```

**PASS:** 파일이 존재하지 않고 검색 결과가 없거나 주석만 존재함
**FAIL:** 레거시 파일이 남아있거나 활성 코드에서 참조됨
**수정:** 레거시 파일 삭제 및 참조 코드를 `InventorySystem` 등으로 이관

### Step 4: 서비스 의존성 주입 확인 (DI)

`InventoryPersistence` 및 `InventoryManager`가 서버 서비스를 주입받는지 확인합니다.

**검사:**

```bash
# 1. InventoryPersistence 생성자 주입 확인
grep "public InventoryPersistence(InGame.Services.ILogService logService, IGameDataService gameDataService)" Assets/_Game/Scripts/ScriptableObjects/InventoryPersistence.cs

# 2. InventoryManager.Init 메서드 확인
grep "public void Init(IGameDataService gameDataService)" Assets/_Game/Scripts/Lobby/InventoryManager.cs
```

**PASS:** 명시적인 서비스 주입 인터페이스가 존재함
**FAIL:** `ServerManager.Instance`에 직접 의존함
**수정:** 리팩토링된 DI 패턴 적용 (Init 메서드 및 생성자 수정)

## Output Format

### 검증 결과

| 검사 항목 | 상태 | 파일/상세 |
|-----------|------|-----------|
| 데이터베이스 SO | PASS | 파일 존재함 |
| 판매 로직 | PASS | SellItem 구현됨 |
| 레거시 의존성 | WARNING | 아직 3곳에서 참조 중 |

## Exceptions

-   리팩토링 과도기에는 `InventoryDataManager`가 존재할 수 있으나, 최종적으로는 삭제되어야 함.
