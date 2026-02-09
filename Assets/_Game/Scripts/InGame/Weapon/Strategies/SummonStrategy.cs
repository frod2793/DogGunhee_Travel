using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers; 
using System.Linq;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 소환수(FriendCharacter)를 소환하는 전략입니다.
    /// WeaponFriendsLogic을 사용하여 위치와 타입을 결정합니다.
    /// </summary>
    public class SummonStrategy : IWeaponStrategy
    {
        #region 내부 변수

        private readonly WeaponFriendsLogic m_logic;
        private Camera m_camera;

        #endregion

        #region 생성자

        public SummonStrategy()
        {
            var spawnStrategy = new ViewportSpawnStrategy(); 
            m_logic = new WeaponFriendsLogic(spawnStrategy);
            m_camera = Camera.main;
        }

        #endregion

        public void Initialize(WeaponDataSO data)
        {
            // 데이터 기반 초기화가 필요하다면 여기서 수행
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_camera == null) m_camera = Camera.main;

            int count = stats.CurrentProjectileCount; // 소환 수
            
            // 1. 소환할 타입 결정 (셔플)
            var types = m_logic.GetNextFriendTypes(count).ToList();

            // 2. 소환
            foreach (var type in types)
            {
                // 위치 계산
                Vector3 spawnPos = m_logic.CalculateSpawnPosition(owner, m_camera);
                
                // 풀에서 친구 가져오기
                var friend = WeaponPoolManager.Instance.Get<FriendCharacter>();
                if (friend != null)
                {
                    friend.transform.position = spawnPos;
                    friend.gameObject.SetActive(true);
                    
                    // 초기화
                    friend.Initialize(spawnPos, type, stats.CurrentAttackPower, stats.MobStunTime);
                }
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 소환수는 독자 행동하므로 업데이트 없음
        }
    }
}
