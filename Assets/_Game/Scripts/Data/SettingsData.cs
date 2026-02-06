using System;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 게임의 전반적인 설정을 관리하고 저장하는 ScriptableObject 클래스입니다.
/// </summary>
[CreateAssetMenu(fileName = "SettingsData", menuName = "GameSettings/Settings Data")]
public class SettingsData : ScriptableObject
{
    #region 필드 및 프로퍼티

    // --- 사운드 설정 ---
    [Tooltip("배경음 볼륨 (0.0 ~ 1.0)")]
    [FormerlySerializedAs("backgroundSoundVolume")]
    [SerializeField] private float m_backgroundSoundVolume = 0.5f;
    public float BackgroundSoundVolume { get => m_backgroundSoundVolume; set => m_backgroundSoundVolume = value; }

    [Tooltip("효과음 볼륨 (0.0 ~ 1.0)")]
    [FormerlySerializedAs("effectSoundVolume")]
    [SerializeField] private float m_effectSoundVolume = 0.5f;
    public float EffectSoundVolume { get => m_effectSoundVolume; set => m_effectSoundVolume = value; }

    // --- 조이스틱 버튼 크기 설정 ---
    [Tooltip("작은 조이스틱 버튼 토글 상태")]
    [FormerlySerializedAs("conSmallToggle")]
    [SerializeField] private bool m_conSmallToggle = true;
    public bool ConSmallToggle { get => m_conSmallToggle; set => m_conSmallToggle = value; }

    [Tooltip("중간 조이스틱 버튼 토글 상태")]
    [FormerlySerializedAs("conNormalToggle")]
    [SerializeField] private bool m_conNormalToggle = false;
    public bool ConNormalToggle { get => m_conNormalToggle; set => m_conNormalToggle = value; }

    [Tooltip("큰 조이스틱 버튼 토글 상태")]
    [FormerlySerializedAs("conBigBtnToggle")]
    [SerializeField] private bool m_conBigBtnToggle = false;
    public bool ConBigBtnToggle { get => m_conBigBtnToggle; set => m_conBigBtnToggle = value; }
    
    // --- 조이스틱 상세 설정 ---
    [Tooltip("조이스틱 타입 (예: 0=고정, 1=유동)")]
    [FormerlySerializedAs("joystickType")]
    [SerializeField] private int m_joystickType = 0;
    public int JoystickType { get => m_joystickType; set => m_joystickType = value; }

    [Tooltip("조이스틱 크기 배율")]
    [FormerlySerializedAs("joystickSize")]
    [SerializeField] private float m_joystickSize = 1f;
    public float JoystickSize { get => m_joystickSize; set => m_joystickSize = value; }

    [Tooltip("조이스틱 기본 위치 (화면 비율 기준)")]
    [FormerlySerializedAs("joystickPos")]
    [SerializeField] private Vector2 m_joystickPos = new Vector2(0.5f, 0.5f);
    public Vector2 JoystickPos { get => m_joystickPos; set => m_joystickPos = value; }
    
    // --- 성능 설정 ---
    [Tooltip("목표 프레임 레이트 (FPS)")]
    [SerializeField] private int m_targetFrameRate = 120;
    public int TargetFrameRate { get => m_targetFrameRate; set => m_targetFrameRate = value; }

    // --- 이벤트 ---
    /// <summary>
    /// 설정이 저장될 때 호출되는 이벤트입니다.
    /// </summary>
    public static event Action OnSettingsChanged;

    // --- 상수 ---
    private const string k_SettingsFileName = "settingsData.json";
    private static string FilePath => Path.Combine(Application.persistentDataPath, k_SettingsFileName);

    #endregion

    #region 데이터 저장 및 불러오기

    /// <summary>
    /// 현재 설정을 JSON 파일로 저장합니다.
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            string jsonData = JsonUtility.ToJson(this, true);
            File.WriteAllText(FilePath, jsonData);
            LogManager.Log($"설정 파일이 저장되었습니다: {FilePath}", LogManager.LogCategory.SettingsManager);
            
            // 설정이 저장되었음을 모든 구독자에게 알립니다.
            OnSettingsChanged?.Invoke();
        }
        catch (Exception e)
        {
            LogManager.LogError($"설정 파일 저장 중 오류 발생: {e.Message}", LogManager.LogCategory.SettingsManager);
        }
    }

    /// <summary>
    /// JSON 파일에서 설정을 불러옵니다. 파일이 없거나 손상된 경우 기본값으로 새로 생성합니다.
    /// </summary>
    public void LoadSettings()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                string jsonData = File.ReadAllText(FilePath);
                JsonUtility.FromJsonOverwrite(jsonData, this);
                LogManager.Log($"설정 파일을 불러왔습니다: {FilePath}", LogManager.LogCategory.SettingsManager);
            }
            catch (Exception e)
            {
                LogManager.LogWarning($"설정 파일({FilePath})을 불러오는 데 실패했습니다. 손상되었을 수 있습니다. 기본값으로 새로 생성합니다. 오류: {e.Message}", LogManager.LogCategory.SettingsManager);
                SaveSettings(); // 파일이 손상된 경우 기본값으로 덮어쓰기
            }
        }
        else
        {
            LogManager.LogWarning("설정 파일이 없어 기본값으로 새로 생성합니다.", LogManager.LogCategory.SettingsManager);
            SaveSettings();
        }
    }

    #endregion
}