using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Manager;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 히어로 랜딩(방패) 공격의 시각적 연출과 비동기 제어를 담당하는 컨트롤러입니다.
    /// <br/> 충격파 생성, 부메랑 투사체 발사 등의 타이밍을 애니메이션과 동기화합니다.
    /// <br/> 실제 데미지 계산 및 물리 로직은 ShieldWeaponLogic에 수행니다.
    /// </summary>
    public class ShieldWeaponController : WeaponControllerBase
    {
        #region 1. 내부 변수 및 컴포넌트 (Components & State)

        // 비주얼 오브젝트 (방패 모델)
        private GameObject m_shieldObj;
        private Transform m_shieldTransform;
        private Animator[] m_shieldAnimators; // 하위 애니메이터 포함 캐싱

        // 프리팹 참조
        private ShieldShockwave m_shockwavePrefab; // 이펙트
        private ShieldProjectile m_boomerangPrefab; // 투사체

        // 로직 및 제어
        private ShieldWeaponLogic m_logic;
        private CancellationTokenSource m_attackCts;
        private Transform m_playerTransform;

        // 애니메이션 해시 (최적화)
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Drop_shield");

        #endregion

        #region 2. 초기화 및 해제 (Init & Dispose)

        public override void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, poolManager, getTargetDirection);

            // 1. 방패 모델 인스턴스화
            if (data.ModelPrefab != null)
            {
                m_shieldObj = UnityEngine.Object.Instantiate(data.ModelPrefab, m_ownerTransform);
                m_shieldTransform = m_shieldObj.transform;
                
                // 위치 초기화
                m_shieldTransform.localPosition = Vector3.zero;
                m_shieldTransform.localRotation = Quaternion.identity;

                // 애니메이터 캐싱 (배열로 관리하여 일괄 제어)
                m_shieldAnimators = m_shieldObj.GetComponentsInChildren<Animator>(true);
                
                // 프리팹에서 비활성화되어 있을 수 있으므로 강제 활성화
                if (m_shieldAnimators != null)
                {
                    foreach (var anim in m_shieldAnimators)
                    {
                        anim.enabled = true;
                    }
                }
                
                if (m_shieldAnimators == null || m_shieldAnimators.Length == 0)
                {
                    LogManager.LogError($"[ShieldWeaponController] {data.WeaponName} 모델에 Animator가 없습니다.", LogManager.LogCategory.Weapon);
                }
            }

            // 2. 튜닝 데이터 및 로직(POCO) 초기화 (수정: Nullable을 사용하여 데이터 부재 시 기본값 보호)
            ShieldWeaponTuningData? tuningData = null;
            ShieldWeaponView view = null;
            if (m_shieldObj != null)
            {
                view = m_shieldObj.GetComponentInChildren<ShieldWeaponView>();
            }

            // 무기 오브젝트에 없으면 PoolManager에서 시도
            if (view == null && m_poolManager != null)
            {
                view = m_poolManager.GetComponent<ShieldWeaponView>();
            }

            if (view != null)
            {
                tuningData = new ShieldWeaponTuningData
                {
                    ImpactTriggerTime = view.ImpactTriggerTime,
                    FollowThroughDelay = view.FollowThroughDelay,
                    BoomerangSpeed = view.BoomerangSpeed,
                    ReturnDelay = view.ReturnDelay,
                    RotationsPerSecond = view.RotationsPerSecond,
                    ShockwaveOffset = view.ShockwaveOffset
                };
            }
            m_logic = new ShieldWeaponLogic(m_runtimeStats, tuningData);

            // 3. 프리팹 캐싱
            if (data.EffectPrefab != null) m_shockwavePrefab = data.EffectPrefab.GetComponent<ShieldShockwave>();
            if (data.ProjectilePrefab != null) m_boomerangPrefab = data.ProjectilePrefab.GetComponent<ShieldProjectile>();

            if (GameManager.Instance != null)
            {
                m_playerTransform = GameManager.Instance.PlayerTransfrom();
            }

            // 4. 상태 초기화 및 풀 등록
            ResetShieldState();
            RegisterPools();
        }

        /// <summary>
        /// 투사체(부메랑)와 이펙트(충격파)를 위한 오브젝트 풀을 등록합니다.
        /// </summary>
        private void RegisterPools()
        {
            if (m_poolManager == null) return;

            // 부메랑 풀 (투사체 개수 기반으로 넉넉하게 설정)
            if (m_boomerangPrefab != null)
            {
                int maxBoomerangs = m_logic.BoomerangCount * 3 + 5;
                m_poolManager.GetOrAddPool<ShieldProjectile>(
                    createFunc: CreateBoomerangProjectile,
                    actionOnGet: OnGetProjectile,
                    actionOnRelease: OnReleaseProjectile,
                    actionOnDestroy: OnDestroyProjectile,
                    defaultCapacity: m_logic.BoomerangCount,
                    maxSize: maxBoomerangs
                );
            }

            // 충격파 풀
            if (m_shockwavePrefab != null)
            {
                m_poolManager.GetOrAddPool<ShieldShockwave>(
                    createFunc: CreateShockwave,
                    actionOnGet: OnGetShockwave,
                    actionOnRelease: OnReleaseShockwave,
                    actionOnDestroy: OnDestroyShockwave,
                    defaultCapacity: 2,
                    maxSize: 5
                );
            }
        }

        public override void Dispose()
        {
            CancelAttack();
            
            if (m_shieldObj != null)
            {
                UnityEngine.Object.Destroy(m_shieldObj);
                m_shieldObj = null;
            }

            base.Dispose();
        }

        private void ResetShieldState()
        {
            if (m_shieldTransform != null)
            {
                m_shieldTransform.localPosition = Vector3.zero;
                m_shieldTransform.localRotation = Quaternion.identity;
            }

            if (m_shieldAnimators != null)
            {
                foreach (var anim in m_shieldAnimators)
                {
                    if (anim != null)
                    {
                        anim.Rebind();
                        anim.speed = 1f;
                    }
                }
            }

            if (m_shieldObj != null) m_shieldObj.SetActive(false);
        }

        #endregion

        #region 3. 공격 실행 (Attack Execution)

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 이전 공격 취소 및 새 토큰 생성
            CancelAttack();
            m_attackCts = new CancellationTokenSource();
            
            // 비동기 공격 루틴 시작
            AnimateShieldAttackAsync(m_attackCts.Token).Forget();
        }

        private void CancelAttack()
        {
            if (m_attackCts != null)
            {
                m_attackCts.Cancel();
                m_attackCts.Dispose();
                m_attackCts = null;
            }
        }

        /// <summary>
        /// 방패 공격 애니메이션을 재생하고, 특정 타이밍에 충격파와 부메랑을 생성합니다.
        /// </summary>
        private async UniTaskVoid AnimateShieldAttackAsync(CancellationToken token)
        {
            try
            {
                // 1. 방패 활성화 및 애니메이션 시작
                if (m_shieldObj != null)
                {
                    m_shieldObj.SetActive(true);
                    
                    // 애니메이터 활성화 및 재생
                    if (m_shieldAnimators != null)
                    {
                        foreach (var anim in m_shieldAnimators)
                        {
                            if (anim == null || !anim.gameObject.activeSelf) continue;
                            
                            anim.speed = m_logic.AttackSpeed; // 공속 반영
                            anim.Play(k_AnimHashAttack, 0, 0f);
                            anim.Update(0f); // 첫 프레임 즉시 갱신
                        }
                    }
                }

                // 2. 임팩트 시점까지 대기 (내려찍는 동작)
                float impactTime = m_logic.GetAdjustedWaitTime(m_logic.ImpactTriggerTime);
                await UniTask.Delay(TimeSpan.FromSeconds(impactTime), cancellationToken: token);

                // 3. 효과 발생 (충격파 & 부메랑)
                SpawnShockwaveEffect();

                if (m_logic.IsEvolved)
                {
                    LaunchBoomerangs();
                }

                // 4. 후속 동작(Follow-through) 대기
                float followTime = m_logic.GetAdjustedWaitTime(m_logic.FollowThroughDelay);
                await UniTask.Delay(TimeSpan.FromSeconds(followTime), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 공격 취소됨 (정상)
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ShieldWeapon] 공격 중 오류: {ex.Message}", LogManager.LogCategory.Weapon);
            }
            finally
            {
                // 5. 종료 및 상태 리셋
                ResetShieldState();
            }
        }

        #endregion

        #region 4. 투사체 및 효과 생성 (Spawning)

        private void SpawnShockwaveEffect()
        {
            if (m_poolManager == null || m_shockwavePrefab == null) return;

            ShieldShockwave effect = m_poolManager.Get<ShieldShockwave>();
            if (effect != null)
            {
                // 위치 설정 (오프셋 반영)
                // 로컬 좌표계 오프셋을 월드 좌표로 변환하여 적용
                Vector3 offset = m_ownerTransform.TransformDirection(m_logic.ShockwaveOffset);
                effect.transform.position = m_ownerTransform.position + offset;
                effect.transform.rotation = Quaternion.identity;
                
                // 데이터 초기화
                effect.Init(
                    m_logic.AttackPower, 
                    m_logic.MobStunTime, 
                    m_logic.AttackSpeed, 
                    m_poolManager
                );
            }
        }

        private void LaunchBoomerangs()
        {
            if (m_poolManager == null || m_boomerangPrefab == null || m_playerTransform == null) return;

            Vector3 spawnPos = m_playerTransform.position;

            for (int i = 0; i < m_logic.BoomerangCount; i++)
            {
                // 로직에서 발사 정보(방향, 회전) 계산
                var (direction, rotation) = m_logic.CalculateBoomerangLaunchInfo(i);

                ShieldProjectile boomerang = m_poolManager.Get<ShieldProjectile>();
                if (boomerang != null)
                {
                    boomerang.transform.SetPositionAndRotation(spawnPos, rotation);
                    
                    // 데이터 초기화
                    boomerang.Init(
                        m_logic.AttackPower,
                        m_logic.MobStunTime,
                        m_playerTransform,
                        m_poolManager,
                        direction,
                        m_logic.BoomerangSpeed,
                        m_logic.BoomerangDistance,
                        m_logic.ReturnDelay,
                        m_logic.RotationsPerSecond
                    );
                }
            }
        }

        #endregion

        #region 5. 오브젝트 풀 델리게이트 (Pool Callbacks)

        // --- 부메랑 (Projectile) ---
        private ShieldProjectile CreateBoomerangProjectile()
        {
            if (m_boomerangPrefab == null) return null;
            return UnityEngine.Object.Instantiate(m_boomerangPrefab);
        }

        private void OnGetProjectile(ShieldProjectile p) => p.gameObject.SetActive(true);
        private void OnReleaseProjectile(ShieldProjectile p) => p.gameObject.SetActive(false);
        private void OnDestroyProjectile(ShieldProjectile p)
        {
            if (p != null) UnityEngine.Object.Destroy(p.gameObject);
        }

        // --- 충격파 (Effect) ---
        private ShieldShockwave CreateShockwave()
        {
            if (m_shockwavePrefab == null) return null;
            return UnityEngine.Object.Instantiate(m_shockwavePrefab);
        }

        private void OnGetShockwave(ShieldShockwave e) => e.gameObject.SetActive(true);
        private void OnReleaseShockwave(ShieldShockwave e) => e.gameObject.SetActive(false);
        private void OnDestroyShockwave(ShieldShockwave e)
        {
            if (e != null) UnityEngine.Object.Destroy(e.gameObject);
        }

        #endregion
    }
}