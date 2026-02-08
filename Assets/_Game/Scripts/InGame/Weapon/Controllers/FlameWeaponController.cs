using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Manager;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Controllers
{
    public class FlameWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private FlamePillar m_flamePillarPrefab;
        private int m_maxActivePillars;
        private int m_poolSize;
        
        // 튜닝 데이터 (기본값)
        private float m_dotDamageRatio = 0.5f;
        private float m_dotDuration;
        private int m_dotTicks;
        private Color m_hitFlashColor = Color.white;

        #endregion

        #region 내부 상태

        private Camera m_mainCamera;
        private int m_currentActivePillars;
        private CancellationTokenSource m_attackCts;

        #endregion

        #region 초기화

        public override void Init(WeaponDataSO data, Transform owner, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);

            // 1. 프리팹 매핑
            if (data.ProjectilePrefab != null)
            {
                m_flamePillarPrefab = data.ProjectilePrefab.GetComponent<FlamePillar>();
            }

            if (m_flamePillarPrefab == null)
            {
                LogManager.LogError($"[FlameWeaponController] FlamePillar 컴포넌트 누락: {data.WeaponName}");
            }

            // 2. 튜닝 데이터 추출 (WeaponPoolManager)
            FlameWeaponView view = null;
            if (WeaponPoolManager.Instance != null)
            {
                view = WeaponPoolManager.Instance.GetComponent<FlameWeaponView>();
            }

            if (view != null)
            {
                m_dotDamageRatio = view.DotDamageRatio;
                m_hitFlashColor = view.HitFlashColor;
                m_maxActivePillars = view.MaxActivePillars;
                m_poolSize = view.PoolSize;
            }
            else
            {
                m_maxActivePillars = data.BaseProjectileCount > 0 ? data.BaseProjectileCount : 3;
                m_poolSize = m_maxActivePillars + 2;
            }

            m_dotDuration = data.BaseDuration > 0 ? data.BaseDuration : 2.0f;
            m_dotTicks = Mathf.Max(1, Mathf.RoundToInt(m_dotDuration));

            m_mainCamera = Camera.main;
            m_currentActivePillars = 0;

            // 3. 풀 등록
            RegisterPool();

            // 4. 공격 루프 시작
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

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // Flame은 자체 AttackLoop를 사용하므로 별도 Update 로직 불필요
            // 단, 쿨타임 Timer 등은 부모에서 관리될 수 있음
            base.OnUpdate(deltaTime);
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // Flame은 자동 공격 루프를 사용하므로 수동 Attack은 무시됩니다.
        }

        public override void Dispose()
        {
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;
            
            base.Dispose();
        }

        #endregion

        #region 공격 로직

        private void StartAttackLoop()
        {
            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackCts.Token).Forget();
        }

        private async UniTaskVoid AttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                float delay = m_runtimeStats.CoolTime / speed;

                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);

                // 게임이 플레이 중이 아니면 스폰 생략
                if (PlayStateManager.instance != null && !PlayStateManager.instance.IsPlaying)
                {
                    continue;
                }

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

            // 직접 타격 데미지
            float directDamage = m_runtimeStats.AttackPower;
            // 지속 피해 데미지
            float dotDamage = directDamage * m_dotDamageRatio;

            // 로직 클래스 생성 (POCO)
            var logic = new FlamePillarLogic(directDamage, dotDamage, m_dotDuration, m_dotTicks, m_hitFlashColor);
            
            // 뷰 활성화 및 로직 주입
            pillar.Activate(randomPosition, logic);
        }

        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null)
            {
                if (m_ownerTransform != null) return m_ownerTransform.position;
                return Vector3.zero;
            }

            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);

            // Z=10 정도 앞 (카메라 기준)
            Vector3 viewportPos = new Vector3(randomX, randomY, 10);

            return m_mainCamera.ViewportToWorldPoint(viewportPos);
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