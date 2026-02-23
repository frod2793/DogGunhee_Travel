using System;
using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Core.Interfaces;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 화면 내에서 계속 튕기며 적을 공격하는 '진주(Pearl)' 무기를 제어하는 컨트롤러입니다.
    /// 투사체는 화면에 단 하나만 유지되며(Single Instance), 지속적으로 상태를 갱신합니다.
    /// </summary>
    public class JinjooWeaponController : WeaponControllerBase
    {
        #region 내부 변수 및 상태

        // 프리팹 및 뷰 데이터
        private PearlProjectile m_pearlPrefab;
        private PearlWeaponView m_view;

        // 로직 (POCO)
        private PearlWeaponLogic m_logic;

        // 런타임 상태
        private bool m_currentEvolveState;
        private PearlProjectile m_activePearl; // 현재 활성화된 유일한 진주 인스턴스

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// [설명]: 무기를 초기화하고 진주 투사체 및 전용 오브젝트 풀을 설정합니다.
        /// </summary>
        public override void Init(
            WeaponDataSO data, 
            Transform ownerTransform,
            WeaponPoolManager poolManager, 
            Func<Vector3> getTargetDirection,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            base.Init(data, ownerTransform, poolManager, getTargetDirection, gameState, combatContext, playerContext);

            // 1. 투사체 프리팹 추출 및 검증
            if (data.ProjectilePrefab != null)
            {
                m_pearlPrefab = data.ProjectilePrefab.GetComponent<PearlProjectile>();
            }

            if (m_pearlPrefab == null)
            {
                LogManager.LogError($"[JinjooWeaponController] WeaponData에서 PearlProjectile 컴포넌트를 찾을 수 없습니다: {data.WeaponName}",
                    LogManager.LogCategory.Weapon);
                return;
            }

            // 2. View 데이터 추출 (WeaponPoolManager에 부착된 컴포넌트 참조)
            InitializeView();

            // 3. 로직초기화
            PearlTuningData tuningData = new PearlTuningData
            {
                HitCooldown = m_view.HitCooldown
            };
            m_logic = new PearlWeaponLogic(m_runtimeStats);

            m_currentEvolveState = m_runtimeStats.IsEvolved;

            // 4. 오브젝트 풀 등록 (화면에 1개만 존재하도록 제한)
            RegisterPool();
        }

        /// <summary>
        /// 뷰 컴포넌트를 찾거나 없으면 생성하여 초기화합니다.
        /// </summary>
        private void InitializeView()
        {
            if (m_poolManager != null)
            {
                m_view = m_poolManager.GetComponent<PearlWeaponView>();
            }

            // 안전장치: 뷰가 없으면 동적으로 생성 (기본값 사용)
            if (m_view == null)
            {
                Debug.LogWarning("[JinjooWeaponController] View를 찾을 수 없어 기본값을 생성합니다.");
                var go = (m_poolManager != null) ? m_poolManager.gameObject : new GameObject("PearlWeaponView_Default");
                m_view = go.GetComponent<PearlWeaponView>() ?? go.AddComponent<PearlWeaponView>();
            }
        }

        /// <summary>
        /// 진주 투사체 전용 오브젝트 풀을 등록합니다.
        /// 단일 개체만 사용하므로 MaxSize를 1로 설정합니다.
        /// </summary>
        private void RegisterPool()
        {
            if (m_poolManager == null) return;

            m_poolManager.GetOrAddPool<PearlProjectile>(
                createFunc: CreatePearlProjectile,
                actionOnGet: OnGetPearlProjectile,
                actionOnRelease: OnReleasePearlProjectile,
                actionOnDestroy: OnDestroyPearlProjectile,
                defaultCapacity: 1,
                maxSize: 1
            );
        }

        public override void Dispose()
        {
            // 전역 풀 매니저를 사용하므로 여기서 개별 풀을 파괴하지 않습니다.
            m_activePearl = null;
        }

        #endregion

        #region 업데이트 루프

        /// <summary>
        /// [설명]: 매 프레임 호출되어 로직 상태를 갱신하고 활성 진주의 업데이트를 수행합니다.
        /// </summary>
        public override void OnUpdate(float deltaTime)
        {
            if (m_logic == null) return;

            // 1. 실시간 스탯 변화 감지 (진화 여부, 공속 변화 등)
            // 변화가 감지되면 로직에 반영하여 즉시 적용되도록 함
            bool isStatsChanged = m_currentEvolveState != m_runtimeStats.IsEvolved ||
                                  !Mathf.Approximately(m_logic.AttackSpeed, m_runtimeStats.AttackSpeed);

            if (isStatsChanged)
            {
                m_currentEvolveState = m_runtimeStats.IsEvolved;
                m_logic.UpdateStats(m_runtimeStats);
            }

            // 2. 활성화된 진주가 있다면 상태 갱신 (물리/이동 로직 위임)
            if (m_activePearl != null)
            {
                m_activePearl.UpdateState();
            }
        }

        #endregion

        #region 공격 실행

        /// <summary>
        /// [설명]: 공격 명령을 수행합니다. 진주는 화면에 하나만 유지되므로 중복 생성을 막습니다.
        /// </summary>
        protected override void ExecuteAttack(Vector3 direction)
        {
            // 이미 활성화된 진주가 있다면 발사하지 않음
            if (m_activePearl != null)
            {
                return;
            }

            LaunchPearl(direction);
        }

        /// <summary>
        /// 풀에서 진주를 가져와 초기화하고 물리력을 가해 발사합니다.
        /// </summary>
        private void LaunchPearl(Vector3 direction)
        {
            if (m_pearlPrefab == null || m_poolManager == null) return;

            // 방향이 없으면 랜덤 방향 설정
            if (direction == Vector3.zero)
            {
                direction = UnityEngine.Random.insideUnitCircle.normalized;
            }

            // 1. 풀에서 가져오기
            PearlProjectile pearl = m_poolManager.Get<PearlProjectile>();
            if (pearl == null) return;

            // 2. 위치 및 회전 초기화
            pearl.transform.SetPositionAndRotation(m_ownerTransform.position, Quaternion.identity);

            // 3. 초기 속도 계산
            float speed = m_logic.AttackSpeed;
            Vector3 velocity = direction.normalized * speed;

            // 4. 투사체 초기화 (데이터 주입)
            pearl.Init(m_logic, m_view, velocity, m_poolManager);
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private PearlProjectile CreatePearlProjectile()
        {
            if (m_pearlPrefab == null) return null;
            return UnityEngine.Object.Instantiate(m_pearlPrefab);
        }

        private void OnGetPearlProjectile(PearlProjectile pearl)
        {
            m_activePearl = pearl;
            pearl.gameObject.SetActive(true);
        }

        private void OnReleasePearlProjectile(PearlProjectile pearl)
        {
            // 반환되는 객체가 현재 활성 객체라면 참조 해제
            if (m_activePearl == pearl)
            {
                m_activePearl = null;
            }
            pearl.gameObject.SetActive(false);
        }

        private void OnDestroyPearlProjectile(PearlProjectile pearl)
        {
            if (pearl != null)
            {
                UnityEngine.Object.Destroy(pearl.gameObject);
            }
        }

        #endregion
    }
}