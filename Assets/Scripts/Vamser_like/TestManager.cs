using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DogGuns_Games;
using DogGuns_Games.vamsir;

namespace DogGuns_Games.Test
{
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
        
        private Vector2 m_panelOriginalPos;
        private bool m_isPanelOpen = false;
        private bool m_isPanelAnimating = false;
        
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
            if (m_changeButton != null)
                m_changeButton.onClick.AddListener(() => ChangeCharacterAndWeaponAsync().Forget());

            if (m_testPanelToggleBtn != null)
                m_testPanelToggleBtn.onClick.AddListener(() => TogglePanelAsync().Forget());

            UpdateInputFields();
        }

        private void InitializePanel()
        {
            if (m_testPanel != null)
            {
                m_panelRectTransform = m_testPanel.GetComponent<RectTransform>();
                m_panelOriginalPos = m_panelRectTransform.anchoredPosition;
            }
        }

        private void SetInteractable(bool isInteractable)
        {
            if (m_changeButton != null) m_changeButton.interactable = isInteractable;
            if (m_testPanelToggleBtn != null) m_testPanelToggleBtn.interactable = isInteractable;
            if (m_isWeaponUpgradeToggle != null) m_isWeaponUpgradeToggle.interactable = isInteractable; // [추가] 토글도 제어
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

        private async UniTaskVoid TogglePanelAsync()
        {
            if (m_testPanel == null || m_isPanelAnimating) return;

            m_isPanelAnimating = true;
            m_isPanelOpen = !m_isPanelOpen;
            
            // 버튼 상호작용 잠시 차단 (애니메이션 중 중복 클릭 방지)
            if (m_testPanelToggleBtn != null) m_testPanelToggleBtn.interactable = false;

            m_panelRectTransform.DOKill();

            try
            {
                float targetX = m_isPanelOpen 
                    ? m_panelOriginalPos.x + m_panelRectTransform.rect.width 
                    : m_panelOriginalPos.x;

                Ease easeType = m_isPanelOpen ? Ease.OutQuad : Ease.InQuad;

                if (m_isPanelOpen) m_testPanel.SetActive(true);

                await m_panelRectTransform.DOAnchorPosX(targetX, m_animationDuration)
                    .SetEase(easeType)
                    .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

                // 닫힌 후 비활성화는 선택 사항 (자주 열고 닫으면 굳이 안 꺼도 됨)
                // if (!m_isPanelOpen) m_testPanel.SetActive(false);
            }
            finally
            {
                m_isPanelAnimating = false;
                if (m_testPanelToggleBtn != null) m_testPanelToggleBtn.interactable = true;
            }
        }

        #endregion

        #region 데이터 변경 로직

        private async UniTaskVoid ChangeCharacterAndWeaponAsync()
        {
            if (m_isChanging || m_gameManager == null) return;

            if (!TryParseInputs(out int charIndex, out int wpIndex))
            {
                Debug.LogWarning("[TestManager] 유효하지 않은 입력값입니다.");
                return;
            }

            m_isChanging = true;
            SetInteractable(false); // 모든 UI 잠금

            try
            {
                var dataManager = PlayerDataManagerDontdesytoy.Instance;
                if (dataManager != null)
                {
                    dataManager.SelectCharacterIndex = charIndex;
                    dataManager.SelectWeaponIndex = wpIndex;
                }

                await m_gameManager.ChangeCharacterAndWeapon_Spawn();

                ApplyWeaponUpgradeState();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TestManager] 변경 중 오류 발생: {e.Message}");
            }
            finally
            {
                m_isChanging = false;
                SetInteractable(true); // UI 잠금 해제
            }
        }

        private bool TryParseInputs(out int charIndex, out int wpIndex)
        {
            charIndex = 0;
            wpIndex = 0;

            if (m_characterIndexInput == null || m_weaponIndexInput == null) return false;

            bool isCharValid = int.TryParse(m_characterIndexInput.text, out charIndex);
            bool isWpValid = int.TryParse(m_weaponIndexInput.text, out wpIndex);

            return isCharValid && isWpValid;
        }

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