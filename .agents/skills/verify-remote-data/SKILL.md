---
name: verify-remote-data
description: 구글 시트(GAS) 기반 리모트 데이터 동기화 시스템의 정합성을 검증합니다.
---

# 리모트 데이터 동기화 시스템 검증

## Purpose

구글 시트에서 JSON 데이터를 실시간으로 가져와 게임 데이터에 반영하는 시스템의 안정성을 검증합니다:

1.  **서비스 URL 유효성** — `RemoteDataService`에 설정된 GAS Web App URL이 유효하고 배포된 상태인지 확인
2.  **데이터 로딩 패턴** — `SkillDatabase` 및 `StageDatabase`가 JSON 데이터를 통해 에셋(WeaponDataSO 등)을 올바르게 동기화하는지 확인
3.  **로컬 캐싱 정합성** — 다운로드된 JSON이 `persistentDataPath/DataCache`에 올바른 명칭으로 저장되고 관리되는지 확인
4.  **DTO 구조 일치** — `SheetDataDTO`의 필드명과 구글 시트의 컬럼명이 일치하여 파싱 오류가 없는지 확인

## When to Run

-   구글 시트 구조(컬럼 추가/삭제)를 변경한 후
-   GAS 스크립트를 새로 배포하여 Web App URL이 변경되었을 때
-   `SkillDatabase` 또는 `RemoteDataService` 로직을 수정한 후
-   데이터 파싱 관련 오류(JSON Parsing Error)가 발생했을 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/Data/Services/RemoteDataService.cs` | 구글 서버 통신 및 버전 체크 |
| `Assets/_Game/Scripts/Data/Managers/RemoteDataUpdateManager.cs` | 데이터 동기화 흐름 제어 |
| `Assets/_Game/Scripts/ScriptableObjects/SkillDatabase.cs` | 스킬/무기 데이터 에셋 동기화 |
| `Assets/_Game/Scripts/ScriptableObjects/StageDatabase.cs` | 스테이지/웨이브 데이터 로딩 |
| `Assets/_Game/Scripts/Data/DTOs/SheetDataDTO.cs` | 데이터 전송 객체 구조 정의 |

## Workflow

### Step 1: GAS URL 및 배포 상태 확인

`RemoteDataService`에 설정된 URL이 배포된 Web App 형식인지 확인합니다.

**파일:** `Assets/_Game/Scripts/Data/Services/RemoteDataService.cs`

**검사:**

```bash
grep "k_BaseUrl =" Assets/_Game/Scripts/Data/Services/RemoteDataService.cs
```

**PASS:** `https://script.google.com/macros/s/.../exec` 형식이 포함됨
**FAIL:** URL이 비어있거나 구형 배포 링크임
**수정:** 최신 GAS 배포 URL로 업데이트

### Step 2: 무기 스탯 동기화 명칭 검사

`SkillDatabase`에서 시트의 컬럼명(DTO)과 실제 `WeaponDataSO` 프로퍼티 매칭이 올바른지 확인합니다. 특히 최근 변경된 `Range` 등을 점검합니다.

**파일:** `Assets/_Game/Scripts/ScriptableObjects/SkillDatabase.cs`

**검사:**

```bash
grep -E "(Damage|Cooldown|AttackSpeed|Range|Duration|ProjectileCount)" Assets/_Game/Scripts/ScriptableObjects/SkillDatabase.cs
```

**PASS:** `"Range"` 컬럼이 `BaseAttackRange`와 매칭됨 (구 "WeaponSize" 사용 금지)
**FAIL:** 구형 컬럼명(WeaponSize 등)이 잔존함
**수정:** 시트 컬럼명에 맞춰 매칭 로직 업데이트

### Step 3: 로컬 캐시 경로 및 파일 확장자 확인

데이터가 로컬에 저장될 때 `.json` 확장자를 사용하고 올바른 폴더에 담기는지 확인합니다.

**파일:** `SkillDatabase.cs`, `StageDatabase.cs`

**검사:**

```bash
grep "DataCache" Assets/_Game/Scripts/ScriptableObjects/*.cs
```

**PASS:** `DataCache` 폴더 내에 `.json` 파일로 저장/로드함
**FAIL:** 다른 확장자나 상이한 경로 사용
**수정:** `Application.persistentDataPath/DataCache`로 경로 통일

### Step 4: DTO 클래스 구조 검사

`JsonUtility`가 파싱할 수 있도록 DTO 클래스와 필드가 구성되어 있는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Data/DTOs/SheetDataDTO.cs`

**검사:**

```bash
grep "public class" Assets/_Game/Scripts/Data/DTOs/SheetDataDTO.cs
```

**PASS:** `StatValueDTO`, `SkillDescriptionDTO` 등이 정의되어 있음
**FAIL:** 필수 DTO 클래스 누락

### Step 5: 강제 동기화 및 에디터 폴백 검사

초기 로딩 시 구글 시트 데이터의 즉각 반영을 위해 `force: true`를 사용하는지, 그리고 에디터 직접 실행 시 `GameManager`에서 동기화를 수행하는지 확인합니다.

**파일:** `LoadAddresaableManager.cs`, `GameManager.cs`

**검사:**
1. `LoadAddresaableManager`에서 `UpdateAllRemoteDataAsync(force: true)` 호출 확인.
2. `GameManager.InitializeAsync` 내에 `#if UNITY_EDITOR`를 활용한 리모트 데이터 동기화 폴백 존재 확인.

```bash
grep "UpdateAllRemoteDataAsync(force: true)" Assets/_Game/Scripts/Title/LoadAddresaableManager.cs
grep -A 10 "UNITY_EDITOR" Assets/_Game/Scripts/InGame/Manager/GameManager.cs | grep "RemoteDataUpdateManager"
```

**PASS:** 강제 동기화 및 에디터 폴백 로직 존재
**FAIL:** 데이터 정합성 누락 가능성 있음

### Step 6: 모바일 안정성 및 씬 초기화 대기 검사

`RemoteDataService`의 타임아웃 처리와 `RemoteDataUpdateManager`가 씬 로더와 정상 연동되는지 확인합니다.

**파일:** `RemoteDataService.cs`, `RemoteDataUpdateManager.cs`

**검사:**

```bash
# 1. RemoteDataService의 모바일 타임아웃(15초) 적용 확인
grep "k_MobileTimeoutSeconds = 15" Assets/_Game/Scripts/Data/Services/RemoteDataService.cs

# 2. RemoteDataUpdateManager의 ISceneInitializer 구현 여부 확인
grep "class RemoteDataUpdateManager : MonoBehaviour, IRemoteDataUpdateService, InGame.Core.ISceneInitializer" Assets/_Game/Scripts/Data/Managers/RemoteDataUpdateManager.cs
```

**PASS:** 타임아웃 15초 설정 확인됨, `ISceneInitializer`를 통한 씬 로딩 대기 루프 참여 확인
**FAIL:** 무한 대기 위험 또는 씬 초기화 인터페이스 미구현
**수정:** 타임아웃 로직 추가 및 `ISceneInitializer` 인터페이스 구현

## Output Format

### 리모트 데이터 시스템 검증 결과

| 검사 항목 | 상태 | 파일/상세 |
|-----------|------|-----------|
| GAS URL 유효성 | PASS | 최신 배포 URL 확인됨 |
| 스탯 매칭 정합성 | PASS | Range/Duration 매칭 완료 |
| 모바일 안정성 | PASS | 15초 타임아웃 및 네트워크 체크 적용 |
| 씬 초기화 대기 | PASS | ISceneInitializer를 통한 안정적 대기 |
| 로컬 캐싱 경로 | PASS | DataCache/.json 사용 중 |

## Exceptions

-   에디터 전용 동기화(`UNITY_EDITOR`) 로직은 빌드 포함 여부에 따라 분기 처리되어야 하므로, 빌드 시 에러 여부만 확인합니다.
-   네트워크 미연결 시에는 로컬 캐시를 우선 사용하므로, 오프라인 상태에서의 로컬 로딩 작동 여부를 예외로 두지 않고 필수 체크합니다.
