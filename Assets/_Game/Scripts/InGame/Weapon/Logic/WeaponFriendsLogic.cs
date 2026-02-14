using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Weapon.Strategies;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// [설명]: 친구 소환 무기(Friends)의 비즈니스 로직(타입 셔플, 소환 위치 계산)을 담당하는 클래스입니다.
    /// </summary>
    public class WeaponFriendsLogic
    {
        #region 내부 변수

        private readonly ISpawnPositionStrategy m_spawnStrategy;
        private readonly FriendCharacter.FriendAnimationType[] m_allTypes;
        private readonly List<FriendCharacter.FriendAnimationType> m_shuffledTypes;

        #endregion

        #region 생성자 및 초기화

        public WeaponFriendsLogic(ISpawnPositionStrategy spawnStrategy)
        {
            m_spawnStrategy = spawnStrategy;
            m_allTypes = (FriendCharacter.FriendAnimationType[])Enum.GetValues(typeof(FriendCharacter.FriendAnimationType));
            m_shuffledTypes = new List<FriendCharacter.FriendAnimationType>(m_allTypes);
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 이번 소환 주기에 사용할 친구 타입들을 반환합니다.
        /// 중복을 최소화하기 위해 셔플 로직을 사용합니다.
        /// </summary>
        /// <param name="count">소환할 친구 수</param>
        public IEnumerable<FriendCharacter.FriendAnimationType> GetNextFriendTypes(int count)
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
                // 타입 종류보다 더 많이 요청하면 랜덤 반환 (중복 허용)
                for (int i = 0; i < count; i++)
                {
                    yield return (FriendCharacter.FriendAnimationType)UnityEngine.Random.Range(0, m_allTypes.Length);
                }
            }
        }

        /// <summary>
        /// 주입받은 전략(Strategy)을 사용하여 소환 위치를 계산합니다.
        /// </summary>
        public Vector3 CalculateSpawnPosition(Transform owner, Camera camera)
        {
            return m_spawnStrategy.GetSpawnPosition(owner);
        }

        /// <summary>
        /// Fisher-Yates 알고리즘을 사용하여 친구 타입 리스트를 무작위로 섞습니다.
        /// </summary>
        private void ShuffleTypes()
        {
            int n = m_shuffledTypes.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                (m_shuffledTypes[k], m_shuffledTypes[n]) = (m_shuffledTypes[n], m_shuffledTypes[k]);
            }
        }

        #endregion
    }
}