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
    /// </summary>
    public class SummonStrategy : IWeaponStrategy
    {
        #region 내부 상태 및 변수

        private readonly WeaponFriendsLogic m_logic;
        private Camera m_camera;

        #endregion

        #region 생성자

        public SummonStrategy()
        {
            // 카메라 영역 내 스폰 전략 사용
            var spawnStrategy = new ViewportSpawnStrategy(); 
            m_logic = new WeaponFriendsLogic(spawnStrategy);
            m_camera = Camera.main;
        }

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            // 소환수 전략은 별도의 사전 데이터 초기화가 필요 없음
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_camera == null)
            {
                m_camera = Camera.main;
            }

            int count = stats.CurrentProjectileCount;
            
            // 1. 소환할 친구 타입 목록 결정 (셔플 포함)
            var types = m_logic.GetNextFriendTypes(count).ToList();

            // 2. 순차적으로 전장에 소환
            foreach (var type in types)
            {
                Vector3 spawnPos = m_logic.CalculateSpawnPosition(owner, m_camera);
                
                var friend = WeaponPoolManager.Instance.Get<FriendCharacter>();
                if (friend != null)
                {
                    friend.transform.position = spawnPos;
                    friend.gameObject.SetActive(true);
                    
                    friend.Init(spawnPos, type, stats.CurrentAttackPower, stats.MobStunTime);
                }
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 소환된 객체는 독자적으로 행동하므로 전략 레벨의 업데이트는 없음
        }

        #endregion
    }
}
