using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Core.Interfaces;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;
using System.Linq; // ToList() 사용

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 친구(FriendCharacter)를 소환하는 전략 클래스입니다.
    /// 뷰포트 내 랜덤 위치에 소환수를 배치합니다.
    /// </summary>
    public class SummonStrategy : IWeaponStrategy
    {
        #region 내부 변수

        private readonly WeaponFriendsLogic m_logic;
        private WeaponPoolManager m_poolManager;
        private Camera m_camera;

        #endregion

        #region 생성자

        public SummonStrategy()
        {
            // 기본 전략으로 ViewportSpawnStrategy 사용
            var spawnStrategy = new ViewportSpawnStrategy();
            m_logic = new WeaponFriendsLogic(spawnStrategy);
            m_camera = Camera.main;
        }

        #endregion

        #region 인터페이스 구현

        public void Init(
            WeaponDataSO data, 
            WeaponPoolManager poolManager,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            m_poolManager = poolManager;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_camera == null) m_camera = Camera.main;

            int count = stats.CurrentProjectileCount;

            // 소환할 타입 결정 (셔플)
            var types = m_logic.GetNextFriendTypes(count).ToList();

            // 순차 소환
            foreach (var type in types)
            {
                Vector3 spawnPos = m_logic.CalculateSpawnPosition(owner, m_camera);

                if (m_poolManager == null) continue;
                
                var friend = m_poolManager.Get<FriendCharacter>();
                if (friend != null)
                {
                    friend.transform.position = spawnPos;
                    friend.gameObject.SetActive(true);

                    friend.Init(spawnPos, type, stats.CurrentAttackPower, stats.MobStunTime, m_poolManager);
                }
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 소환수는 개별 행동하므로 업데이트 불필요
        }

        #endregion
    }
}