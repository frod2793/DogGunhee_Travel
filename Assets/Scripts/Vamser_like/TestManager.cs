using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DogGuns_Games.vamsir;
using Cysharp.Threading.Tasks;
using DG.Tweening; // DOTween 네임스페이스 추가
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

    [Header("테스트 패널")]
    [Tooltip("애니메이션을 적용할 테스트 패널")]
    [SerializeField] private GameObject testpanel;
    [Tooltip("테스트 패널을 열고 닫는 버튼")]
    [SerializeField] private Button TestPanelonoffBtn;
    [Tooltip("패널 애니메이션 지속 시간")]
    [SerializeField] private float animationDuration = 0.3f;

    private VamserLikeGameManager _gameManager;
    private bool _isChanging; // 중복 실행 방지를 위한 플래그
    
    // 패널 애니메이션 관련 변수
    private RectTransform _panelRectTransform;
    private Vector2 _panelOriginalPos;
    private bool _isPanelOpen = false;
    private bool _isAnimating = false;

    private void Start()
    {
        // VamserLikeGameManager의 싱글톤 인스턴스를 참조합니다.
        _gameManager = VamserLikeGameManager.Instance;
        if (_gameManager == null)
        {
            Debug.LogError("VamserLikeGameManager 인스턴스를 찾을 수 없습니다. TestManager를 사용할 수 없습니다.");
            if (changeButton != null) changeButton.interactable = false;
            // TestManager의 다른 기능도 비활성화
            if (TestPanelonoffBtn != null) TestPanelonoffBtn.interactable = false;
            return;
        }

        // '변경' 버튼 리스너 추가
        if (changeButton != null)
        {
            changeButton.onClick.AddListener(OnChangeButtonPressed);
        }

        // 테스트 패널 토글 버튼 리스너 및 초기화
        if (testpanel != null)
        {
            _panelRectTransform = testpanel.GetComponent<RectTransform>();
            _panelOriginalPos = _panelRectTransform.anchoredPosition;
            //testpanel.SetActive(false); // 시작 시 패널 비활성화
        }

        if (TestPanelonoffBtn != null)
        {
            TestPanelonoffBtn.onClick.AddListener(OnTestPanelTogglePressed);
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
    /// 테스트 패널 토글 버튼이 클릭되었을 때 호출됩니다.
    /// </summary>
    private void OnTestPanelTogglePressed()
    {
        if (_isAnimating) return; // 애니메이션 중에는 입력을 무시합니다.

        _isPanelOpen = !_isPanelOpen; // 상태 전환
        TogglePanelAsync(_isPanelOpen).Forget();
    }

    /// <summary>
    /// 테스트 패널을 열거나 닫는 애니메이션을 비동기적으로 처리합니다.
    /// </summary>
    /// <param name="open">true이면 패널을 열고, false이면 닫습니다.</param>
    private async UniTaskVoid TogglePanelAsync(bool open)
    {
        _isAnimating = true;

        if (open)
        {
            // 패널을 엽니다.
            testpanel.SetActive(true);
            // 패널 너비만큼 오른쪽으로 이동합니다.
            float targetX = _panelOriginalPos.x + _panelRectTransform.rect.width;
            await _panelRectTransform.DOAnchorPosX(targetX, animationDuration).SetEase(Ease.OutQuad).ToUniTask();
        }
        else
        {
            // 패널을 닫습니다.
            // 원래 위치로 이동합니다.
            await _panelRectTransform.DOAnchorPosX(_panelOriginalPos.x, animationDuration).SetEase(Ease.InQuad).ToUniTask();
           // testpanel.SetActive(false);
        }

        _isAnimating = false;
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