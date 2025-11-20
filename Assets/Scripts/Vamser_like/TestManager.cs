using UnityEngine;
using UnityEngine.Serialization; // 인스펙터 데이터 보존용
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DogGuns_Games;
using DogGuns_Games.vamsir;

namespace DogGuns_Games.Test
{
    /// <summary>
    /// 테스트 씬에서 UI를 통해 캐릭터와 무기를 동적으로 변경하는 관리자 클래스입니다.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        #region 인스펙터 필드 (UI 요소)

        [Header("Input Fields")]
        [Tooltip("캐릭터 인덱스 입력 필드")]
        [FormerlySerializedAs("characterIndexInput")]
        [SerializeField] private TMP_InputField m_characterIndexInput;
        
        [Tooltip("무기 인덱스 입력 필드")]
        [FormerlySerializedAs("weaponIndexInput")]
        [SerializeField] private TMP_InputField m_weaponIndexInput;

        [Header("Control Buttons")]
        [Tooltip("무기 업그레이드 여부 토글")]
        [FormerlySerializedAs("isWeaponeUPgrade")]
        [SerializeField] private Toggle m_isWeaponUpgradeToggle;

        [Tooltip("설정 변경 실행 버튼")]
        [FormerlySerializedAs("changeButton")]
        [SerializeField] private Button m_changeButton;

        [Header("Test Panel Settings")]
        [Tooltip("슬라이드 애니메이션을 적용할 패널")]
        [FormerlySerializedAs("testpanel")]
        [SerializeField] private GameObject m_testPanel;

        [Tooltip("패널 열기/닫기 토글 버튼")]
        [FormerlySerializedAs("TestPanelonoffBtn")]
        [SerializeField] private Button m_testPanelToggleBtn;

        [Tooltip("패널 애니메이션 지속 시간")]
        [SerializeField] private float m_animationDuration = 0.3f;

        #endregion

        #region 내부 상태 변수

        private VamserLikeGameManager m_gameManager;
        private RectTransform m_panelRectTransform;
        
        // 패널 상태
        private Vector2 m_panelOriginalPos;
        private bool m_isPanelOpen = false;
        private bool m_isPanelAnimating = false;
        
        // 로직 상태
        private bool m_isChanging = false;

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            InitializeReferences();
            InitializeUI();
            InitializePanel();
        }

        private void OnDestroy()
        {
            // 씬 전환이나 객체 파괴 시 실행 중인 트윈 제거 (메모리 누수 방지)
            if (m_panelRectTransform != null)
            {
                m_panelRectTransform.DOKill();
            }
        }

        #endregion

        #region 초기화

        private void InitializeReferences()
        {
            m_gameManager = VamserLikeGameManager.Instance;
            
            if (m_gameManager == null)
            {
                Debug.LogError("[TestManager] VamserLikeGameManager 인스턴스를 찾을 수 없습니다.");
                SetInteractable(false);
            }
        }

        private void InitializeUI()
        {
            // 버튼 리스너 등록
            if (m_changeButton != null)
                m_changeButton.onClick.AddListener(() => ChangeCharacterAndWeaponAsync().Forget());

            if (m_testPanelToggleBtn != null)
                m_testPanelToggleBtn.onClick.AddListener(() => TogglePanelAsync().Forget());

            // 초기값 설정
            UpdateInputFields();
        }

        private void InitializePanel()
        {
            if (m_testPanel != null)
            {
                m_panelRectTransform = m_testPanel.GetComponent<RectTransform>();
                m_panelOriginalPos = m_panelRectTransform.anchoredPosition;
                
                // 시작 시 닫힌 상태라면 비활성화하여 성능 최적화
                // (현재 로직상 닫혀있다고 가정)
                // m_testPanel.SetActive(false); 
            }
        }

        private void SetInteractable(bool isInteractable)
        {
            if (m_changeButton != null) m_changeButton.interactable = isInteractable;
            if (m_testPanelToggleBtn != null) m_testPanelToggleBtn.interactable = isInteractable;
        }

        #endregion

        #region UI 로직

        private void UpdateInputFields()
        {
            var dataManager = PlayerDataManagerDontdesytoy.Instance;
            if (dataManager == null) return;

            if (m_characterIndexInput != null)
                m_characterIndexInput.text = dataManager.SelectCharacterIndex.ToString();

            if (m_weaponIndexInput != null)
                m_weaponIndexInput.text = dataManager.SelectWeaponIndex.ToString();
        }

        #endregion

        #region 패널 애니메이션

        /// <summary>
        /// 패널 열기/닫기 애니메이션 처리
        /// </summary>
        private async UniTaskVoid TogglePanelAsync()
        {
            if (m_testPanel == null || m_isPanelAnimating) return;

            m_isPanelAnimating = true;
            m_isPanelOpen = !m_isPanelOpen;

            // 애니메이션 중복 방지를 위해 기존 트윈 제거
            m_panelRectTransform.DOKill();

            try
            {
                if (m_isPanelOpen)
                {
                    m_testPanel.SetActive(true);
                    float targetX = m_panelOriginalPos.x + m_panelRectTransform.rect.width;
                    
                    await m_panelRectTransform.DOAnchorPosX(targetX, m_animationDuration)
                        .SetEase(Ease.OutQuad)
                        .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
                }
                else
                {
                    await m_panelRectTransform.DOAnchorPosX(m_panelOriginalPos.x, m_animationDuration)
                        .SetEase(Ease.InQuad)
                        .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
                    
                    // 닫힌 후 비활성화 (드로우 콜 절약)
                    // m_testPanel.SetActive(false); 
                }
            }
            finally
            {
                m_isPanelAnimating = false;
            }
        }

        #endregion

        #region 데이터 변경 로직

        /// <summary>
        /// 캐릭터 및 무기 변경 프로세스
        /// </summary>
        private async UniTaskVoid ChangeCharacterAndWeaponAsync()
        {
            if (m_isChanging || m_gameManager == null) return;

            // 1. 입력값 유효성 검사 및 파싱
            if (!TryParseInputs(out int charIndex, out int wpIndex))
            {
                Debug.LogWarning("[TestManager] 유효하지 않은 입력값입니다.");
                return;
            }

            m_isChanging = true;
            if (m_changeButton != null) m_changeButton.interactable = false;

            try
            {
                // 2. 데이터 매니저 업데이트
                var dataManager = PlayerDataManagerDontdesytoy.Instance;
                if (dataManager != null)
                {
                    dataManager.SelectCharacterIndex = charIndex;
                    dataManager.SelectWeaponIndex = wpIndex;
                }

                // 3. 게임 매니저에 변경 요청
                await m_gameManager.ChangeCharacterAndWeapon_Spawn();

                // 4. 무기 업그레이드 상태 적용
                ApplyWeaponUpgradeState();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TestManager] 변경 중 오류 발생: {e.Message}");
            }
            finally
            {
                m_isChanging = false;
                if (m_changeButton != null) m_changeButton.interactable = true;
            }
        }

        /// <summary>
        /// 입력 필드의 값을 파싱합니다.
        /// </summary>
        private bool TryParseInputs(out int charIndex, out int wpIndex)
        {
            charIndex = 0;
            wpIndex = 0;

            if (m_characterIndexInput == null || m_weaponIndexInput == null) return false;

            bool isCharValid = int.TryParse(m_characterIndexInput.text, out charIndex);
            bool isWpValid = int.TryParse(m_weaponIndexInput.text, out wpIndex);

            return isCharValid && isWpValid;
        }

        /// <summary>
        /// 생성된 플레이어의 무기에 업그레이드 상태를 적용합니다.
        /// </summary>
        private void ApplyWeaponUpgradeState()
        {
            if (m_gameManager.SpawnedPlayer == null || m_isWeaponUpgradeToggle == null) return;

            var weapon = m_gameManager.SpawnedPlayer.GetComponentInChildren<Weaphon_base>();
            if (weapon != null)
            {
                weapon.isUpgradelv2 = m_isWeaponUpgradeToggle.isOn;
                
                LogManager.Log(
                    $"무기({weapon.name}) 업그레이드 설정: {m_isWeaponUpgradeToggle.isOn}", 
                    LogManager.LogCategory.VamserLikeGameManager, 
                    weapon
                );
            }
        }

        #endregion
    }
}