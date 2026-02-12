using InGame.Player.Player_Base;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 UI(체력바 등)를 관리하는 MonoBehaviour 컴포넌트입니다.
    /// <br/> PlayerController에서 분리되어 단일 책임 원칙(SRP)을 준수합니다.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("UI 참조")]
        [SerializeField, Tooltip("HP 슬라이더 프리팹")]
        private Slider m_hpSliderPrefab;

        [SerializeField, Tooltip("체력바가 따라다닐 타겟 (플레이어)")]
        private Transform m_targetTransform;
        
        [SerializeField, Tooltip("체력바 오프셋 (기본값: (0, -0.5, 0))")]
        private Vector3 m_offset = new Vector3(0, -0.5f, 0);

        #endregion

        #region 2. 내부 로직 및 변수

        private PlayerUIHandler m_uiHandler;
        private PlayerBase m_playerCharacter;

        #endregion

        #region 3. 초기화 (Initialization)

        /// <summary>
        /// 플레이어 캐릭터 정보를 받아 UI를 초기화하고 이벤트를 구독합니다.
        /// </summary>
        public void Initialize(PlayerBase player)
        {
            if (player == null) return;

            m_playerCharacter = player;
            
            // 타겟이 설정되지 않았다면 플레이어 Transform을 사용
            if (m_targetTransform == null)
            {
                m_targetTransform = player.transform;
            }

            // POCO 핸들러 생성
            m_uiHandler = new PlayerUIHandler(m_hpSliderPrefab, m_targetTransform, m_offset);
            
            // 초기 상태 갱신
            m_uiHandler.UpdateHpUI(player.CurrentHealth, player.MaxHealth);

            // 이벤트 구독
            m_playerCharacter.OnHealthChanged += OnHealthChanged;
        }

        #endregion

        #region 4. 유니티 생명주기 (Lifecycle)

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnHealthChanged -= OnHealthChanged;
            }

            // UI 리소스 정리
            m_uiHandler?.Dispose();
        }

        #endregion

        #region 5. 이벤트 핸들러 (Event Handlers)

        private void OnHealthChanged(float current, float max)
        {
            m_uiHandler?.UpdateHpUI(current, max);
        }

        #endregion
    }
}
