using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Weapon.Strategies;

namespace InGame.Weapon.Logics
{
    /// <summary>
    /// WeaponFriends의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// 친구 타입 셔플 및 소환 위치 계산 로직을 포함합니다.
    /// </summary>
    public class WeaponFriendsLogic
    {
        private readonly ISpawnPositionStrategy m_spawnStrategy;
        private readonly FriendAnimationType[] m_allTypes;
        private List<FriendAnimationType> m_shuffledTypes;

        public WeaponFriendsLogic(ISpawnPositionStrategy spawnStrategy)
        {
            m_spawnStrategy = spawnStrategy;
            m_allTypes = (FriendAnimationType[])Enum.GetValues(typeof(FriendAnimationType));
            m_shuffledTypes = new List<FriendAnimationType>(m_allTypes);
        }

        /// <summary>
        /// 한 번의 공격(다수 소환)에 사용할 친구 타입 리스트를 반환합니다.
        /// </summary>
        /// <param name="count">소환할 마리 수</param>
        public IEnumerable<FriendAnimationType> GetNextFriendTypes(int count)
        {
            // 요청 수보다 타입 종류가 많으면 셔플해서 중복 없이 반환
            if (count <= m_allTypes.Length)
            {
                ShuffleTypes();
                for (int i = 0; i < count; i++)
                {
                    yield return m_shuffledTypes[i];
                }
            }
            else
            {
                // 타입 종류보다 많이 요청하면 그냥 랜덤
                for (int i = 0; i < count; i++)
                {
                    yield return (FriendAnimationType)UnityEngine.Random.Range(0, m_allTypes.Length);
                }
            }
        }

        /// <summary>
        /// 소환 위치를 계산합니다.
        /// </summary>
        public Vector3 CalculateSpawnPosition(Camera camera)
        {
            return m_spawnStrategy.GetSpawnPosition(camera);
        }

        private void ShuffleTypes()
        {
            // Fisher-Yates Shuffle
            int n = m_shuffledTypes.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                (m_shuffledTypes[k], m_shuffledTypes[n]) = (m_shuffledTypes[n], m_shuffledTypes[k]);
            }
        }
    }
}
