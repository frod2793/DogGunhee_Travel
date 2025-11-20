using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 모든 무기의 기본 클래스입니다.
    /// 무기의 공통적인 능력치, 상태, 동작을 정의합니다.
    /// </summary>
    public abstract class Weaphon_base : MonoBehaviour
    {
        #region 필드 및 프로퍼티

        [Header("기본 능력치")]
        [Tooltip("무기의 기본 공격력입니다.")]
        public float attackPower;
        [Tooltip("공격 후 다음 공격까지의 대기 시간(초)입니다.")]
        public float coolTime;
        [Tooltip("투사체 속도 또는 공격 애니메이션 속도입니다.")]
        public float attackSpeed;
        [Tooltip("공격이 닿는 최대 범위입니다.")]
        public float attackRange;

        [Header("공격 특성")]
        [Tooltip("피격 대상에게 부여할 스턴 시간(초)입니다.")]
        public float mobStunTime;
        [Tooltip("투사체를 사용하는 무기인지 여부입니다.")]
        public bool isShooting;

        [Header("상태 및 업그레이드")]
        [Tooltip("현재 무기의 고유 인덱스입니다.")]
        public int weaphonIndex;
        [Tooltip("무기의 2단계 업그레이드 적용 여부입니다.")]
        public bool isUpgradelv2 = false;

        /// <summary>
        /// 무기의 현재 상태 (대기, 공격, 재장전)
        /// </summary>
        public enum WeaphonState
        {
            Idle,
            Attack,
            Reload
        }

        [Tooltip("무기의 현재 상태를 나타냅니다.")]
        [SerializeField] protected WeaphonState weaphonState;
        public WeaphonState CurrentState => weaphonState;

        #endregion

        #region Unity 라이프사이클

        protected virtual void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
        }

        protected virtual void OnDisable()
        {
            // 자식 클래스에서 필요 시 재정의
        }

        /// <summary>
        /// 에디터에서 값이 변경될 때 호출됩니다. (플레이 모드에서만 동작)
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                SetWeaphonState(weaphonState);
            }
        }

        #endregion

        #region 상태 관리

        /// <summary>
        /// 무기의 상태를 변경하고 해당 상태에 맞는 동작을 호출합니다.
        /// </summary>
        /// <param name="state">변경할 새로운 상태</param>
        public void SetWeaphonState(WeaphonState state)
        {
            weaphonState = state;
            switch (state)
            {
                case WeaphonState.Idle:
                    Weaphon_Idle();
                    break;
                case WeaphonState.Attack:
                    // SetWeaphonState는 주로 상태 전환에 사용되므로, 실제 공격 각도는 Weaphon_Attack에서 직접 받습니다.
                    Weaphon_Attack(Vector3.zero);
                    break;
                case WeaphonState.Reload:
                    Weaphon_Reload();
                    break;
            }
        }

        #endregion

        #region 핵심 동작 (추상)

        /// <summary>
        /// 무기가 대기 상태일 때의 동작을 정의합니다.
        /// </summary>
        public virtual void Weaphon_Idle()
        {
            // 자식 클래스에서 재정의
        }

        /// <summary>
        /// 무기가 공격 상태일 때의 동작을 정의합니다.
        /// </summary>
        /// <param name="attackAngle">공격 방향 벡터</param>
        public virtual void Weaphon_Attack(Vector3 attackAngle)
        {
            // 자식 클래스에서 재정의
        }

        /// <summary>
        /// 무기가 재장전 상태일 때의 동작을 정의합니다.
        /// </summary>
        public virtual void Weaphon_Reload()
        {
            // 자식 클래스에서 재정의
        }

        #endregion
    }
}