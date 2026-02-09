using UnityEngine;
using R3;
using System;
using InGame;
using InGame.Manager;
using InGame.Weapon.Base;
using InGame.Weapon.Strategies;

namespace InGame.Weapon.Core
{
    /// <summary>
    /// 무기의 상태와 쿨타임을 관리하는 Context 클래스입니다.
    /// 구체적인 공격 동작은 IWeaponStrategy에게 위임합니다.
    /// </summary>
    public class WeaponController : IWeaponController
    {
        #region 내부 상태 및 변수

        private WeaponDataSO m_data;
        private SkillData m_skillData;
        private WeaponRuntimeStats m_stats;
        private readonly IWeaponStrategy m_strategy;
        private Transform m_owner;
        private Func<Vector3> m_targetProvider;
        private readonly CompositeDisposable m_disposables = new();
        
        private float m_currentCooldown;

        #endregion

        #region 프로퍼티 (IWeaponController 구현)

        public WeaponRuntimeStats Stats => m_stats;
        public string SkillCode => m_data?.SkillCode ?? string.Empty;
        public string WeaponName => m_data?.WeaponName ?? string.Empty;
        public SkillData SkillData { get => m_skillData; set => m_skillData = value; }
        public Sprite Thumbnail => SkillData?.skillIcon;
        public int CurrentLevel => m_stats?.CurrentLevel ?? 1;
        public int MaxLevel => 6;
        public bool IsEvolved => m_stats?.IsEvolved ?? false;

        #endregion

        #region 초기화 및 생성자

        /// <summary>
        /// 무기 전략(Strategy)을 주입받아 컨트롤러를 생성합니다.
        /// </summary>
        public WeaponController(IWeaponStrategy strategy)
        {
            m_strategy = strategy;
            m_currentCooldown = 0f;
        }

        public void Init(WeaponDataSO data, Transform owner, Func<Vector3> getTargetDirection)
        {
            m_data = data;
            m_stats = new WeaponRuntimeStats(data);
            m_owner = owner;
            m_targetProvider = getTargetDirection;

            // 전략 클래스 내부 초기화 (풀 등록 등)
            m_strategy?.Init(data);

            // [디버그] 스탯 실시간 모니터링 구독 루틴
            m_disposables.Clear();
            m_stats.AttackPowerRP.Subscribe(val => Debug.Log($"[{data.name}] 공격력 변경: {val}")).AddTo(m_disposables);
            m_stats.CurrentLevelRP.Skip(1).Subscribe(lv => Debug.Log($"[{data.name}] 레벨 업: {lv}")).AddTo(m_disposables);
        }

        #endregion

        #region 생명주기 및 업데이트 루프

        public void OnUpdate(float deltaTime)
        {
            // 쿨타임 처리
            if (m_currentCooldown > 0f)
            {
                m_currentCooldown -= deltaTime;
            }

            // 전략별 프레임 업데이트 수행 (지속 피해 등)
            m_strategy?.OnUpdate(m_stats, deltaTime);
            
            // 자동 공격 로직 실행
            if (m_currentCooldown <= 0f)
            {
                // 월드에 적이 존재하지 않으면 공격 스킵
                if (GameManager.Instance.ObjectPoolSpawner == null || GameManager.Instance.ObjectPoolSpawner.ActiveMobCount <= 0)
                {
                    return;
                }

                Vector3 direction = m_targetProvider?.Invoke() ?? Vector3.zero;
                if (direction == Vector3.zero)
                {
                    return;
                }
                
                TryAttack(direction);
            }
        }

        public void OnLateUpdate()
        {
            // 필요 시 자식 클래스에서 확장 구현 가능
        }

        #endregion

        #region 공격 및 성장 로직

        /// <summary>
        /// 쿨타임 및 사거리를 체크한 후 최종적으로 공격을 시도합니다.
        /// </summary>
        public bool TryAttack(Vector3 direction)
        {
            if (m_currentCooldown > 0f)
            {
                return false;
            }

            // 무기 사거리 기반 필터링
            if (m_stats != null && m_stats.CurrentAttackRange > 0)
            {
                var autoAttack = GameManager.Instance.PlayerController?.AutoAttack;
                if (autoAttack != null && autoAttack.CurrentTarget != null)
                {
                    float dist = Vector3.Distance(m_owner.position, autoAttack.CurrentTarget.transform.position);
                    
                    // 판정 보정값 포함 체크
                    if (dist > m_stats.AttackRange * 1.1f) 
                    {
                        return false;
                    }
                }
            }

            // 전략 클래스에 실제 공격 처리 위임
            m_strategy?.Attack(m_stats, m_owner, direction);

            // 공격 속도 보정치를 포함한 차기 쿨타임 계산
            m_currentCooldown = m_stats.CurrentCoolTime / m_stats.CurrentAttackSpeed;
            
            return true;
        }

        public void Attack(Vector3 direction)
        {
            TryAttack(direction);
        }

        public void LevelUp()
        {
            m_stats.LevelUp(m_stats.CurrentLevel + 1);
        }

        #endregion

        #region 리소스 해제

        public void Dispose()
        {
            m_disposables.Dispose();
        }

        #endregion
    }
}
