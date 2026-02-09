using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Manager;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 친구 캐릭터를 소환하여 공격하는 무기 로직을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class FriendsWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private FriendCharacter m_friendCharacterPrefab;
        private int m_friendsPerAttack;
        private int m_poolSize;

        #endregion

        #region 내부 상태

        private Camera m_mainCamera;
        private CancellationTokenSource m_attackLoopCts;

        #endregion

        #region 초기화

        /// <summary>
        /// 표준 초기화 메서드입니다. WeaponDataSO에서 프리팹을 로드하고 풀을 초기화합니다.
        /// </summary>
        public override void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 프리팹 매핑
            if (data.ProjectilePrefab != null)
            {
                m_friendCharacterPrefab = data.ProjectilePrefab.GetComponent<FriendCharacter>();
            }

            if (m_friendCharacterPrefab == null)
            {
                LogManager.LogError($"[FriendsWeaponController] 프리팹에 FriendCharacter 컴포넌트가 누락되었습니다: {data.WeaponName}");
            }

            // 2. 튜닝 데이터 설정 (기본값)
            // 추후 WeaponPoolManager > FriendsWeaponView 등을 통해 외부에서 주입 가능하도록 확장 권장
            m_friendsPerAttack = data.BaseProjectileCount > 0 ? data.BaseProjectileCount : 3; 
            m_poolSize = m_friendsPerAttack * 3 + 5; // 여유 있게 할당

            m_mainCamera = Camera.main;

            // 3. 풀 등록
            RegisterPool();

            // 4. 공격 루프 시작
            StartAttackLoop();
        }

        private void RegisterPool()
        {
            WeaponPoolManager.Instance.GetOrAddPool<FriendCharacter>(
                CreateFriendCharacter,
                OnGetFriendCharacter,
                OnReleaseFriendCharacter,
                OnDestroyFriendCharacter,
                maxSize: m_poolSize
            );
        }

        #endregion

        #region 공격 루프

        private void StartAttackLoop()
        {
            m_attackLoopCts?.Cancel();
            m_attackLoopCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackLoopCts.Token).Forget();
        }

        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                float delay = m_runtimeStats.CoolTime / speed;

                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);

                // 게임이 플레이 중이 아니면 스폰 생략
                if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
                {
                    continue;
                }

                // [Optimization] 적이 없으면 공격 로직 패스
                if (!IsEnemyPresent)
                {
                    continue;
                }

                SpawnFriends();
            }
        }

        private void SpawnFriends()
        {
            if (m_friendCharacterPrefab == null)
            {
                LogManager.LogError("FriendsWeaponController: FriendCharacter 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return;
            }

            // 모든 애니메이션 타입 가져오기
            var allTypes = (FriendCharacter.FriendAnimationType[])Enum.GetValues(typeof(FriendCharacter.FriendAnimationType));
            int typesCount = allTypes.Length;

            // 섞을 리스트 준비 (전체 타입 수 이하일 때 중복 방지용)
            List<FriendCharacter.FriendAnimationType> uniqueTypesList = null;
            if (m_friendsPerAttack <= typesCount)
            {
                uniqueTypesList = new List<FriendCharacter.FriendAnimationType>(allTypes);
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
                    LogManager.LogWarning("FriendsWeaponController: 풀에서 FriendCharacter를 가져오지 못했습니다.", LogManager.LogCategory.Weapon);
                    continue;
                }

                FriendCharacter.FriendAnimationType selectedType;

                if (uniqueTypesList != null)
                {
                    // 셔플된 리스트에서 순서대로 가져옴 (중복 없음 보장)
                    selectedType = uniqueTypesList[i];
                }
                else
                {
                    // 타입 수보다 많이 소환할 때는 완전 랜덤 (중복 허용)
                    selectedType = (FriendCharacter.FriendAnimationType)UnityEngine.Random.Range(0, typesCount);
                }

                friend.Initialize(randomPosition, selectedType, m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);
            }
        }

        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null) return m_ownerTransform.position;

            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);

            Vector3 viewportPos = new Vector3(randomX, randomY, 10);

            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // Friends는 자체 AttackLoop를 사용하므로 별도 Update 로직 불필요
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // Friends는 자동 공격 루프를 사용하므로 수동 Attack은 무시됩니다.
        }

        public override void Dispose()
        {
            m_attackLoopCts?.Cancel();
            m_attackLoopCts?.Dispose();
            m_attackLoopCts = null;
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private FriendCharacter CreateFriendCharacter()
        {
            if (m_friendCharacterPrefab == null)
            {
                LogManager.LogError("FriendsWeaponController: FriendCharacter 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }
            return UnityEngine.Object.Instantiate(m_friendCharacterPrefab);
        }

        private void OnGetFriendCharacter(FriendCharacter friend) => friend.gameObject.SetActive(true);

        private void OnReleaseFriendCharacter(FriendCharacter friend) => friend.gameObject.SetActive(false);

        private void OnDestroyFriendCharacter(FriendCharacter friend)
        {
            if (friend != null) UnityEngine.Object.Destroy(friend.gameObject);
        }

        #endregion
    }
}
