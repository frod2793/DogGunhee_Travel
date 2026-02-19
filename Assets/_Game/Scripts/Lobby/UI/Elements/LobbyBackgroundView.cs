using UnityEngine;

namespace Lobby.UI.Elements
{
    /// <summary>
    /// [설명]: 로비의 배경 애니메이션(연출)을 관리하는 뷰 컴포넌트입니다.
    /// </summary>
    public class LobbyBackgroundView : MonoBehaviour
    {
        #region 에디터 설정
        [SerializeField, Tooltip("배경 애니메이터")]
        private Animator m_backgroundAnimator;

        [SerializeField, Tooltip("배경 애니메이션 초기 재생 속도")]
        private float m_animationSpeed = 1.7f;
        #endregion

        #region 내부 변수
        private float m_cachedSpeed = -1f;
        #endregion

        #region 유니티 생명주기
        private void Awake()
        {
            if (m_backgroundAnimator != null)
            {
                m_backgroundAnimator.speed = m_animationSpeed;
            }
        }

        private void Update()
        {
            // 속도 실시간 갱신 (디버깅/튜닝용)
            if (m_backgroundAnimator != null && Mathf.Abs(m_cachedSpeed - m_animationSpeed) > Mathf.Epsilon)
            {
                m_cachedSpeed = m_animationSpeed;
                m_backgroundAnimator.speed = m_animationSpeed;
            }
        }
        #endregion

        #region 공개 메서드
        /// <summary>
        /// [설명]: 지정된 트리거로 애니메이션을 재생합니다.
        /// </summary>
        public void PlayAnimation(string triggerName)
        {
            if (m_backgroundAnimator != null && !string.IsNullOrEmpty(triggerName))
            {
                m_backgroundAnimator.SetTrigger(triggerName);
            }
        }

        /// <summary>
        /// [설명]: 애니메이션을 리셋합니다.
        /// </summary>
        public void StopAnimation()
        {
            if (m_backgroundAnimator != null)
            {
                m_backgroundAnimator.Rebind();
                m_backgroundAnimator.Update(0f);
            }
        }
        #endregion
    }
}
