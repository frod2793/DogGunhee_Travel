using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using InGame.ObjectPool;
using UnityEngine.Serialization;
using Vamser_like.Weaphon.Base;

namespace Vamser_like.Weaphon
{
    /// <summary>
    /// 친구 캐릭터를 소환하여 공격하는 무기 컨트롤러입니다.
    /// </summary>
    public class WeaPhon_Friends : WeaphonBase
    {
        [Header("친구 소환 설정")]
        [SerializeField] private FriendCharacter m_friendCharacterPrefab;
        [Tooltip("한 번에 소환될 친구 캐릭터의 수입니다.")]
        [SerializeField] private int m_friendsPerAttack = 3;
        [Tooltip("풀에 미리 생성해둘 친구 캐릭터의 총 개수입니다.")]
        [SerializeField] private int m_poolSize = 10;
        [Tooltip("친구 캐릭터 소환 전 경고 애니메이션 또는 지연 시간입니다.")]
        [SerializeField] private Animator m_warningAnimator; // 경고 애니메이터 추가
        [SerializeField] private float m_warningDelay = 0.5f;

        private Camera m_mainCamera;
        private CancellationTokenSource m_attackLoopCts;

        private void Awake()
        {
            m_mainCamera = Camera.main;
            // 경고 애니메이터가 있다면 초기에는 비활성화
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(false);
            }
        }

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            
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
        
        public override void Weaphon_Attack(Vector3 attackAngle) { /* 공격 로직은 AttackLoopAsync에서 처리 */ }

        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float speed = this.attackSpeed > 0 ? this.attackSpeed : 1f;
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime / speed), cancellationToken: token);

                // 경고 애니메이션 또는 지연 시간
                if (m_warningAnimator != null)
                {
                    m_warningAnimator.gameObject.SetActive(true);
                    // 애니메이터 상태 업데이트를 위해 한 프레임 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    
                    AnimatorStateInfo warningStateInfo = m_warningAnimator.GetCurrentAnimatorStateInfo(0);
                    float warningAnimLength = warningStateInfo.length / (warningStateInfo.speed > 0 ? warningStateInfo.speed : 1f);
                    
                    await UniTask.Delay(System.TimeSpan.FromSeconds(warningAnimLength), ignoreTimeScale: true, cancellationToken: token);
                    m_warningAnimator.gameObject.SetActive(false);
                }
                else
                {
                    // 경고 애니메이터가 없으면 기존 지연 시간을 사용
                    await UniTask.Delay(TimeSpan.FromSeconds(m_warningDelay), cancellationToken: token);
                }

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

            for (int i = 0; i < m_friendsPerAttack; i++)
            {
                Vector3 randomPosition = GetRandomPositionInView();
                FriendCharacter friend = WeaponPoolManager.Instance.Get<FriendCharacter>();
                
                if (friend == null)
                {
                    Debug.LogWarning("Failed to get FriendCharacter from pool.");
                    continue;
                }

                // 4가지 애니메이션 타입 중 랜덤 선택
                FriendAnimationType randomAnimType = (FriendAnimationType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(FriendAnimationType)).Length);
                
                friend.Initialize(randomPosition, randomAnimType, this.attackPower, this.mobStunTime);
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