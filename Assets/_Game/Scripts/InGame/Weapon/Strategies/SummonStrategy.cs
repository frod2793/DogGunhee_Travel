using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;
using System.Linq;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 친구(FriendCharacter)를 소환하는 전략 클래스입니다.
    /// <br/> 뷰포트 내 랜덤 위치에 소환수를 배치합니다.
    /// </summary>
    public class SummonStrategy : IWeaponStrategy
    {
        #region 1. 내부 변수 (Internal State)

        private readonly WeaponFriendsLogic m_logic;
        private WeaponPoolManager m_poolManager;
        private Camera m_camera;

        #endregion

        #region 2. 생성자 (Constructor)

        public SummonStrategy()
        {
            // 기본 전략으로 ViewportSpawnStrategy 사용
            var spawnStrategy = new ViewportSpawnStrategy();
            m_logic = new WeaponFriendsLogic(spawnStrategy);
            m_camera = Camera.main;
        }

        #endregion

        #region 3. 인터페이스 구현 (IWeaponStrategy Implementation)

        public void Init(WeaponDataSO data, WeaponPoolManager poolManager)
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