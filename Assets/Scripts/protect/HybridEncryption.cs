using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 암호화된 데이터와 세션 키를 포함하며, JSON 직렬화를 지원하는 하이브리드 암호화 패킷 클래스입니다.
/// Base64 문자열로 데이터를 직렬화하고, 내부적으로 byte[]로 변환하여 사용합니다.
/// </summary>
[System.Serializable]
public class EncryptedPacket
{
    // JSON 직렬화를 위한 Base64 인코딩된 문자열 필드
    public string EncryptedSessionKeyBase64;
    public string EncryptedDataBase64;

    // 내부 로직에서 사용할 byte[] 프로퍼티 (직렬화되지 않음)
    [System.NonSerialized]
    private byte[] _encryptedSessionKey;
    public byte[] EncryptedSessionKey
    {
        get
        {
            if (_encryptedSessionKey == null && !string.IsNullOrEmpty(EncryptedSessionKeyBase64))
            {
                _encryptedSessionKey = Convert.FromBase64String(EncryptedSessionKeyBase64);
            }
            return _encryptedSessionKey;
        }
        set
        {
            _encryptedSessionKey = value;
            EncryptedSessionKeyBase64 = value != null ? Convert.ToBase64String(value) : null;
        }
    }

    [System.NonSerialized]
    private byte[] _encryptedData;
    public byte[] EncryptedData
    {
        get
        {
            if (_encryptedData == null && !string.IsNullOrEmpty(EncryptedDataBase64))
            {
                _encryptedData = Convert.FromBase64String(EncryptedDataBase64);
            }
            return _encryptedData;
        }
        set
        {
            _encryptedData = value;
            EncryptedDataBase64 = value != null ? Convert.ToBase64String(value) : null;
        }
    }
    
    public EncryptedPacket() { } 

    public EncryptedPacket(byte[] encryptedSessionKey, byte[] encryptedData)
    {
        this.EncryptedSessionKey = encryptedSessionKey;
        this.EncryptedData = encryptedData;
    }
}

/// <summary>
/// 하이브리드 암호화(RSA + AES)를 수행하는 클래스입니다.
/// </summary>
public class HybridEncryption
{
    /// <summary>
    /// RSA 공개키와 개인키 쌍을 생성합니다.
    /// </summary>
    /// <param name="publicKey">생성된 공개키(XML 형식)</param>
    /// <param name="privateKey">생성된 개인키(XML 형식)</param>
    public void GenerateRsaKeys(out string publicKey, out string privateKey)
    {
        using (var rsa = new RSACryptoServiceProvider(2048)) // 2048비트 키 생성
        {
            // 공개키와 개인키를 XML 문자열 형식으로 내보냅니다.
            publicKey = rsa.ToXmlString(false); // false: 공개키만
            privateKey = rsa.ToXmlString(true); // true: 개인키 포함
        }
    }

    /// <summary>
    /// 주어진 RSA 공개키를 사용하여 평문 데이터를 암호화합니다.
    /// </summary>
    /// <param name="plainJson">암호화할 JSON 문자열</param>
    /// <param name="publicKey">수신자의 RSA 공개키(XML 형식)</param>
    /// <returns>암호화된 데이터와 세션 키가 포함된 패킷</returns>
    public EncryptedPacket Encrypt(string plainJson, string publicKey)
    {
        // 1. AES 암호화를 위한 임시 대칭키(세션키)와 초기화 벡터(IV)를 생성합니다.
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
                // 키와 IV를 하나로 합쳐서 암호화합니다.
                byte[] keyAndIv = new byte[sessionKey.Length + iv.Length];
                Buffer.BlockCopy(sessionKey, 0, keyAndIv, 0, sessionKey.Length);
                Buffer.BlockCopy(iv, 0, keyAndIv, sessionKey.Length, iv.Length);

                encryptedSessionKey = rsa.Encrypt(keyAndIv, true);
            }

            // 4. 암호화된 데이터와 암호화된 세션키를 하나의 패킷으로 묶어 반환합니다.
            return new EncryptedPacket(encryptedSessionKey, encryptedData);
        }
    }

    /// <summary>
    /// 주어진 데이터와 비밀키를 사용하여 HMAC-SHA256 해시를 생성합니다.
    /// </summary>
    /// <param name="data">해싱할 원본 문자열</param>
    /// <param name="secretKey">비밀키</param>
    /// <returns>생성된 해시 문자열 (Hex format)</returns>
    public string GenerateHMAC(string data, string secretKey)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
        {
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }

    /// <summary>
    /// 주어진 RSA 개인키를 사용하여 암호화된 패킷을 복호화합니다.
    /// </summary>
    /// <param name="packet">암호화된 데이터와 세션 키가 포함된 패킷</param>
    /// <param name="privateKey">수신자의 RSA 개인키(XML 형식)</param>
    /// <returns>복호화된 원본 JSON 문자열</returns>
    public string Decrypt(EncryptedPacket packet, string privateKey)
    {
        // 1. RSA 개인키를 사용하여 암호화된 AES 세션키와 IV를 복호화합니다.
        byte[] decryptedKeyAndIv;
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(privateKey);
            decryptedKeyAndIv = rsa.Decrypt(packet.EncryptedSessionKey, true);
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
                decryptedDataBytes = PerformCryptography(packet.EncryptedData, decryptor);
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
