using UnityEngine;
using System.Collections.Generic;
using InGame.Weapon.Base;
using InGame.Managers;
using InGame.ObjectPool;
using InGame.Core.Interfaces;
using InGame.Weapon.Controllers;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 플레이어 주위를 공전하는 투사체(Orbit Ball)를 관리하는 전략입니다.
    /// 무기 레벨(투사체 개수)에 따라 실시간으로 공 개수를 동기화합니다.
    /// </summary>
    public class OrbitProjectileStrategy : IWeaponStrategy
    {
        #region 내부 변수

        private WeaponDataSO m_data;
        private WeaponPoolManager m_poolManager;
        private BallWeaponView m_view; // 튜닝 데이터
        private GameObject m_ballPrefab;

        private readonly List<OrbitProjectile> m_activeBalls = new();

        // [추가]: 인터페이스 기반 의존성
        private IGameStateService m_gameState;
        private ICombatContext m_combatCtx;
        private IPlayerContext m_playerCtx;

        #endregion

        #region 인터페이스 구현

        public void Init(
            WeaponDataSO data, 
            WeaponPoolManager poolManager,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            m_data = data;
            m_poolManager = poolManager;

            m_gameState = gameState;
            m_combatCtx = combatContext;
            m_playerCtx = playerContext;
            m_ballPrefab = data.ProjectilePrefab;

            if (m_poolManager == null) return;

            // View 컴포넌트 설정 (전역 튜닝값)
            m_view = m_poolManager.GetComponent<BallWeaponView>();
            if (m_view == null)
            {
                m_view = m_poolManager.gameObject.AddComponent<BallWeaponView>();
            }

            // 투사체 풀 등록
            if (m_ballPrefab != null)
            {
                m_poolManager.GetOrAddPool<OrbitProjectile>(
                    createFunc: CreateBall,
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => Object.Destroy(p.gameObject),
                    defaultCapacity: 5,
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 공격 시마다 개수 동기화
            SyncBallCount(stats, owner);
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 플레이어가 살아있다면 계속 동기화 유지
            if (m_playerCtx != null && m_playerCtx.PlayerTransform != null)
            {
                SyncBallCount(stats, m_playerCtx.PlayerTransform);
            }
        }

        #endregion

        #region 상세 로직

        private void SyncBallCount(WeaponRuntimeStats stats, Transform owner)
        {
            if (m_poolManager == null) return;

            int targetCount = stats.ProjectileCount;

            // 부족한 개수만큼 추가 생성
            while (m_activeBalls.Count < targetCount)
            {
                var ball = m_poolManager.Get<OrbitProjectile>();
                if (ball != null)
                {
                    // 각도 등분
                    float angle = (360f / targetCount) * m_activeBalls.Count;

                    // 속도 및 오프셋 계산 (View 데이터 활용)
                    float baseSpeed = 180f * (m_view != null ? m_view.RotationSpeedMultiplier : 1.0f);
                    float rotSpeed = baseSpeed * stats.AttackSpeed;
                    float offset = m_view != null ? m_view.RotationOffset : 0f;
                    bool rotateAlong = m_view != null ? m_view.RotateWithOrbit : true;

                    ball.Init(owner, stats.AttackRange, rotSpeed, angle, stats, offset, rotateAlong);
                    m_activeBalls.Add(ball);
                }
                else
                {
                    break;
                }
            }
            
            // NOTE: 개수가 줄어드는 경우에 대한 처리가 필요하다면 여기에 추가 (현재는 늘어나는 것만 고려)
        }

        private OrbitProjectile CreateBall()
        {
            if (m_ballPrefab == null) return null;
            var go = Object.Instantiate(m_ballPrefab);
            return go.GetComponent<OrbitProjectile>() ?? go.AddComponent<OrbitProjectile>();
        }

        #endregion
    }
}