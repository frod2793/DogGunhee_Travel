using UnityEngine;
using R3;
using System;
using InGame;
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
        private WeaponRuntimeStats m_stats;
        private readonly IWeaponStrategy m_strategy;
        private Transform m_owner;
        private Func<Vector3> m_targetProvider;
        private readonly CompositeDisposable m_disposables = new();
        
        private float m_currentCooldown;
        private WeaponDataSO m_data;
        private SkillData m_skillData;

        #region IWeaponController 식별자 및 데이터

        public string SkillCode => m_data?.SkillCode ?? string.Empty;
        public string WeaponName => m_data?.WeaponName ?? string.Empty;
        public SkillData SkillData { get => m_skillData; set => m_skillData = value; }
        public Sprite Thumnail => m_skillData?.skillIcon;

        #endregion

        #region IWeaponController 레벨 및 상태

        public int CurrentLevel => m_stats?.CurrentLevel ?? 1;
        public int MaxLevel => 6;
        public bool IsEvolved => m_stats?.IsEvolved ?? false;

        #endregion

        public WeaponRuntimeStats Stats => m_stats;

        // Factory에서는 Strategy만 주입하여 생성
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

            // Strategy 초기화 (풀 등록 등)
            m_strategy?.Initialize(data);

            // 스탯 변경 감지 로그 (디버깅용)
            // 기존 구독 해제 후 재구독 (재사용 시)
            m_disposables.Clear();
            
            m_stats.AttackPowerRP.Subscribe(val => Debug.Log($"[{data.name}] 공격력 변경: {val}")).AddTo(m_disposables);
            m_stats.CurrentLevelRP.Skip(1).Subscribe(lv => Debug.Log($"[{data.name}] 레벨 업: {lv}")).AddTo(m_disposables);
        }

        public void OnUpdate(float deltaTime)
        {
            // 쿨타임 감소
            if (m_currentCooldown > 0f)
            {
                m_currentCooldown -= deltaTime;
            }

            // 지속 효과 등 전략의 업데이트 호출
            m_strategy?.OnUpdate(m_stats, deltaTime);
            
            // Auto Attack Implementation
            if (m_currentCooldown <= 0f)
            {
                // 방향: TargetProvider가 있으면 사용, 없으면 Owner의 forward 등 기본값
                Vector3 direction = m_targetProvider?.Invoke() ?? (m_owner != null ? m_owner.forward : Vector3.forward);
                
                TryAttack(direction);
            }
        }

        public void OnLateUpdate()
        {
            // 필요 시 구현
        }

        public bool TryAttack(Vector3 direction)
        {
            if (m_currentCooldown > 0f) return false;

            // 공격 수행
            m_strategy?.Attack(m_stats, m_owner, direction);

            // 쿨타임 재설정
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

        public void Dispose()
        {
            m_disposables.Dispose();
        }
    }
}
