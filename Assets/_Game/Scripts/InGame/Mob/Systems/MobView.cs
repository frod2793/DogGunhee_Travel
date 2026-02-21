using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using InGame.Managers;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 몬스터의 시각적 표현(애니메이션, 렌더링, 트윈)을 담당하는 MonoBehaviour 클래스입니다.
    /// MobLogic에서 발생하는 이벤트를 구독하여 화면에 시각적으로 반영합니다.
    /// </summary>
    public class MobView : MonoBehaviour
    {
        #region 컴포넌트 캐시 및 설정

        [Header("시각적 컴포넌트")]
        [SerializeField]
        private SpriteRenderer m_spriteRenderer;

        [SerializeField]
        private Animator m_animator;

        [Header("회전 설정")]
        [SerializeField, Tooltip("방향 전환 속도")]
        private float m_flipSpeed = 15f;

        /// <summary> 트랜스폼 캐싱 </summary>
        private Transform m_cachedTransform;

        /// <summary> 이동 보간 트윈 </summary>
        private Tween m_moveTween;

        /// <summary> 부드러운 회전을 위한 목표 회전값 </summary>
        private Quaternion m_targetRotation;

        #region 애니메이션 파라미터

        /// <summary> 이동 상태 파라미터 해시 </summary>
        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");

        /// <summary> 사망 트리거 파라미터 해시 </summary>
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        #endregion

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 객체 생성 시 컴포넌트를 캐싱하고 초기 회전값을 설정합니다.
        /// </summary>
        private void Awake()
        {
            m_cachedTransform = transform;
            m_targetRotation = transform.rotation;

            if (m_spriteRenderer == null)
            {
                m_spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (m_animator == null)
            {
                m_animator = GetComponent<Animator>();
            }
        }

        /// <summary>
        /// [설명]: 매 프레임마다 목표 회전값으로 부드럽게 보간(Slerp)합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 객체 비활성화 시 실행 중인 모든 트윈을 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            m_moveTween?.Kill();
        }

        #endregion

        /// <summary> 이펙트 서비스 참조 </summary>
        private IEffectService m_effectService;

        #region 초기화 및 설정

        /// <summary>
        /// [설명]: 필요한 서비스 의존성을 주입받아 초기화합니다.
        /// </summary>
        public void Initialize(IEffectService effectService)
        {
            m_effectService = effectService;
        }

        #endregion

        #region 공개 메서드 (Logic/Controller 호출용)

        /// <summary>
        /// [설명]: 몬스터의 위치를 업데이트합니다. 좌우 반전 로직을 포함합니다.
        /// </summary>
        /// <param name="newPos">새로운 월드 위치</param>
        /// <param name="immediate">즉시 이동 여부 (false일 경우 부드러운 보간 수행)</param>
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
        /// [설명]: 몬스터의 상태 변화에 따라 적절한 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="state">변경된 상태</param>
        public void OnStateChanged(MobBase.MobBase.MobState state)
        {
            switch (state)
            {
                case MobBase.MobBase.MobState.Idle:
                    if (m_animator != null)
                    {
                        m_animator.SetFloat(k_AnimWalk, 0f);
                    }
                    break;

                case MobBase.MobBase.MobState.Move:
                    if (m_animator != null)
                    {
                        m_animator.SetFloat(k_AnimWalk, 1f);
                    }
                    break;

                case MobBase.MobBase.MobState.Die:
                    if (m_animator != null)
                    {
                        m_animator.SetTrigger(k_AnimDie);
                    }
                    break;
            }
        }

        /// <summary>
        /// [설명]: 데미지를 입었을 때의 시각적 효과(깜빡임 등)를 재생합니다.
        /// </summary>
        /// <param name="color">효과에 사용할 색상 (선택 사항)</param>
        public void PlayDamageEffect(Color? color = null)
        {
            if (m_effectService != null && m_spriteRenderer != null)
            {
                // [Refine]: 몬스터 전용 히트 효과 호출 (흰색 점멸)
                m_effectService.PlayMobHitEffect(m_spriteRenderer);
            }
        }

        #endregion

        #region 내부 유틸리티

        /// <summary>
        /// [설명]: 이동 방향에 따라 몬스터의 좌우 회전값을 설정합니다.
        /// </summary>
        /// <param name="dirX">X축 이동 방향</param>
        private void Flip(float dirX)
        {
            if (Mathf.Abs(dirX) > 0.001f) // 감도 상향
            {
                float yRotation = dirX > 0 ? 180f : 0f;
                m_targetRotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        #endregion
    }
}
