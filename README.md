<div align="center">

  # 🚀 모바일 로그라이크 RPG: Project "DogGunhee_Travel"
  
  **수많은 적들과의 대규모 전투 속에서도 안정적인 라이브 서비스를 제공하는 모바일 게임**
  
</div>

<br>

## 🎯 프로젝트 목표
본 프로젝트는 다음 세 가지 핵심 가치를 목표로 설계되었습니다.
- **유연한 라이브 서비스:** 스토어 업데이트 없이 콘텐츠를 추가하고 관리할 수 있는 환경 구축.
- **최적화된 런타임 성능:** 수백 개의 오브젝트가 등장하는 대규모 전투에서도 안정적인 프레임 확보.
- **안전한 데이터 관리:** 사용자의 게임 데이터를 위변조로부터 안전하게 보호하고 영속성을 보장.

---

## 🛠️ 주요 기술 및 구현 상세

### 1. 유연한 라이브 서비스 및 비동기 로딩 파이프라인
**Addressable Asset System**을 도입하여 캐릭터, 몬스터, 아이템 등 모든 게임 리소스를 원격으로 관리합니다. 이를 통해 앱 재설치 없이 신규 콘텐츠를 즉시 패치할 수 있는 라이브 서비스 환경을 구축했습니다.

**강점: 끊김 없는 사용자 경험 (Seamless UX)**
Unity에 최적화된 **UniTask**를 활용하여 에셋 로딩 파이프라인을 비동기적으로 구축했습니다. 전투 중에도 프리징 현상 없이 필요한 리소스를 실시간으로 불러와 부드러운 게임 플레이를 제공합니다.

```csharp
// UniTask와 Addressables를 활용한 캐릭터 스폰 (GameManager.cs)
private async UniTask SpawnPlayerAndInitialWeaponsAsync()
{
    if (m_playerContainer == null) return;

    try
    {
        // 최신 데이터 매니저 참조 (싱글톤)
        int charIndex = PlayerDataManager.Instance.SelectCharacterIndex;
        string charKey = $"Player_Character_{charIndex}"; // Addressable Key

        // 비동기로 캐릭터 프리팹 로드 및 생성
        GameObject charInstance = await Addressables
            .InstantiateAsync(charKey, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform)
            .ToUniTask();

        if (charInstance == null) return;

        charInstance.transform.localPosition = Vector3.zero;
        SpawnedPlayer = charInstance.GetComponent<PlayerBase>();

        // ... (무기 초기화 및 이벤트 연결)

        if (m_playerController != null)
        {
            m_playerController.AssignCharacter(SpawnedPlayer);
        }

        OnPlayerChanged?.Invoke(SpawnedPlayer);
    }
    catch (Exception ex)
    {
        LogManager.LogError($"[GameManager] 스폰 과정에서 오류 발생: {ex.Message}");
        SpawnedPlayer = null;
    }
}
```

### 2. 대규모 전투를 위한 런타임 성능 최적화
뱀파이어 서바이버즈 장르 특성상 수많은 몬스터와 투사체가 등장합니다. 이를 효율적으로 처리하기 위해 다음과 같은 최적화 기법을 적용했습니다.

- **오브젝트 풀링 (Object Pooling):** `Unity.ObjectPool`을 활용하여 가비지 컬렉션(GC) 스파이크를 방지하고 메모리 할당을 최소화했습니다.
- **팩토리 패턴 (Factory Pattern):** 객체 생성과 사용 로직을 분리하여 유지보수성을 높였습니다.
- **타겟 프레임 최적화:** 안드로이드 환경에서 `Application.targetFrameRate`를 120으로 설정하여 부드러운 화면을 제공하며, `SleepTimeout.NeverSleep`을 적용하여 화면 꺼짐을 방지했습니다.
- **이펙트 처리 최적화:** 적 피격 시 발생하는 플래시 이펙트의 처리 병목을 제거하기 위해 순차적 큐(Queue) 방식에서 **즉시 실행(Parallel)** 방식으로 변경하여, 수십 마리의 적을 동시에 타격해도 밀림 없는 시각 효과를 구현했습니다.

```csharp
// 오브젝트 풀을 활용하여 획득한 경험치 오브젝트를 반환하는 코드 (PlayerCollisionHandler.cs)
private void HandleExpCollision(GameObject expObject)
{
    // TryGetComponent로 컴포넌트 캐싱 및 유효성 검사 (Garbage Free)
    if (expObject.TryGetComponent(out EXP_Obj expObj))
    {
        // 이벤트 기반 데이터 전달 (Decoupling)
        OnExpCollected?.Invoke(expObj.ExpValue);
        
        // 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
        }

        // 사용이 끝난 오브젝트를 풀로 반환 (Destroy 호출 방지 -> GC 최소화)
        if (expObj.ObjectPoolSpawner != null && expObj.ObjectPoolSpawner.ExpObjectPool != null)
        {
            expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
        }
    }
}
```

### 3. 안전한 데이터 관리 및 보안
**AES-256 + RSA 하이브리드 암호화**를 적용하여 로컬 데이터를 강력하게 보호하고, **뒤끝(TheBackend)** 서버와 연동하여 데이터 영속성을 보장합니다.

```csharp
// 하이브리드 암호화 핵심 로직 (HybridEncryption.cs)
public EncryptedPacket Encrypt(string plainJson, string publicKey)
{
    using (var aes = Aes.Create())
    {
        // 1. AES 세션키 및 IV(초기화 벡터) 생성
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();
        byte[] sessionKey = aes.Key;
        byte[] iv = aes.IV;

        // 2. 데이터 암호화 (AES 대칭키 사용)
        byte[] encryptedData;
        using (var encryptor = aes.CreateEncryptor(sessionKey, iv))
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainJson);
            encryptedData = PerformCryptography(plainBytes, encryptor);
        }

        // 3. 세션키 암호화 (RSA 공개키 사용)
        byte[] encryptedSessionKey;
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(publicKey);
            // AES 키와 IV를 결합하여 RSA로 암호화
            byte[] keyAndIv = new byte[sessionKey.Length + iv.Length];
            Buffer.BlockCopy(sessionKey, 0, keyAndIv, 0, sessionKey.Length);
            Buffer.BlockCopy(iv, 0, keyAndIv, sessionKey.Length, iv.Length);
            encryptedSessionKey = rsa.Encrypt(keyAndIv, true);
        }

        return new EncryptedPacket(encryptedSessionKey, encryptedData);
    }
}
```

### 4. 리소스 관리 구조 개선 (Refactoring)
프로젝트의 유지보수성을 높이기 위해 리소스 폴더 구조를 **씬(Scene) 중심**으로 전면 개편했습니다.
- **구조화:** `Game_Resource` 하위에 `IntroScene`, `LobbyScene` 등 씬별 전용 폴더를 생성하여 응집도를 높였습니다.
- **중복 제거:** 여러 씬에서 공통으로 사용되는 리소스는 `Common` 폴더로 통합하여 용량 낭비를 줄이고 관리 효율을 높였습니다.
- **표준화:** 각 폴더 내부는 `Images`, `Prefabs`, `Animations`, `Materials`, `Sounds`로 표준화된 분류 체계를 따릅니다.

### 5. 게임플레이 및 UI/UX 개선
- **안전한 스폰 시스템:** `ObjectPoolSpawner`의 알고리즘을 개선하여, 적들이 카메라 시야 밖이면서 맵 경계 내부인 안전한 위치에서만 스폰되도록 구현했습니다. (화면 밖 스폰 보장)
- **반응형 조이스틱:** 씬 진입 시 카운트다운과 관계없이 조이스틱 위치가 즉시 적용되도록 초기화 시점을 최적화했습니다.
- **프레임 설정 옵션:** 플레이어가 기기 사양에 맞춰 30, 60, 120 FPS를 선택할 수 있는 옵션 기능을 구현했습니다.

---

### 6. 코드 아키텍처 및 표준화 (Architecture & Standardization)
본 프로젝트는 **Unity Technologies의 'Clean Code'** 철학을 기반으로 엄격한 코딩 표준을 준수합니다. 이를 통해 코드의 가독성, 유지보수성, 협업 효율성을 극대화합니다.

- **Core Philosophy:** `PlayerBase` 클래스를 표준 모델로 삼아, 모든 스크립트가 통일된 구조를 갖도록 설계했습니다.
- **MVVM 패턴 적용:** UI와 비즈니스 로직을 철저히 분리하기 위해 Title 씬 등 주요 UI 시스템에 **MVVM (Model-View-ViewModel)** 패턴을 도입했습니다.
  - **View:** `MonoBehaviour`를 상속받아 오직 데이터 바인딩과 시각화만 담당합니다. (예: `LoginViewMVVM.cs`)
  - **ViewModel:** 순수 C# 클래스(POCO)로 작성되며, 상태 관리와 비즈니스 로직을 수행합니다. (예: `LoginViewModel.cs`)
- **Naming Convention:**
  - `m_`: Private/Protected 필드 접두사 (예: `m_health`)
  - `s_`: Static 필드 접두사
  - `k_`: Constant 및 Readonly 필드 접두사
- **Documentation:** 모든 주요 클래스와 메서드에는 한국어 XML 문서 주석을 작성하며, `#region`을 활용해 코드 섹션을 명확히 구분합니다.

### 7. MVVM 패턴 적용 예시 (Title Scene)
View와 ViewModel을 분리하여 UI 로직의 의존성을 낮추고 테스트 용이성을 확보했습니다.

**ViewModel (비즈니스 로직 & 상태 관리):**
`MonoBehaviour`를 상속받지 않는 순수 C# 클래스로, View를 전혀 참조하지 않습니다. 오직 상태(State)를 변경하고 이벤트를 발행합니다.

```csharp
// LoginViewModel.cs
public class LoginViewModel
{
    // 1. 상태 변경 감지를 위한 이벤트 (Action 또는 UniRx)
    public event Action<bool> OnBusyStateChanged;
    public event Action<string> OnErrorMessage;

    private bool m_isBusy;
    public bool IsBusy
    {
        get => m_isBusy;
        private set
        {
            m_isBusy = value;
            OnBusyStateChanged?.Invoke(m_isBusy); // 상태 변경 알림
        }
    }

    // 2. 비즈니스 로직 수행 (Async/Await)
    public async UniTask LoginAsync(string id, string pw)
    {
        if (IsBusy) return;
        IsBusy = true;

        try 
        {
            // 서버 통신 및 데이터 처리
            await m_authService.LoginAsync(id, pw);
            OnLoginSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            OnErrorMessage?.Invoke(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

**View (UI 표현 & 입력 전달):**
ViewModel의 상태 변화를 구독(Subscribe)하여 UI를 갱신하고, 사용자 입력을 ViewModel의 메서드 호출로 전달합니다.

```csharp
// LoginViewMVVM.cs
public class LoginViewMVVM : MonoBehaviour
{
    [SerializeField] private GameObject m_loadingIndicator;
    private LoginViewModel m_viewModel;

    // 1. ViewModel 바인딩 (이벤트 구독)
    private void BindViewModel()
    {
        m_viewModel.OnBusyStateChanged += isBusy => 
        {
            m_loadingIndicator.SetActive(isBusy); // 로딩 중 UI 표시
            m_loginButton.interactable = !isBusy; // 버튼 비활성화
        };

        m_viewModel.OnErrorMessage += ShowErrorPopup;
    }

    // 2. UI 입력 -> ViewModel 명령 전달
    public void OnClickLogin()
    {
        string id = m_idInputField.text;
        string pw = m_pwInputField.text;
        
        // Fire-and-Forget 방식으로 비동기 호출
        m_viewModel.LoginAsync(id, pw).Forget();
    }
}
```

---

## 📂 프로젝트 구조

### 🗂️ Assets/Game_Resource (리소스)
```
Assets/Game_Resource/
├── Common/              # 공통 리소스 (대부분의 게임 에셋 통합)
│   ├── Images/
│   ├── Prefabs/
│   ├── Materials/
│   └── Sounds/
├── IntroScene/          # 인트로 씬 전용 리소스
├── LobbyScene/          # 로비 씬 전용 리소스
└── LoadResourceScene/   # 로딩 씬 리소스
```

### 📜 Assets/Scripts (스크립트)
```
Assets/Scripts/
├── Data/                # 데이터 모델 및 관리
├── Editor/              # 에디터 확장 스크립트
├── InGame/              # 인게임 로직 (Player, Mob, Manager 등 핵심 코어)
│   ├── Drop_Item/       # 아이템 드롭 로직
│   ├── Manager/         # GameManager, EffectManager, UIManager 등
│   ├── Mob/             # 몬스터 AI 및 행동 로직
│   ├── ObjectPool/      # 최적화를 위한 풀링 시스템
│   ├── Player/          # 플레이어 컨트롤 및 스탯 관리
│   └── Weapon/          # 무기 및 투사체 구현 (Weapon)
├── Lobby/               # 로비 씬 로직 및 UI
├── Manager/             # 전역 관리 매니저 (SceneLoader 등)
├── protect/             # 보안 및 암호화 (HybridEncryption)
├── ScriptableOJB/       # 스크립터블 오브젝트 데이터
├── Test/                # 테스트용 스크립트
└── Title/               # 타이틀 화면 로직 (MVVM 패턴 적용)
```
