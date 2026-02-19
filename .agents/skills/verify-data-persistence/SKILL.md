---
name: verify-data-persistence
description: 플레이어 데이터의 DTO/Service 구조, 암호화 처리, 로컬 저장소 정합성을 검증합니다.
---

# 데이터 지속성 및 보안 검증

## Purpose

플레이어 데이터 레이어의 현대화된 아키텍처(DTO/Service/Encryption/Repository)가 안전하고 올바르게 동작하는지 검증합니다:

1.  **DTO 정합성** — `PlayerDataDTO`가 필드 위주로 구성되어 있고 로직을 포함하지 않는지 확인
2.  **서비스 의존성** — `PlayerDataService`가 `IPlayerDataService`를 구현하고 암호화/저장소 의존성을 명시적으로 주입받는지 확인
3.  **암호화 보안** — `EncryptionService`를 통해 데이터가 암호화되어 저장되는지, `PlayerPrefs`가 잔존하지 않는지 확인
4.  **명명 규칙** — DTO의 필드가 PascalCase 등 프로젝트 표준을 준수하는지 확인

## When to Run

-   플레이어 데이터 구조(`PlayerDataDTO.cs`)를 변경했을 때
-   저장/로드 로직(`PlayerDataService.cs`, `LocalPlayerDataRepository.cs`)을 수정했을 때
-   재화 및 경험치 등 핵심 데이터 처리 규칙이 변경되었을 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/Data/DTOs/PlayerDataDTO.cs` | 플레이어 데이터 전송 객체 (POCO) |
| `Assets/_Game/Scripts/Data/DTOs/ServerSessionDTO.cs` | 서버 서비스 세션 DTO (Auth/GameData/Post) |
| `Assets/_Game/Scripts/Data/DTOs/ScenePayloadDTO.cs` | 씬 전환 통합 페이로드 DTO |
| `Assets/_Game/Scripts/Data/Services/PlayerDataService.cs` | 데이터 관리 비즈니스 로직 서비스 |
| `Assets/_Game/Scripts/Data/Services/EncryptionService.cs` | RSA/AES 암호화 서비스 |
| `Assets/_Game/Scripts/Data/LocalPlayerDataRepository.cs` | 로컬 파일 시스템 저장소 |
| `Assets/_Game/Scripts/Data/ServerManager.cs` | 서버 서비스 팩토리 (싱글톤 제거 확인용) |

## Workflow

### Step 1: DTO 순수성 및 명명 규칙 검사

`PlayerDataDTO`가 데이터를 담는 용도로만 사용되는지, 필드명이 표준(PascalCase)을 따르는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Data/DTOs/PlayerDataDTO.cs`

**검사:**

```bash
# 1. MonoBehaviour 상속 여부 확인 (상속하지 않아야 함)
grep "class PlayerDataDTO : MonoBehaviour" Assets/_Game/Scripts/Data/DTOs/PlayerDataDTO.cs

# 2. 필드 명명 규칙 확인 (모두 PascalCase 여부)
# lowercase로 시작하는 public 필드가 있는지 탐색
grep "public [a-z]" Assets/_Game/Scripts/Data/DTOs/PlayerDataDTO.cs
```

**PASS:** `MonoBehaviour`를 상속받지 않으며, 모든 public 필드가 대문자로 시작함
**FAIL:** `MonoBehaviour` 잔존 또는 camelCase 필드 존재
**수정:** POCO로 변경하고 필드명 일괄 수정

### Step 2: 서비스 의존성 주입 확인

`PlayerDataService`의 생성자가 필요한 의존성을 모두 주입받고 있는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Data/Services/PlayerDataService.cs`

**검사:**

```bash
grep "public PlayerDataService(" Assets/_Game/Scripts/Data/Services/PlayerDataService.cs
```

**PASS:** `EncryptionService` 및 `LocalPlayerDataRepository` 매개변수 존재
**FAIL:** 기본 생성자만 있거나 싱글톤 참조 사용
**수정:** Constructor Injection 패턴 적용

### Step 3: ScenePayloadDTO 통합 초기화 검사

`LobbyUIViewManager` 및 `GameManager` 등 주요 매니저들이 `ScenePayloadDTO`를 통해 데이터를 수신하고 초기화하는지 확인합니다.

**검사:**

```bash
# 1. GameManager에서의 페이로드 캐스팅 확인
grep "if (payload is ScenePayloadDTO scenePayload)" Assets/_Game/Scripts/InGame/Manager/GameManager.cs

# 2. LobbyUIViewManager에서의 페이로드 캐스팅 확인 (InGame.Data 네임스페이스 포함)
grep "if (payload is InGame.Data.ScenePayloadDTO scenePayload)" Assets/_Game/Scripts/Lobby/LobbyUIViewManager.cs
```

**PASS:** 두 파일 모두에서 `ScenePayloadDTO`를 사용한 조건문이 검색됨
**FAIL:** 검색되지 않거나 구식 방식으로 초기화 중
**수정:** `OnInitialize(object payload)` 메서드 내에서 `ScenePayloadDTO` 캐스팅 로직 구현

### Step 4: ServerManager 싱글톤 제거 및 팩토리 확인

`ServerManager`가 싱글톤을 사용하지 않고, 팩토리로서 세션을 제공하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Data/ServerManager.cs`

**검사:**

```bash
# 1. Instance 필드/프로퍼티 부재 확인 (외부에서 인스턴스에 직접 접근하지 못해야 함)
grep "public static ServerManager Instance" Assets/_Game/Scripts/Data/ServerManager.cs

# 2. GetSession 메서드 존재 확인
grep "public ServerSessionDTO GetSession()" Assets/_Game/Scripts/Data/ServerManager.cs
```

**PASS:** `Instance`가 검색되지 않거나(또는 비공개), `GetSession`이 존재함
**FAIL:** `Instance`가 public으로 남아있거나 `GetSession`이 없음
**수정:** 싱글톤 패턴 제거 및 세션 반환 메서드 구현

## Output Format

### 검증 결과

| 검사 항목 | 상태 | 상세 |
|-----------|------|------|
| DTO 정합성 | PASS | POCO 및 PascalCase 준수 |
| 서비스 주입 | PASS | DI 패턴 적용 확인 |
| 보안성 | PASS | PlayerPrefs 완전 제거됨 |

## Exceptions

-   `PlayerPrefs`는 볼륨 설정이나 간단한 옵션 데이터에는 예외적으로 허용될 수 있으나, 게임 진행 데이터에는 금지합니다.
-   타사 SDK 내부에서 사용하는 `PlayerPrefs`는 검사에서 제외합니다.
