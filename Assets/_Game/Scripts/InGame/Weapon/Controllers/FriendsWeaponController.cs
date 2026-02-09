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
    /// 친구 캐릭터를 소환하여 화면 내 적들을 공격하는 무기 컨트롤러입니다.
    /// </summary>
    public class FriendsWeaponController : WeaponControllerBase
    {
        #region 내부 상태 및 변수

        private FriendCharacter m_friendCharacterPrefab;
        private int m_friendsPerAttack;
        private int m_poolSize;

        private Camera m_mainCamera;
        private CancellationTokenSource m_attackLoopCts;

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 소환수 풀 및 공격 루프를 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform ownerTransform, Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 프리팹 구성 요소 매핑
            if (data.ProjectilePrefab != null)
            {
                m_friendCharacterPrefab = data.ProjectilePrefab.GetComponent<FriendCharacter>();
            }

            if (m_friendCharacterPrefab == null)
            {
                LogManager.LogError($"[FriendsWeaponController] FriendCharacter 컴포넌트 유락: {data.WeaponName}");
            }

            // 2. 튜닝 데이터 설정 (기본값)
            m_friendsPerAttack = data.BaseProjectileCount > 0 ? data.BaseProjectileCount : 3;
            m_poolSize = m_friendsPerAttack * 3 + 5;

            m_mainCamera = Camera.main;

            // 3. 오브젝트 풀 등록
            RegisterPool();

            // 4. 자동 공격 루프 시작
            StartAttackLoop();
        }

        /// <summary>
        /// 소환수 오브젝트 풀을 등록합니다.
        /// </summary>
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

        /// <summary>
        /// 해제 시 비동기 루프를 중단합니다.
        /// </summary>
        public override void Dispose()
        {
            m_attackLoopCts?.Cancel();
            m_attackLoopCts?.Dispose();
            m_attackLoopCts = null;
        }

        #endregion

        #region 업데이트 및 실행 인터페이스

        public override void OnUpdate(float deltaTime)
        {
            // 전용 루프를 사용하므로 부모 로직만 수행
            base.OnUpdate(deltaTime);
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 자동 루프 방식이므로 수동 요청은 처리하지 않음
        }

        #endregion

        #region 공격 로직 및 소환 처리

        /// <summary>
        /// 자동 소환 루틴을 시작합니다.
        /// </summary>
        private void StartAttackLoop()
        {
            m_attackLoopCts?.Cancel();
            m_attackLoopCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackLoopCts.Token).Forget();
        }

        /// <summary>
        /// 쿨타임에 맞춰 친구들을 소환하는 루프입니다.
        /// </summary>
        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                float delay = m_runtimeStats.CoolTime / speed;

                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);

                if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
                {
                    continue;
                }

                if (!IsEnemyPresent)
                {
                    continue;
                }

                SpawnFriends();
            }
        }

        /// <summary>
        /// 설정된 개수만큼 친구 캐릭터를 랜덤하게 소환합니다.
        /// </summary>
        private void SpawnFriends()
        {
            if (m_friendCharacterPrefab == null)
            {
                return;
            }

            var allTypes = (FriendCharacter.FriendAnimationType[])Enum.GetValues(typeof(FriendCharacter.FriendAnimationType));
            int typesCount = allTypes.Length;

            // 중복 방지를 위한 셔플 리스트 준비
            List<FriendCharacter.FriendAnimationType> uniqueTypesList = null;
            if (m_friendsPerAttack <= typesCount)
            {
                uniqueTypesList = new List<FriendCharacter.FriendAnimationType>(allTypes);
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
                    continue;
                }

                FriendCharacter.FriendAnimationType selectedType;
                if (uniqueTypesList != null)
                {
                    selectedType = uniqueTypesList[i];
                }
                else
                {
                    selectedType = (FriendCharacter.FriendAnimationType)UnityEngine.Random.Range(0, typesCount);
                }

                // 초기화 메서드 호출 (Initialize -> Init)
                friend.Init(randomPosition, selectedType, m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);
            }
        }

        /// <summary>
        /// 카메라 뷰포트 내의 랜덤 월드 좌표를 반환합니다.
        /// </summary>
        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null)
            {
                return m_ownerTransform.position;
            }

            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            Vector3 viewportPos = new Vector3(randomX, randomY, 10f);

            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #endregion

        #region 오브젝트 풀 관리 델리게이트

        private FriendCharacter CreateFriendCharacter()
        {
            if (m_friendCharacterPrefab == null)
            {
                return null;
            }
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
