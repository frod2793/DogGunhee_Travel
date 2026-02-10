using System;
using System.Collections.Generic;
using System.Linq; 
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Manager;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 화면 내 무작위 위치에 친구 캐릭터(FriendCharacter)를 소환하여 적을 공격하는 무기 컨트롤러입니다.
    /// <br/> 투사체가 아닌 독립적인 캐릭터를 소환하며, 별도의 비동기 루프로 소환 주기를 관리합니다.
    /// </summary>
    public class FriendsWeaponController : WeaponControllerBase
    {
        #region 1. 내부 변수 및 컴포넌트 (State & Components)

        // 프리팹 및 풀링
        private FriendCharacter m_friendCharacterPrefab;
        private int m_friendsPerAttack;
        private int m_poolSize;

        // 시스템 객체
        private Camera m_mainCamera;
        private CancellationTokenSource m_attackCts;

        // 랜덤 타입 선택을 위한 캐시
        private FriendCharacter.FriendAnimationType[] m_allFriendTypes;

        #endregion

        #region 2. 초기화 및 해제 (Init & Dispose)

        /// <summary>
        /// 무기를 초기화하고 친구 소환 풀을 생성하며 소환 루프를 시작합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, poolManager, getTargetDirection);

            // 1. 프리팹 컴포넌트 캐싱
            if (data.ProjectilePrefab != null)
            {
                m_friendCharacterPrefab = data.ProjectilePrefab.GetComponent<FriendCharacter>();
            }

            if (m_friendCharacterPrefab == null)
            {
                Debug.LogError($"[FriendsWeaponController] 데이터에 FriendCharacter 컴포넌트가 없습니다: {data.WeaponName}");
                return;
            }

            // 2. 설정 값 계산
            m_friendsPerAttack = data.BaseProjectileCount > 0 ? (int)data.BaseProjectileCount : 3;
            m_poolSize = m_friendsPerAttack * 3 + 5; // 동시 활성화 고려하여 여유 있게 설정
            
            m_mainCamera = Camera.main;
            
            // Enum 타입 캐싱 (매번 GetValues 호출 방지)
            m_allFriendTypes = (FriendCharacter.FriendAnimationType[])Enum.GetValues(typeof(FriendCharacter.FriendAnimationType));

            // 3. 오브젝트 풀 등록
            RegisterPool();

            // 4. 소환 루프 시작
            StartAttackLoop();
        }

        /// <summary>
        /// 소환수 전용 오브젝트 풀을 등록합니다.
        /// </summary>
        private void RegisterPool()
        {
            if (m_poolManager == null) return;

            m_poolManager.GetOrAddPool<FriendCharacter>(
                createFunc: CreateFriendCharacter,
                actionOnGet: OnGetFriendCharacter,
                actionOnRelease: OnReleaseFriendCharacter,
                actionOnDestroy: OnDestroyFriendCharacter,
                maxSize: m_poolSize
            );
        }

        public override void Dispose()
        {
            StopAttackLoop();
            base.Dispose();
        }

        #endregion

        #region 3. 공격 루프 (Attack Loop)

        private void StartAttackLoop()
        {
            StopAttackLoop();
            m_attackCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackCts.Token).Forget();
        }

        private void StopAttackLoop()
        {
            if (m_attackCts != null)
            {
                m_attackCts.Cancel();
                m_attackCts.Dispose();
                m_attackCts = null;
            }
        }

        /// <summary>
        /// 쿨타임마다 친구들을 소환하는 비동기 루프입니다.
        /// </summary>
        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 1. 쿨타임 대기
                    float speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                    float delay = Mathf.Max(0.1f, m_runtimeStats.CoolTime / speed);

                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);

                    // 2. 상태 체크
                    if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
                    {
                        continue;
                    }

                    // 3. 적 존재 여부 확인
                    if (!IsEnemyPresent)
                    {
                        continue;
                    }

                    // 4. 소환 실행
                    SpawnFriends();
                }
            }
            catch (OperationCanceledException)
            {
                // 루프 정상 종료
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FriendsWeapon] 소환 루프 오류: {ex.Message}");
            }
        }

        #endregion

        #region 4. 소환 로직 (Spawning Logic)

        /// <summary>
        /// 설정된 수만큼 친구 캐릭터를 랜덤한 위치에 소환합니다.
        /// <br/> 가능한 경우 서로 다른 종류의 친구가 나오도록 섞습니다.
        /// </summary>
        private void SpawnFriends()
        {
            if (m_friendCharacterPrefab == null || m_poolManager == null) return;

            // 랜덤 타입 선택 로직 (중복 최소화 셔플)
            List<FriendCharacter.FriendAnimationType> selectedTypes = GetShuffledTypes(m_friendsPerAttack);

            for (int i = 0; i < m_friendsPerAttack; i++)
            {
                // 1. 풀에서 가져오기
                FriendCharacter friend = m_poolManager.Get<FriendCharacter>();
                if (friend == null) continue;

                // 2. 위치 결정
                Vector3 spawnPos = GetRandomPositionInView();
                
                // 3. 타입 결정 (리스트 범위 내면 셔플된 값, 아니면 랜덤)
                var type = (i < selectedTypes.Count) 
                    ? selectedTypes[i] 
                    : m_allFriendTypes[UnityEngine.Random.Range(0, m_allFriendTypes.Length)];

                // 4. 초기화
                friend.Init(
                    spawnPos, 
                    type, 
                    m_runtimeStats.AttackPower, 
                    m_runtimeStats.MobStunTime, 
                    m_poolManager
                );
            }
        }

        /// <summary>
        /// 소환할 개수에 맞춰 중복되지 않는(가능하다면) 타입 리스트를 반환합니다.
        /// </summary>
        private List<FriendCharacter.FriendAnimationType> GetShuffledTypes(int count)
        {
            if (m_allFriendTypes == null || m_allFriendTypes.Length == 0) return new List<FriendCharacter.FriendAnimationType>();

            // 전체 타입 리스트 복사
            var list = new List<FriendCharacter.FriendAnimationType>(m_allFriendTypes);
            
            // Fisher-Yates Shuffle
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }

            // 필요한 개수만큼만 반환 (부족하면 전체 반환 후 루프에서 처리)
            return list.Take(count).ToList();
        }

        /// <summary>
        /// 카메라 뷰포트 내 랜덤 위치를 월드 좌표로 변환합니다.
        /// </summary>
        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null)
            {
                return m_ownerTransform != null ? m_ownerTransform.position : Vector3.zero;
            }

            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            
            // 2D 게임이므로 Z축 깊이는 카메라 위치 고려 (World z=0 유도)
            // Camera가 -10에 있다면 distance는 10
            float camDistance = -m_mainCamera.transform.position.z; 
            Vector3 viewportPos = new Vector3(randomX, randomY, camDistance);

            Vector3 worldPos = m_mainCamera.ViewportToWorldPoint(viewportPos);
            worldPos.z = 0f; // 확실하게 0으로 고정

            return worldPos;
        }

        #endregion

        #region 5. 상속 구현 (Override Methods)

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 이 무기는 자동 루프(AttackLoopAsync)로 동작하므로
            // 외부 강제 공격 호출은 무시합니다.
        }

        #endregion

        #region 6. 오브젝트 풀 델리게이트 (Pool Callbacks)

        private FriendCharacter CreateFriendCharacter()
        {
            if (m_friendCharacterPrefab == null) return null;
            return UnityEngine.Object.Instantiate(m_friendCharacterPrefab);
        }

        private void OnGetFriendCharacter(FriendCharacter friend)
        {
            friend.gameObject.SetActive(true);
        }

        private void OnReleaseFriendCharacter(FriendCharacter friend)
        {
            friend.gameObject.SetActive(false);
        }

        private void OnDestroyFriendCharacter(FriendCharacter friend)
        {
            if (friend != null)
            {
                UnityEngine.Object.Destroy(friend.gameObject);
            }
        }

        #endregion
    }
}