using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DogGuns_Games.vamsir;
using Cysharp.Threading.Tasks;
using DogGuns_Games;

/// <summary>
/// 테스트 씬에서 UI를 통해 캐릭터와 무기를 동적으로 변경하는 테스트용 매니저입니다.
/// </summary>
public class TestManager : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("캐릭터 인덱스를 입력할 InputField")]
    [SerializeField] private TMP_InputField characterIndexInput;
    
    [Tooltip("무기 인덱스를 입력할 InputField")]
    [SerializeField] private TMP_InputField weaponIndexInput;
    
    [Tooltip("변경을 실행할 버튼")]
    [SerializeField] private Button changeButton;

    private VamserLikeGameManager _gameManager;
    private bool _isChanging; // 중복 실행 방지를 위한 플래그

    private void Start()
    {
        // VamserLikeGameManager의 싱글톤 인스턴스를 참조합니다.
        _gameManager = VamserLikeGameManager.Instance;
        if (_gameManager == null)
        {
            Debug.LogError("VamserLikeGameManager 인스턴스를 찾을 수 없습니다. TestManager를 사용할 수 없습니다.");
            if (changeButton != null) changeButton.interactable = false;
            return;
        }

        // 버튼에 리스너를 추가합니다.
        if (changeButton != null)
        {
            changeButton.onClick.AddListener(OnChangeButtonPressed);
        }

        // 현재 선택된 인덱스로 입력 필드를 초기화합니다.
        UpdateInputFields();
    }

    /// <summary>
    /// 현재 PlayerDataManager의 선택 인덱스로 UI 입력 필드를 업데이트합니다.
    /// </summary>
    private void UpdateInputFields()
    {
        var dataManager = PlayerDataManagerDontdesytoy.Instance;
        if (dataManager == null) return;

        if (characterIndexInput != null)
        {
            characterIndexInput.text = dataManager.SelectCharacterIndex.ToString();
        }
        if (weaponIndexInput != null)
        {
            weaponIndexInput.text = dataManager.SelectWeaponIndex.ToString();
        }
    }

    /// <summary>
    /// '변경' 버튼이 클릭되었을 때 호출됩니다.
    /// </summary>
    private void OnChangeButtonPressed()
    {
        if (_isChanging) return; // 이미 변경 작업이 진행 중이면 무시
        
        ChangeCharacterAndWeaponAsync().Forget();
    }

    /// <summary>
    /// 입력 필드의 값을 읽어 캐릭터와 무기를 비동기적으로 변경합니다.
    /// </summary>
    private async UniTaskVoid ChangeCharacterAndWeaponAsync()
    {
        _isChanging = true;
        if (changeButton != null) changeButton.interactable = false;

        try
        {
            var dataManager = PlayerDataManagerDontdesytoy.Instance;
            if (dataManager == null)
            {
                Debug.LogError("PlayerDataManagerDontdesytoy 인스턴스를 찾을 수 없습니다.");
                return;
            }

            // UI 입력 값 파싱 및 데이터 매니저에 설정
            if (characterIndexInput != null && int.TryParse(characterIndexInput.text, out int charIndex))
            {
                dataManager.SelectCharacterIndex = charIndex;
            }

            if (weaponIndexInput != null && int.TryParse(weaponIndexInput.text, out int wepIndex))
            {
                dataManager.SelectWeaponIndex = wepIndex;
            }

            // 게임 매니저의 변경 함수 호출
            if (_gameManager != null)
            {
                await _gameManager.ChangeCharacterAndWeapon_Spawn();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"캐릭터/무기 변경 중 오류 발생: {e.Message}");
        }
        finally
        {
            // 작업 완료 후 버튼 상태 복원
            _isChanging = false;
            if (changeButton != null) changeButton.interactable = true;
        }
    }
}