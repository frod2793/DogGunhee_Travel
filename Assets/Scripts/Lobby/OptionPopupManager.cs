using System;
using System.Collections.Generic;
using DogGuns_Games.vamsir;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionPopupManager : MonoBehaviour
{
    public SettingsData_oBJ settingsData; // ScriptableObject 참조

    [Header("사운드 조절")] [SerializeField] private Slider effectSoundVolum;
    [SerializeField] private Slider bgMsoundVolum;

    [Header("<color=green> 나가기 버튼")] [SerializeField]
    private Button exitBtn;

    SoundManager _soundManager;


    [Header("조이스틱 사이즈및 타입 조절 버튼")] [SerializeField]
    private Button joystickSizeBtn;

    [SerializeField] private Joystic_setter joysticSetterPopUp_Prefb;


    private void OnEnable()
    {
        settingsData.LoadSettings();
        // 슬라이더와 토글에 대한 리스너 등록
        effectSoundVolum.onValueChanged.AddListener(delegate { EffectsoundVolumSlider(); });
        bgMsoundVolum.onValueChanged.AddListener(delegate { BgmSoundVolumSlider(); });
        exitBtn.onClick.AddListener(SaveAndExit);
        joystickSizeBtn.onClick.AddListener(EnableJoystickSizeBtn);
        DropDown_Init();
        
        if (_soundManager == null)
        {
            _soundManager = FindAnyObjectByType<SoundManager>();
        }

        LoadSettings(); // 시작 시 설정 불러오기
    }

    private void OnDestroy()
    {
        // 슬라이더와 토글에 대한 리스너 해제
        effectSoundVolum.onValueChanged.RemoveAllListeners();
        bgMsoundVolum.onValueChanged.RemoveAllListeners();
        exitBtn.onClick.RemoveAllListeners();
        joystickSizeBtn.onClick.RemoveAllListeners();
    }


    private void EffectsoundVolumSlider()
    {
        if (_soundManager != null)
        {
            _soundManager.VolumSet(Sound.Effect, effectSoundVolum.value);
        }
    }

    private void BgmSoundVolumSlider()
    {
        if (_soundManager != null)
        {
            _soundManager.VolumSet(Sound.Bgm, bgMsoundVolum.value);
        }
    }

    private void SaveAndExit()
    {
        // ScriptableObject에 직접 저장
        settingsData.effectSoundVolume = effectSoundVolum.value;
        settingsData.backgroundSoundVolume = bgMsoundVolum.value;

        settingsData.SaveSettings();
        // ScriptableObject는 자동으로 저장되므로, 별도로 저장할 필요 없음
        Destroy(gameObject); // 창 종료

        PlayStateManager.instance.PlayState = PlayStateManager.GameState.Resume;
    }


    private void LoadSettings()
    {
        // ScriptableObject에서 설정 불러오기
        effectSoundVolum.value = settingsData.effectSoundVolume;
        bgMsoundVolum.value = settingsData.backgroundSoundVolume;
    }

    private void DropDown_Init()
    {
        List<string> options = new List<string>();
        options.Add("Fixed");
        options.Add("Floating");
        options.Add("Dynamic");
    }


    private void EnableJoystickSizeBtn()
    {
        GameObject joystickSetterPopUp = Instantiate(joysticSetterPopUp_Prefb.gameObject, transform);

        joystickSetterPopUp.SetActive(true);
    }
}