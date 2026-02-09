using UnityEngine;
using InGame.Manager;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기 컨트롤러의 공통 기능을 담은 기본 추상 클래스입니다. (POCO)
    /// 모든 구체적 무기 컨트롤러는 이 클래스를 상속받아 구현하는 것이 권장됩니다.
    /// </summary>
    public abstract class WeaponControllerBase : IWeaponController
    {
        #region 내부 상태 및 변수

        /// <summary>
        /// 무기의 기획 데이터 (ScriptableObject)
        /// </summary>
        protected WeaponDataSO m_data;

        /// <summary>
        /// 실시간으로 변하는 무기 스탯 관리 객체
        /// </summary>
        protected WeaponRuntimeStats m_runtimeStats;

        /// <summary>
        /// 무기를 장착한 소유자의 위치 정보
        /// </summary>
        protected Transform m_ownerTransform;

        /// <summary>
        /// 현재 공격 재사용 대기시간 타이머
        /// </summary>
        protected float m_currentCooldownTimer;

        /// <summary>
        /// 공격할 대상의 방향을 결정하는 동적 함수
        /// </summary>
        protected System.Func<Vector3> m_getTargetDirection;

        #endregion

        #region 프로퍼티 (IWeaponController 구현)

        /// <summary>
        /// 무기의 고유 식별 코드
        /// </summary>
        public string SkillCode => m_data?.SkillCode ?? string.Empty;

        /// <summary>
        /// 무기 명칭
        /// </summary>
        public string WeaponName => m_data?.WeaponName ?? string.Empty;

        /// <summary>
        /// 런타임에 주입되는 스킬 데이터 시트 정보
        /// </summary>
        public SkillData SkillData { get; set; }

        /// <summary>
        /// UI에 표시될 아이콘 스프라이트
        /// </summary>
        public Sprite Thumbnail => SkillData?.skillIcon;

        /// <summary>
        /// 현재 무기의 강화 레벨
        /// </summary>
        public int CurrentLevel => m_runtimeStats?.CurrentLevel ?? 1;

        /// <summary>
        /// 진화 상태를 포함한 최대 강화 가능 레벨
        /// </summary>
        public virtual int MaxLevel => 6;

        /// <summary>
        /// 최종 단계 진화 여부
        /// </summary>
        public bool IsEvolved => m_runtimeStats?.IsEvolved ?? false;

        #endregion

        #region 보호된 유틸리티

        /// <summary>
        /// 현재 필드에 유효한 적이 스폰되어 있는지 확인합니다.
        /// </summary>
        protected bool IsEnemyPresent
        {
            get
            {
                if (GameManager.Instance != null && GameManager.Instance.ObjectPoolSpawner != null)
                {
                    return GameManager.Instance.ObjectPoolSpawner.ActiveMobCount > 0;
                }
                return false;
            }
        }

        #endregion

        #region 초기화 및 레벨 관리

        /// <summary>
        /// 컨트롤러를 초기화하고 기본 정보를 설정합니다.
        /// </summary>
        public virtual void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection)
        {
            m_data = data;
            m_runtimeStats = new WeaponRuntimeStats(data);
            m_ownerTransform = owner;
            m_getTargetDirection = getTargetDirection;
            m_currentCooldownTimer = 0f;
        }

        /// <summary>
        /// 명시적으로 무기 레벨을 1단계 상승시킵니다.
        /// </summary>
        public virtual void LevelUp()
        {
            m_runtimeStats.LevelUp(m_runtimeStats.CurrentLevel + 1);
            OnLevelUp();
        }

        /// <summary>
        /// 레벨업 발생 시 자식 클래스에서 수행할 추가 로직을 정의합니다.
        /// </summary>
        protected virtual void OnLevelUp()
        {
        }

        #endregion

        #region 생명주기 루프

        /// <summary>
        /// 매 프레임 업데이트 로직을 수행합니다. (쿨타임 및 공격 실행)
        /// </summary>
        public virtual void OnUpdate(float deltaTime)
        {
            // 인게임 플레이 중이 아닐 경우 로직 중단
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                return;
            }

            if (m_currentCooldownTimer > 0f)
            {
                m_currentCooldownTimer -= deltaTime;
            }

            // 조건 충족 시 타겟 방향을 확인하여 공격 실행
            if (CanAttack())
            {
                Vector3 direction = m_getTargetDirection?.Invoke() ?? Vector3.zero;
                
                if (direction != Vector3.zero)
                {
                    Attack(direction);
                }
            }
        }

        public virtual void OnLateUpdate()
        {
        }

        public virtual void Dispose()
        {
        }

        #endregion

        #region 공격 로직 인터페이스

        /// <summary>
        /// 현재 공격이 가능한 상황(쿨타임, 적 존재 여부, 사거리)인지 판단합니다.
        /// </summary>
        protected virtual bool CanAttack()
        {
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                return false;
            }

            if (!IsEnemyPresent)
            {
                return false;
            }

            // 무기별 사거리 유효성 체크 (타겟이 너무 멀면 공격 스킵)
            if (m_runtimeStats != null && m_runtimeStats.CurrentAttackRange > 0)
            {
                var autoAttack = GameManager.Instance.PlayerController?.AutoAttack;
                if (autoAttack != null && autoAttack.CurrentTarget != null)
                {
                    float dist = Vector3.Distance(m_ownerTransform.position, autoAttack.CurrentTarget.transform.position);
                    
                    // 판정 보정값(110%) 적용
                    if (dist > m_runtimeStats.CurrentAttackRange * 1.1f)
                    {
                        return false;
                    }
                }
            }

            return m_currentCooldownTimer <= 0f;
        }

        /// <summary>
        /// 전반적인 무기 공격 시퀀스를 관리하며 쿨타임을 갱신합니다.
        /// </summary>
        public virtual void Attack(Vector3 direction)
        {
            if (!CanAttack())
            {
                return;
            }

            ExecuteAttack(direction);

            // 공격 속도 보정을 고려한 쿨타임 재설정
            float speed = m_runtimeStats.CurrentAttackSpeed > 0 ? m_runtimeStats.CurrentAttackSpeed : 1f;
            m_currentCooldownTimer = m_runtimeStats.CurrentCoolTime / speed;
        }

        /// <summary>
        /// 구체적인 무기 공격 로직은 서브 클래스에서 상속받아 구현합니다.
        /// </summary>
        protected abstract void ExecuteAttack(Vector3 direction);

        #endregion
    }
}
