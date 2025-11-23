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
// UniTask와 Addressables를 활용한 실제 캐릭터 스폰 예시 (VamserLikeGameManager.cs)
private async UniTask SpawnCharacterAsync(Weaphon_base weapon)
{
    int index = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
    string key = $"Player_Character_{index}"; // Addressable Key

    try
    {
        var op = Addressables.InstantiateAsync(key, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform);
        GameObject instance = await op.ToUniTask();

        if (instance != null)
        {
            instance.transform.localPosition = Vector3.zero;
            SpawnedPlayer = instance.GetComponent<PlayerBase>();

            if (SpawnedPlayer != null)
            {
                // 무기 장착
                SpawnedPlayer.InitializeWeapon(weapon);
                
                // 컨트롤러 연결
                if (m_playerController != null)
                {
                    m_playerController.AssignCharacter(SpawnedPlayer);
                }
            }
            else
            {
                LogManager.LogError($"[GameManager] PlayerBase component missing on {instance.name}");
            }

            // 이벤트 전파 (UI 업데이트 등)
            OnPlayerChanged?.Invoke(SpawnedPlayer);
        }
    }
    catch (Exception e)
    {
        LogManager.LogError($"[GameManager] Character Spawn Error ({key}): {e.Message}");
        SpawnedPlayer = null;
    }
}
```

### 2. 대규모 전투를 위한 런타임 성능 최적화
뱀파이어 서바이버즈 장르의 특성상 화면에 수많은 투사체와 몬스터가 등장합니다. 이는 `Instantiate`와 `Destroy`의 반복 호출로 이어져 심각한 **GC Spike(가비지 컬렉션으로 인한 프레임 드랍)**를 유발합니다.

**강점: 오브젝트 풀링과 팩토리 패턴을 결합한 고성능 객체 관리**
이 문제를 해결하기 위해 **오브젝트 풀링(Object Pooling)**과 **팩토리 패턴(Factory Pattern)**을 결합한 시스템을 설계했습니다.

- **오브젝트 풀링**: `Unity.ObjectPool` API를 기반으로, 자주 사용되는 오브젝트(몬스터, 투사체 등)를 미리 생성하여 풀(Pool)에 보관합니다. 이를 통해 GC 발생을 원천적으로 차단하고 안정적인 프레임을 확보했습니다.
- **팩토리 패턴**: 오브젝트를 **'생성하는 코드'**와 **'사용하는 코드'**를 명확히 분리했습니다. 그 결과, 새로운 몬스터나 아이템을 추가할 때 기존 코드 수정 없이 팩토리 클래스에 새로운 생성 로직만 추가하면 되므로, **유지보수성과 확장성**이 크게 향상되었습니다.

```csharp
// 오브젝트 풀을 활용하여 획득한 경험치 오브젝트를 반환하는 실제 코드 (PlayerBase.cs)
private void HandleExpCollision(GameObject expObject)
{
    bool hasComponent = expObject.TryGetComponent(out EXP_Obj expObj);

    if (hasComponent && expObj.ObjectPoolSpawner != null)
    {
        // [정상 처리]
        float expAmount = expObj.ExpValue * ExpGain;
        AddExperience(expAmount);
        
        expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
        SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
    }
}
```

### 3. 안전한 데이터 관리 및 보안
로컬에 저장되는 재화, 아이템 등 민감한 사용자 데이터의 위변조를 방지하고, 기기 변경이나 삭제 시에도 데이터를 보존하기 위한 강력한 보안 및 백업 시스템을 구축했습니다.

**강점: 하이브리드 암호화 및 서버 연동**
- **데이터 위변조 방지 (하이브리드 암호화)**: 로컬 데이터는 **AES-256** 대칭키로 암호화하여 빠른 성능을 보장하고, 이 AES 키 자체는 **RSA** 공개키로 다시 암호화하여 안전하게 보관합니다. 이 하이브리드 방식은 속도와 보안을 모두 만족시키는 효과적인 해결책입니다.
- **서버를 통한 데이터 영속성**: 암호화된 데이터는 JSON으로 직렬화하여 **뒤끝(TheBackend.io)** 서버에 백업합니다. 이를 통해 로컬 데이터가 조작되더라도 서버의 데이터를 통해 즉시 복구할 수 있어 치팅을 방지하고 사용자의 자산을 안전하게 보호합니다.

```csharp
// 실제 사용 중인 하이브리드 암호화/복호화 로직 (HybridEncryption.cs)

/// <summary>
/// 암호화된 데이터와 세션 키를 포함하며, JSON 직렬화를 지원하는 하이브리드 암호화 패킷 클래스입니다.
/// </summary>
[System.Serializable]
public class EncryptedPacket
{
    public string EncryptedSessionKeyBase64;
    public string EncryptedDataBase64;
}

/// <summary>
/// 하이브리드 암호화(RSA + AES)를 수행하는 클래스입니다.
/// </summary>
public class HybridEncryption
{
    /// <summary>
    /// 주어진 RSA 공개키를 사용하여 평문 데이터를 암호화합니다.
    /// </summary>
    public EncryptedPacket Encrypt(string plainJson, string publicKey)
    {
        // 1. AES 암호화를 위한 임시 대칭키(세션키)와 IV를 생성합니다.
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.GenerateKey();
            aes.GenerateIV();
            byte[] sessionKey = aes.Key;
            byte[] iv = aes.IV;

            // 2. 생성된 AES 세션키를 사용하여 JSON 데이터를 암호화합니다.
            byte[] encryptedData;
            using (var encryptor = aes.CreateEncryptor(sessionKey, iv))
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainJson);
                encryptedData = PerformCryptography(plainBytes, encryptor);
            }

            // 3. RSA 공개키를 사용하여 AES 세션키와 IV를 암호화합니다.
            byte[] encryptedSessionKey;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKey);
                byte[] keyAndIv = new byte[sessionKey.Length + iv.Length];
                Buffer.BlockCopy(sessionKey, 0, keyAndIv, 0, sessionKey.Length);
                Buffer.BlockCopy(iv, 0, keyAndIv, sessionKey.Length, iv.Length);
                encryptedSessionKey = rsa.Encrypt(keyAndIv, true);
            }

            // 4. 암호화된 데이터와 암호화된 세션키를 하나의 패킷으로 묶어 반환합니다.
            return new EncryptedPacket { 
                EncryptedSessionKeyBase64 = Convert.ToBase64String(encryptedSessionKey),
                EncryptedDataBase64 = Convert.ToBase64String(encryptedData)
            };
        }
    }

    /// <summary>
    /// 주어진 RSA 개인키를 사용하여 암호화된 패킷을 복호화합니다.
    /// </summary>
    public string Decrypt(EncryptedPacket packet, string privateKey)
    {
        // 1. RSA 개인키를 사용하여 암호화된 AES 세션키와 IV를 복호화합니다.
        byte[] decryptedKeyAndIv;
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(privateKey);
            decryptedKeyAndIv = rsa.Decrypt(Convert.FromBase64String(packet.EncryptedSessionKeyBase64), true);
        }

        // 2. 복호화된 바이트 배열에서 AES 키와 IV를 다시 분리합니다.
        using (var aes = Aes.Create())
        {
            byte[] sessionKey = new byte[aes.KeySize / 8];
            byte[] iv = new byte[aes.BlockSize / 8];
            Buffer.BlockCopy(decryptedKeyAndIv, 0, sessionKey, 0, sessionKey.Length);
            Buffer.BlockCopy(decryptedKeyAndIv, sessionKey.Length, iv, 0, iv.Length);

            // 3. 복호화된 AES 세션키를 사용하여 원본 데이터를 복호화합니다.
            byte[] decryptedDataBytes;
            using (var decryptor = aes.CreateDecryptor(sessionKey, iv))
            {
                decryptedDataBytes = PerformCryptography(Convert.FromBase64String(packet.EncryptedDataBase64), decryptor);
            }

            // 4. 바이트 배열을 UTF8 문자열로 변환하여 최종 반환합니다.
            return Encoding.UTF8.GetString(decryptedDataBytes);
        }
    }

    // 암호화/복호화 스트림 처리를 위한 헬퍼 메소드
    private byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
    {
        using (var memoryStream = new MemoryStream())
        using (var cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
        {
            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();
            return memoryStream.ToArray();
        }
    }
}
```

### 4. 그 외 기술 요소
- **확장 가능한 데이터 관리**: **Scriptable Object**를 활용하여 게임의 핵심 데이터를 관리함으로써, 기획자가 코드 수정 없이 밸런스를 쉽게 조절할 수 있도록 설계했습니다.
- **안정적인 전역 접근**: **Singleton 패턴**을 적용하여 게임 내 주요 매니저(UIManager, GameManager 등)에 대한 일관되고 안정적인 접근점을 제공했습니다.
- **다양한 사용자 인증**: 게스트, GPGS, 토큰 기반 자동 로그인 등 다양한 인증 방식을 지원하여 사용자의 접근성을 높였습니다.
