using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using InGame.Manager;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 몬스터의 시각적 표현(애니메이션, 렌더링, 트윈)을 담당하는 MonoBehaviour 클래스입니다.
    /// <br/> MobLogic에서 발생하는 이벤트를 구독하여 화면에 출력합니다.
    /// </summary>
    public class MobView : MonoBehaviour
    {
        #region 1. 컴포넌트 캐시 및 설정

        [Header("시각적 컴포넌트")]
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private Animator m_animator;

        [Header("회전 설정")]
        [SerializeField, Tooltip("방향 전환 속도")] private float m_flipSpeed = 15f;
        
        private Transform m_cachedTransform;
        private Tween m_moveTween;
        private Quaternion m_targetRotation;

        // 애니메이션 파라미터 (필요 시 정의)
        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        #endregion

        #region 2. 유니티 생명주기

        private void Awake()
        {
            m_cachedTransform = transform;
            m_targetRotation = transform.rotation;
            if (m_spriteRenderer == null) m_spriteRenderer = GetComponent<SpriteRenderer>();
            if (m_animator == null) m_animator = GetComponent<Animator>();
        }

        private void Update()
        {
            // 부드러운 회전 보간 (Slerp)
            if (Quaternion.Angle(m_cachedTransform.rotation, m_targetRotation) > 0.01f)
            {
                m_cachedTransform.rotation = Quaternion.Slerp(
                    m_cachedTransform.rotation, 
                    m_targetRotation, 
                    Time.deltaTime * m_flipSpeed
                );
            }
        }

        private void OnDisable()
        {
            m_moveTween?.Kill();
        }

        #endregion

        #region 3. 공개 메서드 (Logic/Controller에서 호출)

        /// <summary>
        /// 위치를 즉시 업데이트하거나 트윈으로 부드럽게 이동시킵니다.
        /// </summary>
        public void UpdatePosition(Vector3 newPos, bool immediate = true)
        {
            // 이동 방향에 따른 좌우 반전 (위치 업데이트 전에 수행)
            float deltaX = newPos.x - m_cachedTransform.position.x;
            Flip(deltaX);

            if (immediate)
            {
                m_moveTween?.Kill();
                m_cachedTransform.position = newPos;
            }
            else
            {
                // 필요 시 DOTween을 사용한 부드러운 보정
                m_moveTween?.Kill();
                m_moveTween = m_cachedTransform.DOMove(newPos, 0.1f).SetEase(Ease.Linear);
            }
        }

        /// <summary>
        /// 상태에 맞는 애니메이션을 재생합니다.
        /// </summary>
        public void OnStateChanged(MobBase.MobBase.MobState state)
        {
            switch (state)
            {
                case MobBase.MobBase.MobState.Idle:
                    if (m_animator != null) m_animator.SetFloat(k_AnimWalk, 0f);
                    break;
                case MobBase.MobBase.MobState.Move:
                    if (m_animator != null) m_animator.SetFloat(k_AnimWalk, 1f);
                    break;
                case MobBase.MobBase.MobState.Die:
                    if (m_animator != null) m_animator.SetTrigger(k_AnimDie);
                    break;
            }
        }

        /// <summary>
        /// 피격 연출을 수행합니다.
        /// </summary>
        public void PlayDamageEffect(Color? color = null)
        {
            if (EffectManager.Instance != null && m_spriteRenderer != null)
            {
                EffectManager.Instance.PlayQueuedFlashEffect(m_spriteRenderer, color).Forget();
            }
        }

        #endregion

        #region 4. 내부 유틸리티

        private void Flip(float dirX)
        {
            if (Mathf.Abs(dirX) > 0.001f) // 감도 상향 (0.01 -> 0.001)
            {
                float yRotation = dirX > 0 ? 180f : 0f;
                m_targetRotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        #endregion
    }
}
