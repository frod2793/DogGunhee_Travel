using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weaphon.Base;

namespace InGame.Weaphon.Controllers
{
    /// <summary>
    /// 랜덤 위치에 불기둥을 소환하는 무기 로직을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class FlameWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private FlamePillar m_flamePillarPrefab;
        private int m_maxActivePillars;
        private int m_poolSize;

        private float m_dotDamageRatio;
        private float m_dotDuration;
        private int m_dotTicks;

        #endregion

        #region 내부 상태

        private Camera m_mainCamera;
        private int m_currentActivePillars;
        private CancellationTokenSource m_attackLoopCts;

        #endregion

        #region 초기화

        /// <summary>
        /// FlameWeaponController를 초기화합니다.
        /// </summary>
        /// <param name="data">무기 데이터 ScriptableObject</param>
        /// <param name="ownerTransform">소유자(플레이어)의 Transform</param>
        /// <param name="getTargetDirection">공격 방향을 가져오는 델리게이트 (Flame은 사용하지 않음)</param>
        /// <param name="flamePillarPrefab">불기둥 프리팹</param>
        /// <param name="maxActivePillars">동시에 존재할 수 있는 최대 불기둥 수</param>
        /// <param name="poolSize">풀 최대 크기</param>
        /// <param name="dotDamageRatio">직접 타격 대비 DoT 데미지 비율</param>
        /// <param name="dotDuration">DoT 지속 시간</param>
        /// <param name="dotTicks">DoT 틱 횟수</param>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            FlamePillar flamePillarPrefab,
            int maxActivePillars,
            int poolSize,
            float dotDamageRatio,
            float dotDuration,
            int dotTicks)
        {
            // 부모 클래스 초기화
            base.Init(data, ownerTransform, getTargetDirection);

            m_flamePillarPrefab = flamePillarPrefab;
            m_maxActivePillars = maxActivePillars;
            m_poolSize = poolSize;
            m_dotDamageRatio = dotDamageRatio;
            m_dotDuration = dotDuration;
            m_dotTicks = dotTicks;

            m_mainCamera = Camera.main;
            m_currentActivePillars = 0;

            // 풀 등록
            RegisterPool();

            // 공격 루프 시작
            StartAttackLoop();
        }

        private void RegisterPool()
        {
            WeaponPoolManager.Instance.GetOrAddPool<FlamePillar>(
                CreateFlamePillar,
                OnGetFlamePillar,
                OnReleaseFlamePillar,
                OnDestroyFlamePillar,
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

                if (m_currentActivePillars >= m_maxActivePillars)
                {
                    continue;
                }

                SpawnFlamePillar();
            }
        }

        private void SpawnFlamePillar()
        {
            Vector3 randomPosition = GetRandomPositionInView();

            FlamePillar pillar = WeaponPoolManager.Instance.Get<FlamePillar>();
            if (pillar == null)
            {
                LogManager.LogWarning("FlameWeaponController: FlamePillar 풀에서 가져오지 못했습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            float dotDamage = m_runtimeStats.AttackPower * m_dotDamageRatio;

            pillar.Activate(randomPosition, m_runtimeStats.AttackPower, dotDamage, m_dotDuration, m_dotTicks);
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
            // Flame은 자체 AttackLoop를 사용하므로 별도 Update 로직 불필요
        }

        public override void Attack(Vector3 direction)
        {
            // Flame은 자동 공격 루프를 사용하므로 수동 Attack은 무시됩니다.
        }

        public override void Dispose()
        {
            m_attackLoopCts?.Cancel();
            m_attackLoopCts?.Dispose();
            m_attackLoopCts = null;
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private FlamePillar CreateFlamePillar()
        {
            if (m_flamePillarPrefab == null)
            {
                LogManager.LogError("FlameWeaponController: FlamePillar 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }
            return UnityEngine.Object.Instantiate(m_flamePillarPrefab);
        }

        private void OnGetFlamePillar(FlamePillar pillar)
        {
            pillar.gameObject.SetActive(true);
            m_currentActivePillars++;
        }

        private void OnReleaseFlamePillar(FlamePillar pillar)
        {
            pillar.gameObject.SetActive(false);
            m_currentActivePillars--;
        }

        private void OnDestroyFlamePillar(FlamePillar pillar)
        {
            if (pillar != null)
            {
                UnityEngine.Object.Destroy(pillar.gameObject);
            }
        }

        #endregion
    }
}
