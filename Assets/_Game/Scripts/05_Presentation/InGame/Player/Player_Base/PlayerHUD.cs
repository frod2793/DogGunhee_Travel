using InGame.Player.Player_Base;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 인게임 UI(체력바 등) 요소를 관리하는 MonoBehaviour 컴포넌트입니다.
    /// 데이터 변경 이벤트를 구독하여 실시간으로 HUD 요소를 위젯 형태로 시각화합니다.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        #region 에디터 설정

        [Header("UI 참조")]
        [SerializeField, Tooltip("플레이어의 현재 체력을 표시할 UI 슬라이더 컴포넌트")]
        private Slider m_hpSliderPrefab;

        [SerializeField, Tooltip("체력바 위젯이 월드 상에서 추적할 대상 트랜스폼")]
        private Transform m_targetTransform;

        [SerializeField, Tooltip("타겟의 위치로부터 위젯이 표시될 상대적 좌표 간격")]
        private Vector3 m_offset = new Vector3(0, -0.5f, 0);

        #endregion

        #region 내부 필드

        /// <summary> 실제 UI 갱신 및 배치를 담당하는 로직 핸들러 </summary>
        private PlayerUIHandler m_uiHandler;

        /// <summary> 이벤트를 관찰할 플레이어 데이터 인스턴스 </summary>
        private PlayerBase m_playerCharacter;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 플레이어 데이터를 주입받아 UI 시스템과 핸들러를 구성합니다.
        /// </summary>
        /// <param name="player">관찰 대상 플레이어 기반 데이터</param>
        public void Initialize(PlayerBase player)
        {
            if (player == null)
            {
                return;
            }

            m_playerCharacter = player;

            // 추적 타겟이 명시되지 않은 경우 플레이어 본체 트랜스폼을 사용
            if (m_targetTransform == null)
            {
                m_targetTransform = player.transform;
            }

            // 시각화 로직을 담당할 POCO 핸들러 생성
            m_uiHandler = new PlayerUIHandler(m_hpSliderPrefab, m_targetTransform, m_offset);

            // 현재 수치로 첫 번째 갱신 수행
            m_uiHandler.UpdateHpUI(player.CurrentHealth, player.MaxHealth);

            // 데이터 변경 이벤트 바인딩
            m_playerCharacter.OnHealthChanged += OnHealthChanged;
        }

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 객체 파괴 시 등록된 구독을 해제하고 UI 리소스를 안전하게 정리합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnHealthChanged -= OnHealthChanged;
            }

            m_uiHandler?.Dispose();
        }

        #endregion

        #region 이벤트 응답 핸들러

        /// <summary>
        /// [설명]: 플레이어 체력 변경 이벤트 발생 시 호출되어 UI 게이지를 동기화합니다.
        /// </summary>
        /// <param name="current">현재 체력</param>
        /// <param name="max">최대 체력</param>
        private void OnHealthChanged(float current, float max)
        {
            m_uiHandler?.UpdateHpUI(current, max);
        }

        #endregion
    }
}
