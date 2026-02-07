using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using InGame.ObjectPool;
using InGame.Weapon.Base;

// todo : 친구들 오브젝트 착지시 모래 먼지 이펙트 추가 

namespace InGame.Weapon
{
    /// <summary>
    /// 친구 캐릭터를 소환하여 공격하는 무기 컨트롤러입니다.
    /// </summary>
    public class WeaPhonFriends : WeaponBase
    {
        [Header("친구 소환 설정")]
        [SerializeField] private FriendCharacter m_friendCharacterPrefab;
        [Tooltip("한 번에 소환될 친구 캐릭터의 수입니다.")]
        [SerializeField] private int m_friendsPerAttack = 3;
        [Tooltip("풀에 미리 생성해둘 친구 캐릭터의 총 개수입니다.")]
        [SerializeField] private int m_poolSize = 10;
        
        private Camera m_mainCamera;
        private CancellationTokenSource m_attackLoopCts;

        private void Awake()
        {
            m_mainCamera = Camera.main;
        }

        private new void OnEnable()
        {
            SetWeaponState(WeaponState.Idle);
            
            m_attackLoopCts?.Cancel();
            m_attackLoopCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackLoopCts.Token).Forget();

            // WeaponPoolManager를 통해 FriendCharacter 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<FriendCharacter>(
                CreateFriendCharacter,
                OnGetFriendCharacter,
                OnReleaseFriendCharacter,
                OnDestroyFriendCharacter,
                maxSize: m_poolSize
            );
        }

        private new void OnDisable()
        {
            m_attackLoopCts?.Cancel();
        }
        
        public override void Weapon_Attack(Vector3 attackAngle) { /* 공격 로직은 AttackLoopAsync에서 처리 */ }

        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float speed = this.attackSpeed > 0 ? this.attackSpeed : 1f;
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime / speed), cancellationToken: token);

                SpawnFriends();
            }
        }

        private void SpawnFriends()
        {
            if (m_friendCharacterPrefab == null)
            {
                Debug.LogError("[WeaPhon_Friends] Friend Character Prefab이 할당되지 않았습니다!");
                return;
            }

            // 모든 애니메이션 타입 가져오기
            var allTypes = (FriendAnimationType[])Enum.GetValues(typeof(FriendAnimationType));
            int typesCount = allTypes.Length;

            // 섞을 리스트 준비 (전체 타입 수 이하일 때 중복 방지용)
            List<FriendAnimationType> uniqueTypesList = null;
            if (m_friendsPerAttack <= typesCount)
            {
                uniqueTypesList = new List<FriendAnimationType>(allTypes);
                // Fisher-Yates Shuffle 로 섞기
                for (int j = 0; j < uniqueTypesList.Count; j++)
                {
                    int rnd = UnityEngine.Random.Range(j, uniqueTypesList.Count);
                    (uniqueTypesList[j], uniqueTypesList[rnd]) = (uniqueTypesList[rnd], uniqueTypesList[j]);
                }
            }

            for (int i = 0; i < m_friendsPerAttack; i++)
            {
                Vector3 randomPosition = GetRandomPositionInView();
                FriendCharacter friend = WeaponPoolManager.Instance.Get<FriendCharacter>();
                
                if (friend == null)
                {
                    Debug.LogWarning("Failed to get FriendCharacter from pool.");
                    continue;
                }

                FriendAnimationType selectedType;

                if (uniqueTypesList != null)
                {
                    // 셔플된 리스트에서 순서대로 가져옴 (중복 없음 보장)
                    selectedType = uniqueTypesList[i];
                }
                else
                {
                    // 타입 수보다 많이 소환할 때는 완전 랜덤 (중복 허용)
                    selectedType = (FriendAnimationType)UnityEngine.Random.Range(0, typesCount);
                }
                
                friend.Initialize(randomPosition, selectedType, this.attackPower, this.mobStunTime);
            }
        }

        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null) return transform.position;

            // 화면의 뷰포트 내에서 랜덤 위치를 가져옵니다.
            // 0.1f ~ 0.9f 범위로 설정하여 화면 가장자리에 너무 붙지 않도록 합니다.
            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            
            Vector3 viewportPos = new Vector3(randomX, randomY, 10); // Z값은 카메라와의 거리에 따라 조절
            
            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #region Object Pooling Delegates

        private FriendCharacter CreateFriendCharacter()
        {
            if (m_friendCharacterPrefab == null)
            {
                Debug.LogError("[WeaPhon_Friends] FriendCharacter 프리팹이 할당되지 않았습니다!");
                return null;
            }
            return Instantiate(m_friendCharacterPrefab);
        }

        private void OnGetFriendCharacter(FriendCharacter friend)
        {
            friend.gameObject.SetActive(true);
            // Initialize에서 위치 및 애니메이션 설정
        }

        private void OnReleaseFriendCharacter(FriendCharacter friend) 
        {
            friend.gameObject.SetActive(false);
        }

        private void OnDestroyFriendCharacter(FriendCharacter friend) => Destroy(friend.gameObject);

        #endregion
    }
}