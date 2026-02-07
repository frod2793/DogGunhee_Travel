using UnityEngine;
using Cysharp.Threading.Tasks;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기 컨트롤러의 기본 구현을 제공하는 추상 클래스입니다. (POCO)
    /// </summary>
    public abstract class WeaponControllerBase : IWeaponController
    {
        protected WeaponRuntimeStats m_runtimeStats;
        protected Transform m_ownerTransform;
        protected float currentCooldownTimer;

        protected System.Func<Vector3> m_getTargetDirection;

        public virtual void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection)
        {
            this.m_runtimeStats = new WeaponRuntimeStats(data);
            this.m_ownerTransform = owner;
            this.m_getTargetDirection = getTargetDirection;
            this.currentCooldownTimer = 0f;
        }

        public virtual void OnUpdate(float deltaTime)
        {
            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer -= deltaTime;
            }

            if (CanAttack())
            {
                Attack(m_getTargetDirection?.Invoke() ?? Vector3.zero);
                // 쿨타임 재설정 (공격 속도 반영)
                float speed = m_runtimeStats.CurrentAttackSpeed > 0 ? m_runtimeStats.CurrentAttackSpeed : 1f;
                currentCooldownTimer = m_runtimeStats.CurrentCoolTime / speed;
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
            return currentCooldownTimer <= 0f;
        }

        public abstract void Attack(Vector3 direction);
        protected virtual void OnLevelUp() { }
    }
}
