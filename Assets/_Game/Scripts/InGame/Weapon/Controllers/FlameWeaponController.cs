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
    /// 화면 내 무작위 위치에 불기둥(Flame Pillar)을 소환하여 광역 지속 피해(DoT)를 입히는 무기 컨트롤러입니다.
    /// <br/> 일반적인 투사체와 달리 자체적인 비동기 루프를 통해 스폰 타이밍을 제어합니다.
    /// </summary>
    public class FlameWeaponController : WeaponControllerBase
    {
        #region 1. 내부 변수 및 컴포넌트 (State & Components)

        // 프리팹 및 풀링 관련
        private FlamePillar m_flamePillarPrefab;
        private int m_maxActivePillars;
        private int m_poolSize;
        private int m_currentActivePillars;

        // 튜닝 데이터 (View에서 주입)
        private float m_dotDamageRatio = 0.5f;
        private float m_dotDuration;
        private int m_dotTicks;
        private Color m_hitFlashColor = Color.white;

        // 시스템 객체
        private Camera m_mainCamera;
        private CancellationTokenSource m_attackCts;

        #endregion

        #region 2. 초기화 및 해제 (Init & Dispose)

        /// <summary>
        /// 무기를 초기화하고 불기둥 풀을 생성하며 공격 루프를 시작합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, poolManager, getTargetDirection);

            // 1. 프리팹 컴포넌트 캐싱
            if (data.ProjectilePrefab != null)
            {
                m_flamePillarPrefab = data.ProjectilePrefab.GetComponent<FlamePillar>();
            }

            if (m_flamePillarPrefab == null)
            {
                Debug.LogError($"[FlameWeaponController] 데이터에 FlamePillar 컴포넌트가 포함된 프리팹이 없습니다: {data.WeaponName}");
                return;
            }

            // 2. 뷰(View) 설정 및 튜닝 데이터 적용
            ApplyViewSettings(data);

            // 3. 기타 변수 초기화
            m_mainCamera = Camera.main;
            m_currentActivePillars = 0;

            // 4. 오브젝트 풀 등록
            RegisterProjectilePool();

            // 5. 자동 공격 루프 시작
            StartAttackLoop();
        }

        /// <summary>
        /// WeaponPoolManager나 데이터 시트에서 설정을 가져와 적용합니다.
        /// </summary>
        private void ApplyViewSettings(WeaponDataSO data)
        {
            // 기본값 설정
            m_maxActivePillars = data.BaseProjectileCount > 0 ? data.BaseProjectileCount : 3;
            m_poolSize = m_maxActivePillars + 5; // 여유분 확보
            m_dotDuration = data.BaseDuration > 0 ? data.BaseDuration : 2.0f;
            
            // View 컴포넌트가 있다면 오버라이드
            if (m_poolManager != null)
            {
                var view = m_poolManager.GetComponent<FlameWeaponView>();
                if (view != null)
                {
                    m_dotDamageRatio = view.DotDamageRatio;
                    m_hitFlashColor = view.HitFlashColor;
                    m_maxActivePillars = view.MaxActivePillars;
                    m_poolSize = view.PoolSize;
                }
            }

            // 틱 횟수 계산 (최소 1회 보장)
            m_dotTicks = Mathf.Max(1, Mathf.RoundToInt(m_dotDuration));
        }

        /// <summary>
        /// 불기둥 전용 오브젝트 풀을 등록합니다.
        /// </summary>
        private void RegisterProjectilePool()
        {
            if (m_poolManager == null) return;

            m_poolManager.GetOrAddPool(
                createFunc: CreateFlamePillar,
                actionOnGet: OnGetFlamePillar,
                actionOnRelease: OnReleaseFlamePillar,
                actionOnDestroy: OnDestroyFlamePillar,
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

        /// <summary>
        /// 기존 루프를 정리하고 새로운 비동기 공격 루프를 시작합니다.
        /// </summary>
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
        /// 쿨타임마다 조건(적 존재, 최대 개수)을 확인하여 불기둥을 소환하는 메인 루프입니다.
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

                    // 2. 상태 체크 (게임 중지, 플레이어 사망 등)
                    if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
                    {
                        continue;
                    }

                    // 3. 발동 조건 체크
                    if (!IsEnemyPresent || m_currentActivePillars >= m_maxActivePillars)
                    {
                        continue;
                    }

                    // 4. 소환 실행
                    SpawnFlamePillar();
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 루프 종료
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FlameWeapon] 공격 루프 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 카메라 뷰포트 내 랜덤 위치에 불기둥을 소환하고 초기화합니다.
        /// </summary>
        private void SpawnFlamePillar()
        {
            if (m_poolManager == null) return;

            // 1. 풀에서 가져오기
            FlamePillar pillar = m_poolManager.Get<FlamePillar>();
            if (pillar == null) return;

            // 2. 위치 설정 (Z축 보정 포함)
            Vector3 spawnPos = GetRandomPositionInView();
            spawnPos.z = 0f; // 2D 게임 기준 Z축 정렬
            pillar.transform.position = spawnPos;

            // 3. 데미지 로직 계산
            float directDamage = m_runtimeStats.AttackPower;
            float dotDamage = directDamage * m_dotDamageRatio;

            // 4. 로직 주입 및 초기화
            // FlamePillarLogic: 순수 데이터 연산 클래스 (POCO)
            var logic = new FlamePillarLogic(directDamage, dotDamage, m_dotDuration, m_dotTicks, m_hitFlashColor);
            
            pillar.Init(spawnPos, logic, m_poolManager);
        }

        

        /// <summary>
        /// 카메라 뷰포트(0.1 ~ 0.9) 내의 랜덤한 월드 좌표를 반환합니다.
        /// </summary>
        private Vector3 GetRandomPositionInView()
        {
            if (m_mainCamera == null)
            {
                // 카메라가 없으면 플레이어 주변 랜덤 위치 반환
                return m_ownerTransform != null 
                    ? m_ownerTransform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 5f 
                    : Vector3.zero;
            }

            // 화면 가장자리를 제외한 내부 영역(0.1 ~ 0.9)에서 랜덤 좌표 생성
            float randomX = UnityEngine.Random.Range(0.1f, 0.9f);
            float randomY = UnityEngine.Random.Range(0.1f, 0.9f);
            
            // 카메라 기준 Z거리 (2D Orthographic인 경우 nearClipPlane 활용 가능)
            float camDistance = 10f; 
            Vector3 viewportPos = new Vector3(randomX, randomY, camDistance);

            return m_mainCamera.ViewportToWorldPoint(viewportPos);
        }

        #endregion

        #region 4. 상속 구현 (Override Methods)

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 이 무기는 AttackLoopAsync에서 자동으로 실행되므로
            // 외부(PlayerController)에서의 강제 공격 호출은 무시하거나 별도 로직으로 처리합니다.
        }

        #endregion

        #region 5. 오브젝트 풀 델리게이트 (Pool Callbacks)

        private FlamePillar CreateFlamePillar()
        {
            if (m_flamePillarPrefab == null) return null;
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
            m_currentActivePillars = Mathf.Max(0, m_currentActivePillars - 1);
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