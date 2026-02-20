---
name: verify-login-flow
description: 타이틀 씬의 로그인 흐름(MVVM), 어드레서블 로딩, 앱 업데이트 관리를 검증합니다.
---

# 로그인 흐름 검증

## Purpose

타이틀 씬에서 시작되는 게임 진입 프로세스의 안정성과 아키텍처 준수 여부를 검증합니다:

1.  **어드레서블 로딩** — `LoadAddressableManager`가 에셋을 정상적으로 로드하고 다음 씬으로 전환하는지 확인
2.  **로그인 MVVM** — `LoginViewMVVM`과 `LoginViewModel` 간의 데이터 바인딩 및 이벤트 구독 확인
3.  **인증 서비스 주입** — `LoginViewModel`이 `IAuthenticationService`를 통해 인증 로직을 수행하는지 확인
4.  **앱 업데이트 관리** — `AppUpdateManager`가 시작 시 업데이트 가용성을 체크하는지 확인
5.  **페이로드 전달** — 로그인 성공 후 `ScenePayloadDTO`를 통해 로비로 데이터를 올바르게 전달하는지 확인

## When to Run

-   타이틀 씬의 UI나 로그인 로직을 수정한 후
-   어드레서블 에셋 설정이나 로딩 순서를 변경했을 때
-   앱 업데이트(`AppUpdateManager`) 관련 로직을 수정했을 때

## Related Files

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/Title/LoadAddresaableManager.cs` | 어드레서블 로딩 매니저 |
| `Assets/_Game/Scripts/Title/LoginViewMVVM.cs` | 로그인 뷰 (MVVM View) |
| `Assets/_Game/Scripts/Title/LoginViewModel.cs` | 로그인 뷰모델 (MVVM ViewModel) |
| `Assets/_Game/Scripts/Manager/AppUpdateManager.cs` | 앱 업데이트 관리자 |
| `Assets/_Game/Scripts/Data/DTOs/ScenePayloadDTO.cs` | 씬 전환 통합 페이로드 |

## Workflow

### Step 1: 어드레서블 로딩 및 싱글톤 검사

`LoadAddressableManager`의 구현 방식과 코딩 표준 준수 여부를 확인합니다.

**파일:** `Assets/_Game/Scripts/Title/LoadAddresaableManager.cs`

**검사:**

```bash
# 1. 클래스 명칭 오타 확인 (LoadAddresaableManager -> LoadAddressableManager 권장되나 현재 명칭 확인)
grep "class LoadAddresaableManager" Assets/_Game/Scripts/Title/LoadAddresaableManager.cs

# 2. 어드레서블 초기화 호출 확인
grep "Addressables.InitializeAsync()" Assets/_Game/Scripts/Title/LoadAddresaableManager.cs
```

**PASS:** 초기화 로직이 존재함
**FAIL:** 어드레서블 로딩 로직이 없거나 명칭이 일치하지 않음

### Step 2: 로그인 MVVM 바인딩 및 DI 검사

View와 ViewModel 간의 결합도와 의존성 주입 구조를 확인합니다.

**파일:** `Assets/_Game/Scripts/Title/LoginViewMVVM.cs`, `Assets/_Game/Scripts/Title/LoginViewModel.cs`

**검사:**

```bash
# 1. ViewModel 생성 시 서비스 주입 확인
grep "m_viewModel = new LoginViewModel(m_serverManager.Auth)" Assets/_Game/Scripts/Title/LoginViewMVVM.cs

# 2. View에서 ViewModel 이벤트 구독 확인
grep "m_viewModel.OnLoginSuccess += OnLoginSuccess" Assets/_Game/Scripts/Title/LoginViewMVVM.cs

# 3. ViewModel이 IAuthenticationService를 사용하는지 확인
grep "private readonly IAuthenticationService m_authService" Assets/_Game/Scripts/Title/LoginViewModel.cs
```

**PASS:** 인터페이스 기반 DI 및 이벤트 바인딩이 확인됨
**FAIL:** 구체 클래스 직접 참조 또는 바인딩 누락
**수정:** `IAuthenticationService` 인터페이스 사용 및 `BindViewModel` 로직 보강

### Step 3: 초기화 시 앱 업데이트 체크 및 리모트 동기화 제외 확인

앱 시작 시 `AppUpdateManager`를 통해 업데이트를 확인하는지 검증하고, 불필요한 리모트 데이터 동기화가 중복 수행되지 않는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Title/LoginViewMVVM.cs`

**검사:**

```bash
# 1. 앱 업데이트 체크 확인
grep "await AppUpdateManager.Instance.CheckForUpdateAsync()" Assets/_Game/Scripts/Title/LoginViewMVVM.cs

# 2. 리모트 데이터 동기화 중복 수행 여부 확인 (제거/주석 처리 상태여야 함)
grep "UpdateAllRemoteDataAsync" Assets/_Game/Scripts/Title/LoginViewMVVM.cs
```

**PASS:** 업데이트 체크는 존재하고, 리모트 동기화 호출은 존재하지 않음(인게임으로 이전됨)
**FAIL:** 리모트 동기화가 여전히 타이틀에서 호출됨
**수정:** `LoginViewMVVM`에서 리모트 데이터 동기화 로직 제거 및 `GameManager`로 이동 확인

### Step 4: 로비 전환 페이로드 구성 검사

로그인 성공 후 로비로 전환할 때 `ScenePayloadDTO`를 올바르게 구성하는지 확인합니다.

**파일:** `Assets/_Game/Scripts/Title/LoginViewMVVM.cs`

**검사:**

```bash
grep "new ScenePayloadDTO(playerDto, sessionDto, m_soundManager)" Assets/_Game/Scripts/Title/LoginViewMVVM.cs
```

**PASS:** 플레이어 데이터, 세션, 사운드 서비스를 포함한 페이로드 생성 확인
**FAIL:** 단일 데이터만 전달하거나 수동으로 싱글톤 참조

## Output Format

### 로그인 흐름 검증 결과

| 검사 항목 | 상태 | 파일/상세 |
|-----------|------|-----------|
| 어드레서블 로딩 | PASS | LoadAddresaableManager 확인 |
| MVVM 바인딩 | PASS | LoginViewMVVM-LoginViewModel 연결됨 |
| 씬 전환 페이로드 | PASS | ScenePayloadDTO 사용 확인 |

## Exceptions

-   `LoadAddresaableManager`는 인프라 성격상 싱글톤 사용이 허용됨.
-   `AppUpdateManager`는 에디터나 iOS 환경에서는 동작하지 않으므로 플랫폼 분기(`if`) 처리 여부 확인.
