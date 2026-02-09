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
    /// <summary>
    /// 지정된 범위 내에 불기둥(FlamePillar)을 소환하여 지속 피해를 입히는 무기 컨트롤러입니다.
    /// </summary>
    public class FlameWeaponController : WeaponControllerBase
    {
        #region 내부 상태 및 변수

        private FlamePillar m_flamePillarPrefab;
        private int m_maxActivePillars;
        private int m_poolSize;

        // 튜닝 데이터
        private float m_dotDamageRatio = 0.5f;
        private float m_dotDuration;
        private int m_dotTicks;
        private Color m_hitFlashColor = Color.white;

        private Camera m_mainCamera;
        private int m_currentActivePillars;
        private CancellationTokenSource m_attackCts;

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 불기둥 풀 및 자동 공격 루프를 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform owner, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);

            // 1. 프리팹 및 컴포넌트 매핑
            if (data.ProjectilePrefab != null)
            {
                m_flamePillarPrefab = data.ProjectilePrefab.GetComponent<FlamePillar>();
            }

            if (m_flamePillarPrefab == null)
            {
                LogManager.LogError($"[FlameWeaponController] FlamePillar 컴포넌트 누락: {data.WeaponName}");
            }

            // 2. 튜닝 데이터 추출 (FlameWeaponView)
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

            // 3. 오브젝트 풀 등록
            RegisterPool();

            // 4. 공격 루프 시작
            StartAttackLoop();
        }

        /// <summary>
        /// 오브젝트 풀을 설정합니다.
        /// </summary>
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

        /// <summary>
        /// 무기 해제 시 비동기 루프를 중단합니다.
        /// </summary>
        public override void Dispose()
        {
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;

            base.Dispose();
        }

        #endregion

        #region 업데이트 및 실행 인터페이스

        public override void OnUpdate(float deltaTime)
        {
            // Flame은 자체 AttackLoop를 사용하므로 부모의 쿨타임 Timer만 업데이트
            base.OnUpdate(deltaTime);
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 루프 방식이므로 수동 실행은 무시
        }

        #endregion

        #region 공격 로직 및 비동기 루프

        /// <summary>
        /// 자동 공격 비동기 루틴을 시작합니다.
        /// </summary>
        private void StartAttackLoop()
        {
            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            AttackLoopAsync(m_attackCts.Token).Forget();
        }

        /// <summary>
        /// 쿨타임에 맞춰 불기둥을 스폰하는 메인 루프입니다.
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

                if (m_currentActivePillars >= m_maxActivePillars)
                {
                    continue;
                }

                SpawnFlamePillar();
            }
        }

        /// <summary>
        /// 화면 내 랜덤한 위치에 불기둥을 소환합니다.
        /// </summary>
        private void SpawnFlamePillar()
        {
            Vector3 randomPosition = GetRandomPositionInView();

            FlamePillar pillar = WeaponPoolManager.Instance.Get<FlamePillar>();
            if (pillar == null)
            {
                return;
            }

            float directDamage = m_runtimeStats.AttackPower;
            float dotDamage = directDamage * m_dotDamageRatio;

            // 로직 객체 생성 및 주입
            var logic = new FlamePillarLogic(directDamage, dotDamage, m_dotDuration, m_dotTicks, m_hitFlashColor);
            pillar.Init(randomPosition, logic);
        }

        /// <summary>
        /// 카메라 뷰포트 내의 랜덤한 월드 좌표를 가져옵니다.
        /// </summary>
        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null)
            {
                return m_ownerTransform != null ? m_ownerTransform.position : Vector3.zero;
            }

            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            Vector3 viewportPos = new Vector3(randomX, randomY, 10f);

            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #endregion

        #region 오브젝트 풀 관리 델리게이트

        private FlamePillar CreateFlamePillar()
        {
            if (m_flamePillarPrefab == null)
            {
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