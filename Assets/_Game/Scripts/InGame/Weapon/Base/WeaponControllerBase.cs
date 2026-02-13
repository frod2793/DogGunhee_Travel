using UnityEngine;
using InGame.Manager;
using InGame.ObjectPool; // WeaponPoolManager 참조

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기 컨트롤러의 공통 기능을 정의하는 추상 클래스입니다.
    /// <br/> 쿨타임 관리, 스탯 연동, 타겟팅, 레벨업 등 모든 무기가 공유하는 기본 로직을 포함합니다.
    /// </summary>
    public abstract class WeaponControllerBase : IWeaponController
    {
        #region 1. 내부 상태 및 변수 (Fields)

        /// <summary>
        /// 무기 설정 데이터 (ScriptableObject 원본)
        /// </summary>
        protected WeaponDataSO m_data;

        /// <summary>
        /// 런타임에 변동되는 무기 스탯 관리 객체 (데미지, 쿨타임, 범위 등)
        /// </summary>
        protected WeaponRuntimeStats m_runtimeStats;

        /// <summary>
        /// 무기를 소유한 주체(플레이어)의 Transform
        /// </summary>
        protected Transform m_ownerTransform;

        /// <summary>
        /// 투사체 등을 생성할 때 사용할 오브젝트 풀 매니저
        /// </summary>
        protected WeaponPoolManager m_poolManager;

        /// <summary>
        /// 현재 남은 공격 재사용 대기시간 (초 단위)
        /// </summary>
        protected float m_currentCooldownTimer;

        /// <summary>
        /// 공격 방향을 결정하기 위한 외부 델리게이트 함수
        /// </summary>
        protected System.Func<Vector3> m_getTargetDirection;

        #endregion

        #region 2. 프로퍼티 (Properties)

        /// <summary>
        /// 무기 고유 식별 코드 (데이터가 없으면 빈 문자열 반환)
        /// </summary>
        public string SkillCode => m_data != null ? m_data.SkillCode : string.Empty;

        /// <summary>
        /// 표시용 무기 이름
        /// </summary>
        public string WeaponName => m_data != null ? m_data.WeaponName : string.Empty;

        /// <summary>
        /// 인게임 스킬 데이터 (UI 아이콘, 상세 수치 등 포함)
        /// </summary>
        public SkillData SkillData { get; set; }

        /// <summary>
        /// UI 표시용 썸네일 스프라이트
        /// </summary>
        public Sprite Thumbnail => SkillData?.skillIcon;

        /// <summary>
        /// 현재 무기 레벨 (초기값 1)
        /// </summary>
        public int CurrentLevel => m_runtimeStats?.CurrentLevel ?? 1;

        /// <summary>
        /// 최대 성장 가능 레벨 (기본값 6, 오버라이드 가능)
        /// </summary>
        public virtual int MaxLevel => 6;

        /// <summary>
        /// 무기가 진화(Evolution) 상태인지 여부
        /// </summary>
        public bool IsEvolved => m_runtimeStats?.IsEvolved ?? false;

        #endregion

        #region 3. 초기화 (Initialization)

        /// <summary>
        /// 무기 컨트롤러를 초기화하고 필요한 의존성을 주입합니다.
        /// </summary>
        /// <param name="data">무기 기본 설정 데이터</param>
        /// <param name="owner">무기 소유자 Transform</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        /// <param name="getTargetDirection">타겟 방향 계산 함수</param>
        public virtual void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager, System.Func<Vector3> getTargetDirection)
        {
            m_data = data;
            // 런타임 스탯 객체 생성 (데이터 원본 보호)
            m_runtimeStats = new WeaponRuntimeStats(data);
            
            m_ownerTransform = owner;
            m_poolManager = poolManager;
            m_getTargetDirection = getTargetDirection;
            
            m_currentCooldownTimer = 0f;
        }

        #endregion

        #region 4. 레벨 및 성장 (Leveling)

        /// <summary>
        /// 무기 레벨을 1단계 상승시키고 스탯을 재계산합니다.
        /// </summary>
        public virtual void LevelUp()
        {
            if (m_runtimeStats != null)
            {
                m_runtimeStats.LevelUp(m_runtimeStats.CurrentLevel + 1);
                OnLevelUp();
            }
        }

        /// <summary>
        /// 레벨업 직후 호출되는 훅(Hook) 메서드입니다. 
        /// <br/> 파생 클래스에서 특수 효과나 상태 갱신이 필요할 때 오버라이드합니다.
        /// </summary>
        protected virtual void OnLevelUp()
        {
            // 기본 구현 없음
        }

        #endregion

        #region 5. 생명주기 루프 (Lifecycle Loop)

        /// <summary>
        /// 매 프레임 호출되어 쿨타임을 갱신하고 공격을 시도합니다.
        /// </summary>
        /// <param name="deltaTime">프레임 경과 시간</param>
        public virtual void OnUpdate(float deltaTime)
        {
            // 게임이 플레이 중일 때만 로직 수행
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                return;
            }

            // 쿨타임 감소
            if (m_currentCooldownTimer > 0f)
            {
                m_currentCooldownTimer -= deltaTime;
            }

            // 공격 가능 상태인지 확인 후 공격 실행
            // 주의: CanAttack() 내부에서도 쿨타임 체크를 수행하므로 이중 체크 구조임
            if (CanAttack())
            {
                Vector3 direction = m_getTargetDirection?.Invoke() ?? Vector3.zero;

                if (direction != Vector3.zero)
                {
                    Attack(direction);
                }
            }
        }

        /// <summary>
        /// 프레임 후반부에 호출되는 업데이트입니다. (위치 보정 등)
        /// </summary>
        public virtual void OnLateUpdate()
        {
            // 기본 구현 없음
        }

        /// <summary>
        /// 무기가 제거될 때 리소스를 정리합니다.
        /// </summary>
        public virtual void Dispose()
        {
            // 기본 구현 없음
        }

        #endregion

        #region 6. 전투 로직 (Combat Logic)

        /// <summary>
        /// 현재 공격이 가능한지 여부를 판단합니다.
        /// <br/> 체크 항목: 게임 상태, 적 존재 여부, 쿨타임, 사거리(플레이어 타겟 기준)
        /// </summary>
        protected virtual bool CanAttack()
        {
            // 1. 게임 상태 체크
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                return false;
            }

            // 2. 적 존재 여부 체크
            if (!IsEnemyPresent)
            {
                return false;
            }

            // 3. 사거리 체크 (타겟이 지정된 경우)
            if (m_runtimeStats != null && m_runtimeStats.CurrentAttackRange > 0)
            {
                if (GameManager.Instance.PlayerController != null)
                {
                    var autoAttack = GameManager.Instance.PlayerController.AutoAttack;
                    
                    // 자동 공격 시스템이 타겟을 잡고 있다면 거리 계산
                    if (autoAttack != null && autoAttack.CurrentTarget != null)
                    {
                        float dist = Vector3.Distance(m_ownerTransform.position, autoAttack.CurrentTarget.Position);

                        // 사거리의 110% 까지는 공격 허용 (약간의 유예 범위)
                        if (dist > m_runtimeStats.CurrentAttackRange * 1.1f)
                        {
                            return false;
                        }
                    }
                }
            }

            // 4. 쿨타임 체크
            return m_currentCooldownTimer <= 0f;
        }

        /// <summary>
        /// 실제 공격 시퀀스를 실행하는 템플릿 메서드입니다.
        /// <br/> 조건을 재확인하고, 구체적인 공격(ExecuteAttack)을 수행한 뒤 쿨타임을 설정합니다.
        /// </summary>
        /// <param name="direction">공격 방향</param>
        public virtual void Attack(Vector3 direction)
        {
            if (!CanAttack())
            {
                return;
            }

            // 실제 공격 로직 (자식 클래스 구현)
            ExecuteAttack(direction);

            // 쿨타임 재설정 (공격 속도 반영)
            float attackSpeed = m_runtimeStats.CurrentAttackSpeed > 0 ? m_runtimeStats.CurrentAttackSpeed : 1f;
            m_currentCooldownTimer = m_runtimeStats.CurrentCoolTime / attackSpeed;
        }

        /// <summary>
        /// 각 무기별 고유한 공격 동작을 구현해야 하는 추상 메서드입니다.
        /// <br/> 예: 투사체 발사, 범위 데미지 적용, 오라 생성 등
        /// </summary>
        /// <param name="direction">계산된 공격 방향</param>
        protected abstract void ExecuteAttack(Vector3 direction);

        #endregion

        #region 7. 내부 유틸리티 (Helpers)

        /// <summary>
        /// 현재 맵에 활성화된 몬스터가 있는지 확인합니다.
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
    }
}