using UnityEngine;
using R3; // Reactive Extensions
using System;
using InGame.Manager;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Strategies;

namespace InGame.Weapon.Core
{
    /// <summary>
    /// 무기의 공통 상태(쿨타임, 레벨, 스탯)를 관리하고 전략(Strategy)을 실행하는 컨텍스트 클래스입니다.
    /// <br/> IWeaponController 인터페이스를 구현하며, 구체적인 공격 로직은 IWeaponStrategy에게 위임합니다.
    /// </summary>
    public class WeaponController : IWeaponController
    {
        #region 1. 내부 변수 및 상태 (Internal State)

        // 데이터 및 스탯
        private WeaponDataSO m_data;
        private SkillData m_skillData;
        private WeaponRuntimeStats m_stats;

        // 전략 및 타겟팅
        private readonly IWeaponStrategy m_strategy;
        private Transform m_ownerTransform;
        private Func<Vector3> m_targetProvider;

        // 시스템 및 관리
        private WeaponPoolManager m_poolManager;
        private readonly CompositeDisposable m_disposables = new(); // R3 구독 관리

        // 런타임 변수
        private float m_currentCooldown;

        #endregion

        #region 2. 프로퍼티 (Properties - IWeaponController Implementation)

        /// <summary>
        /// 무기 런타임 스탯 (외부 접근용)
        /// </summary>
        public WeaponRuntimeStats Stats => m_stats;

        public string SkillCode => m_data != null ? m_data.SkillCode : string.Empty;
        public string WeaponName => m_data != null ? m_data.WeaponName : string.Empty;

        public SkillData SkillData
        {
            get => m_skillData;
            set => m_skillData = value;
        }

        public Sprite Thumbnail => SkillData?.skillIcon;
        public int CurrentLevel => m_stats?.CurrentLevel ?? 1;
        public int MaxLevel => 6; // 기획 데이터에 따라 변동 가능
        public bool IsEvolved => m_stats?.IsEvolved ?? false;

        #endregion

        #region 3. 초기화 및 생성자 (Initialization)

        /// <summary>
        /// 특정 공격 전략을 가진 무기 컨트롤러를 생성합니다.
        /// </summary>
        /// <param name="strategy">구체적인 무기 동작(공격, 업데이트)을 정의한 전략 객체</param>
        public WeaponController(IWeaponStrategy strategy)
        {
            m_strategy = strategy;
            m_currentCooldown = 0f;
        }

        /// <summary>
        /// 무기 데이터를 기반으로 컨트롤러를 초기화하고 의존성을 주입합니다.
        /// </summary>
        public void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager,
            System.Func<Vector3> getTargetDirection)
        {
            // 1. 기본 데이터 설정
            m_data = data;
            m_stats = new WeaponRuntimeStats(data);
            m_ownerTransform = owner;
            m_poolManager = poolManager;
            m_targetProvider = getTargetDirection;

            // 2. 전략 객체 초기화 (풀 등록 등 위임)
            if (m_strategy != null)
            {
                m_strategy.Init(data, m_poolManager);
            }

            // 3. R3 반응형 구독 설정 (디버깅 및 UI 연동용)
            m_disposables.Clear();

            // 공격력 변경 모니터링
            m_stats.AttackPowerRP
                .Subscribe(val => Debug.Log($"[{data.WeaponName}] 공격력 갱신: {val}"))
                .AddTo(m_disposables);

            // 레벨업 모니터링 (첫 값 스킵)
            m_stats.CurrentLevelRP
                .Skip(1)
                .Subscribe(lv => Debug.Log($"[{data.WeaponName}] 레벨 업! 현재 레벨: {lv}"))
                .AddTo(m_disposables);
        }

        #endregion

        #region 4. 업데이트 루프 (Update Loop)

        /// <summary>
        /// 매 프레임 호출되어 쿨타임 감소, 전략 업데이트, 자동 공격을 수행합니다.
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            // 1. 쿨타임 계산
            if (m_currentCooldown > 0f)
            {
                m_currentCooldown -= deltaTime;
            }

            // 2. 전략별 고유 업데이트 수행 (예: 오라 데미지, 투사체 회전 등)
            m_strategy?.OnUpdate(m_stats, deltaTime);

            // 3. 자동 공격 시도
            if (m_currentCooldown <= 0f)
            {
                AttemptAutoAttack();
            }
        }

        /// <summary>
        /// LateUpdate 로직이 필요할 경우 구현합니다.
        /// </summary>
        public void OnLateUpdate()
        {
            // 현재는 특별한 로직 없음
        }

        #endregion

        #region 5. 공격 및 로직 (Attack Logic)

        /// <summary>
        /// 자동 공격 조건을 검사하고 공격을 수행합니다.
        /// </summary>
        private void AttemptAutoAttack()
        {
            // 적 존재 여부 확인 (GameManager 의존)
            if (GameManager.Instance.ObjectPoolSpawner == null ||
                GameManager.Instance.ObjectPoolSpawner.ActiveMobCount <= 0)
            {
                return;
            }

            // 타겟 방향 계산
            Vector3 direction = m_targetProvider?.Invoke() ?? Vector3.zero;

            // 방향이 유효하지 않으면(0,0,0) 공격하지 않음
            if (direction == Vector3.zero)
            {
                return;
            }

            // 실제 공격 시도
            TryAttack(direction);
        }

        /// <summary>
        /// 쿨타임과 사거리를 확인한 후 전략 패턴을 통해 공격을 실행합니다.
        /// </summary>
        /// <param name="direction">공격 방향</param>
        /// <returns>공격 성공 여부</returns>
        public bool TryAttack(Vector3 direction)
        {
            // 1. 쿨타임 재확인
            if (m_currentCooldown > 0f)
            {
                return false;
            }

            // 2. 사거리 체크 (타겟이 너무 멀면 공격 안 함)
            if (m_stats != null && m_stats.CurrentAttackRange > 0)
            {
                // 플레이어의 현재 오토 타겟 참조
                if (GameManager.Instance.PlayerController != null)
                {
                    var autoAttack = GameManager.Instance.PlayerController.AutoAttack;
                    if (autoAttack != null && autoAttack.CurrentTarget != null)
                    {
                        float dist = Vector3.Distance(m_ownerTransform.position,
                            autoAttack.CurrentTarget.transform.position);

                        // 사거리의 110% 까지 허용 (보정치)
                        if (dist > m_stats.AttackRange * 1.1f)
                        {
                            return false;
                        }
                    }
                }
            }

            // 3. 전략 실행 (실제 투사체 발사 등)
            m_strategy?.Attack(m_stats, m_ownerTransform, direction);

            // 4. 쿨타임 갱신 (공격 속도 반영)
            float attackSpeed = m_stats.CurrentAttackSpeed > 0 ? m_stats.CurrentAttackSpeed : 1f;
            m_currentCooldown = m_stats.CurrentCoolTime / attackSpeed;

            return true;
        }

        /// <summary>
        /// 인터페이스 구현용 공격 메서드 (TryAttack 래핑)
        /// </summary>
        public void Attack(Vector3 direction)
        {
            TryAttack(direction);
        }

        /// <summary>
        /// 무기 레벨을 1단계 상승시킵니다.
        /// </summary>
        public void LevelUp()
        {
            if (m_stats != null)
            {
                m_stats.LevelUp(m_stats.CurrentLevel + 1);
            }
        }

        #endregion

        #region 6. 리소스 해제 (Dispose)

        /// <summary>
        /// 컨트롤러 제거 시 구독 및 리소스를 정리합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();
        }

        #endregion
    }
}