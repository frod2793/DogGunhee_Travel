using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Manager;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기 컨트롤러의 기본 구현을 제공하는 추상 클래스입니다. (POCO)
    /// </summary>
    public abstract class WeaponControllerBase : IWeaponController
    {
        protected WeaponDataSO m_data;
        protected WeaponRuntimeStats m_runtimeStats;
        protected Transform m_ownerTransform;
        protected float currentCooldownTimer;

        protected System.Func<Vector3> m_getTargetDirection;

        #region IWeaponController 속성 구현
        public string SkillCode => m_data?.SkillCode ?? string.Empty;
        public string WeaponName => m_data?.WeaponName ?? string.Empty;
        public SkillData SkillData { get; set; }
        public Sprite Thumbnail => SkillData?.skillIcon;

        public int CurrentLevel => m_runtimeStats?.CurrentLevel ?? 1;
        public virtual int MaxLevel => 6;
        public bool IsEvolved => m_runtimeStats?.IsEvolved ?? false;
        #endregion

        public virtual void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection)
        {
            this.m_data = data;
            this.m_runtimeStats = new WeaponRuntimeStats(data);
            this.m_ownerTransform = owner;
            this.m_getTargetDirection = getTargetDirection;
            this.currentCooldownTimer = 0f;
        }

        public virtual void OnUpdate(float deltaTime)
        {
            // 게임이 시작되지 않았거나 일시정지 상태면 로직 중단
            if (PlayStateManager.instance != null && !PlayStateManager.instance.IsPlaying)
            {
                return;
            }

            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer -= deltaTime;
            }

            // [Refactoring] OnUpdate에서는 자동 공격 시도만 수행합니다.
            if (CanAttack())
            {
                // [주의] Attack() 내부에서 쿨타임을 재설정하도록 변경되었습니다.
                Attack(m_getTargetDirection?.Invoke() ?? Vector3.zero);
            }
        }

        public virtual void OnLateUpdate()
        {
            // 기본적으로는 할 일이 없음, 필요시 오버라이드
        }

        public virtual void LevelUp()
        {
            m_runtimeStats.LevelUp(m_runtimeStats.CurrentLevel + 1);
            OnLevelUp();
        }

        public virtual void Dispose()
        {
            // 리소스 정리 필요 시 오버라이드
        }

        protected virtual bool CanAttack()
        {
            // 게임 상태 체크 추가
            if (PlayStateManager.instance != null && !PlayStateManager.instance.IsPlaying)
            {
                return false;
            }

            return currentCooldownTimer <= 0f;
        }

        /// <summary>
        /// 외부에서 공격을 요청할 때 쿨타임을 체크하고 실행합니다.
        /// </summary>
        public virtual void Attack(Vector3 direction)
        {
            if (!CanAttack()) return;

            ExecuteAttack(direction);

            // 쿨타임 재설정 (공격 속도 반영)
            float speed = m_runtimeStats.CurrentAttackSpeed > 0 ? m_runtimeStats.CurrentAttackSpeed : 1f;
            currentCooldownTimer = m_runtimeStats.CurrentCoolTime / speed;
        }

        /// <summary>
        /// 실제 무기별 공격 로직을 구현합니다.
        /// </summary>
        protected abstract void ExecuteAttack(Vector3 direction);
        
        protected virtual void OnLevelUp() { }
    }
}
