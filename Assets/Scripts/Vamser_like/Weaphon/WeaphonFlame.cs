using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 랜덤 위치에 불기둥을 소환하는 무기 컨트롤러입니다.
    /// </summary>
    public class WeaphonFlame : WeaphonBase
    {
        [Header("화염 공격 설정")]
        [SerializeField] private FlamePillar m_flamePillarPrefab;
        [SerializeField] private int m_poolSize = 10;

        [Header("지속 피해(DoT) 설정")]
        [Tooltip("직접 타격 데미지 대비 지속 피해의 총량 비율")]
        [SerializeField] private float m_dotDamageRatio = 0.5f;
        [Tooltip("지속 피해 총 시간")]
        [SerializeField] private float m_dotDuration = 3f;
        [Tooltip("지속 피해 틱 횟수")]
        [SerializeField] private int m_dotTicks = 3;

        private IObjectPool<FlamePillar> m_flamePillarPool;
        private bool m_isAttacking;
        private Camera m_mainCamera;

        private void Awake()
        {
            m_mainCamera = Camera.main;
            InitializePool();
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (m_isAttacking) return;
            AttackAsync().Forget();
        }

        private async UniTaskVoid AttackAsync()
        {
            m_isAttacking = true;

            try
            {
                Vector3 randomPosition = GetRandomPositionInView();
                
                FlamePillar pillar = m_flamePillarPool.Get();
                
                float dotDamage = attackPower * m_dotDamageRatio;
                
                pillar.Activate(m_flamePillarPool, randomPosition, attackPower, dotDamage, m_dotDuration, m_dotTicks);

                float speed = this.attackSpeed > 0 ? this.attackSpeed : 1f;
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime / speed), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null) return transform.position;

            // 뷰포트 좌표 (0,0) ~ (1,1) 사이에서 랜덤 위치 생성
            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            
            Vector3 viewportPos = new Vector3(randomX, randomY, 10); // z는 카메라로부터의 거리
            
            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #region Object Pooling

        private void InitializePool()
        {
            if (m_flamePillarPrefab == null)
            {
                Debug.LogError("[WeaphonFlame] FlamePillar 프리팹이 할당되지 않았습니다!");
                return;
            }

            m_flamePillarPool = new ObjectPool<FlamePillar>(
                createFunc: () => Instantiate(m_flamePillarPrefab),
                actionOnGet: (p) => { /* Activate에서 처리 */ },
                actionOnRelease: (p) => p.gameObject.SetActive(false),
                actionOnDestroy: (p) => Destroy(p.gameObject),
                maxSize: m_poolSize
            );
        }

        #endregion
    }
}