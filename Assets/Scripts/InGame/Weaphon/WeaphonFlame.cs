using UnityEngine;
using UnityEngine.Pool; // ObjectPool은 더 이상 직접 사용하지 않지만, IObjectPool 인터페이스는 필요할 수 있습니다.
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using InGame.ObjectPool;
using Vamser_like.Weaphon.Base;

namespace Vamser_like.Weaphon
{
    /// <summary>
    /// 랜덤 위치에 불기둥을 소환하는 무기 컨트롤러입니다.
    /// </summary>
    public class WeaphonFlame : WeaphonBase
    {
        [Header("화염 공격 설정")]
        [SerializeField] private FlamePillar m_flamePillarPrefab;
        [Tooltip("화면에 동시에 존재할 수 있는 최대 불기둥 개수입니다.")]
        [SerializeField] private int m_maxActivePillars = 3;
        [SerializeField] private int m_poolSize = 10;

        [Header("지속 피해(DoT) 설정")]
        [Tooltip("직접 타격 데미지 대비 지속 피해의 총량 비율")]
        [SerializeField] private float m_dotDamageRatio = 0.5f;
        [Tooltip("지속 피해 총 시간")]
        [SerializeField] private float m_dotDuration = 3f;
        [Tooltip("지속 피해 틱 횟수")]
        [SerializeField] private int m_dotTicks = 3;

        // private IObjectPool<FlamePillar> m_flamePillarPool; // WeaponPoolManager가 관리하므로 제거
        private Camera m_mainCamera;
        
        private CancellationTokenSource m_attackLoopCts;
        private int m_currentActivePillars = 0;

        private void Awake()
        {
            m_mainCamera = Camera.main;
            // InitializePool(); // WeaponPoolManager가 풀을 초기화하므로 제거
        }

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            
            m_attackLoopCts?.Cancel();
            m_attackLoopCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackLoopCts.Token).Forget();

            // WeaponPoolManager를 통해 FlamePillar 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<FlamePillar>(
                CreateFlamePillar,
                OnGetFlamePillar,
                OnReleaseFlamePillar,
                OnDestroyFlamePillar,
                maxSize: m_poolSize
            );
        }

        private new void OnDisable()
        {
            m_attackLoopCts?.Cancel();
        }
        
        public override void Weaphon_Attack(Vector3 attackAngle) { }

        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float speed = this.attackSpeed > 0 ? this.attackSpeed : 1f;
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime / speed), cancellationToken: token);

                if (m_currentActivePillars >= m_maxActivePillars)
                {
                    continue;
                }

                Vector3 randomPosition = GetRandomPositionInView();
                // WeaponPoolManager를 통해 불기둥을 가져옵니다.
                FlamePillar pillar = WeaponPoolManager.Instance.Get<FlamePillar>();
                if (pillar == null)
                {
                    Debug.LogWarning("Failed to get FlamePillar from pool.");
                    continue;
                }
                
                float dotDamage = attackPower * m_dotDamageRatio;
                
                pillar.Activate(randomPosition, attackPower, dotDamage, m_dotDuration, m_dotTicks);
                // m_currentActivePillars++; // OnGetFlamePillar에서 처리
            }
        }

        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null) return transform.position;

            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            
            Vector3 viewportPos = new Vector3(randomX, randomY, 10);
            
            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #region Object Pooling Delegates (WeaponPoolManager에서 사용될 델리게이트)

        // private void InitializePool() { ... } // 제거

        private FlamePillar CreateFlamePillar()
        {
            if (m_flamePillarPrefab == null)
            {
                Debug.LogError("[WeaphonFlame] FlamePillar 프리팹이 할당되지 않았습니다!");
                return null;
            }
            return Instantiate(m_flamePillarPrefab);
        }

        private void OnGetFlamePillar(FlamePillar pillar)
        {
            // Activate에서 처리되므로 여기서는 SetActive(true)만
            pillar.gameObject.SetActive(true);
            m_currentActivePillars++; // 활성화된 불기둥 개수 증가
        }

        private void OnReleaseFlamePillar(FlamePillar pillar) 
        {
            pillar.gameObject.SetActive(false);
            m_currentActivePillars--; // 활성화된 불기둥 개수 감소
        }

        private void OnDestroyFlamePillar(FlamePillar pillar) => Destroy(pillar.gameObject);

        #endregion
    }
}