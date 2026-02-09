using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.ObjectPool;
using InGame.Manager;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 히어로 랜딩(방패) 공격의 View와 애니메이션 제어를 담당하는 컨트롤러입니다.
    /// 실제 비즈니스 로직은 ShieldWeaponLogic에서 처리합니다.
    /// </summary>
    public class ShieldWeaponController : WeaponControllerBase
    {
        #region 내부 상태 및 변수

        private GameObject m_shieldObj;
        private Animator m_shieldAnimator;
        private ShieldShockwave m_shockwavePrefab;
        private GameObject m_boomerangPrefab;

        private ShieldWeaponLogic m_logic;
        private Transform m_shieldTransform;
        private Transform m_playerTransform;
        private CancellationTokenSource m_attackCts;

        // 애니메이션 해시 (Animator Normalize)
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 방패 모델 및 관련 효과/투사체 풀을 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform owner, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);

            // 1. 비주얼 생성 및 설정 컴포넌트 추출
            ShieldWeaponTuningData? tuningData = null;
            if (data.ModelPrefab != null)
            {
                m_shieldObj = UnityEngine.Object.Instantiate(data.ModelPrefab, m_ownerTransform);
                m_shieldTransform = m_shieldObj.transform;
                m_shieldAnimator = m_shieldObj.GetComponentInChildren<Animator>();
                
                // 설정 보정값 추출 (WeaponPoolManager 자식 뷰 참조)
                ShieldWeaponView viewSettings = null;
                if (WeaponPoolManager.Instance != null)
                {
                    viewSettings = WeaponPoolManager.Instance.GetComponent<ShieldWeaponView>();
                }

                if (viewSettings != null)
                {
                    tuningData = new ShieldWeaponTuningData
                    {
                        ImpactTriggerTime = viewSettings.ImpactTriggerTime,
                        FollowThroughDelay = viewSettings.FollowThroughDelay,
                        BoomerangSpeed = viewSettings.BoomerangSpeed,
                        ReturnDelay = viewSettings.ReturnDelay,
                        RotationsPerSecond = viewSettings.RotationsPerSecond
                    };
                }
                
                if (m_shieldAnimator == null)
                {
                    LogManager.LogError($"[ShieldWeaponController] {data.WeaponName} 프리팹 자식에 Animator가 없습니다.");
                }
                
                m_shieldTransform.localPosition = Vector3.zero;
                m_shieldTransform.localRotation = Quaternion.identity;
            }

            // 2. POCO Logic 생성
            m_logic = new ShieldWeaponLogic(m_runtimeStats, tuningData);

            // 3. 프리팹 캐싱
            if (data.EffectPrefab != null)
            {
                m_shockwavePrefab = data.EffectPrefab.GetComponent<ShieldShockwave>();
            }

            if (data.ProjectilePrefab != null)
            {
                m_boomerangPrefab = data.ProjectilePrefab;
            }

            if (GameManager.Instance != null)
            {
                m_playerTransform = GameManager.Instance.PlayerTransfrom();
            }

            ResetShieldState();
            RegisterPools();
        }

        /// <summary>
        /// 부메랑 및 충격파 효과를 위한 오브젝트 풀을 등록합니다.
        /// </summary>
        private void RegisterPools()
        {
            if (m_boomerangPrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<ShieldProjectile>(
                    CreateBoomerangProjectile,
                    OnGetBoomerangProjectile,
                    OnReleaseBoomerangProjectile,
                    OnDestroyBoomerangProjectile,
                    defaultCapacity: m_logic.BoomerangCount,
                    maxSize: m_logic.BoomerangCount * 3
                );
            }

            if (m_shockwavePrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<ShieldShockwave>(
                    CreateShockwave,
                    OnGetShockwave,
                    OnReleaseShockwave,
                    OnDestroyShockwave,
                    defaultCapacity: 2,
                    maxSize: 5
                );
            }
        }

        /// <summary>
        /// 무기 해제 시 활성화된 트윈 및 비동기 작업을 정리합니다.
        /// </summary>
        public override void Dispose()
        {
            m_ownerTransform.DOKill();
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;
            ResetShieldState();
        }

        /// <summary>
        /// 방패 모델과 애니메이터 상태를 초기화합니다.
        /// </summary>
        private void ResetShieldState()
        {
            if (m_shieldTransform != null) 
            {
                m_shieldTransform.localPosition = Vector3.zero;
                m_shieldTransform.localRotation = Quaternion.identity;
            }

            if (m_shieldAnimator != null)
            {
                m_shieldAnimator.Rebind();
                m_shieldAnimator.speed = 1f;
            }

            SetShieldActive(false);
        }

        #endregion

        #region 공격 실행 및 비동기 루틴

        protected override void ExecuteAttack(Vector3 direction)
        {
            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            AnimateShieldAttackAsync(m_attackCts.Token).Forget();
        }

        /// <summary>
        /// 방패 공격 애니메이션 재생 및 임팩트 시점의 효과/투사체 생성을 제어합니다.
        /// </summary>
        private async UniTaskVoid AnimateShieldAttackAsync(CancellationToken token)
        {
            try
            {
                if (m_shieldTransform != null)
                {
                    m_shieldTransform.localPosition = Vector3.zero;
                }

                if (m_shieldObj != null)
                {
                    SetShieldActive(true);
                    
                    var animators = m_shieldObj.GetComponentsInChildren<Animator>(true);
                    foreach (var anim in animators)
                    {
                        if (anim == null)
                        {
                            continue;
                        }
                        
                        anim.gameObject.SetActive(true);
                        anim.enabled = true;
                    }

                    // 활성화 직후 상태 갱신을 위해 한 프레임 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    
                    foreach (var anim in animators)
                    {
                        if (anim == null || anim.runtimeAnimatorController == null)
                        {
                            continue;
                        }

                        anim.speed = m_logic.AttackSpeed;
                        
                        // 애니메이션 즉시 재생 강제 (Play + Trigger + Update)
                        anim.Play(k_AnimHashAttack, -1, 0f);
                        anim.SetTrigger(k_AnimHashAttack);
                        anim.Update(0f);
                    }
                }

                // 1단계: 임팩트(충격파) 시점까지 대기
                float waitTime = m_logic.GetAdjustedWaitTime(m_logic.ImpactTriggerTime);
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                // 진화 시 부메랑 발사 및 공통 충격파 생성
                if (m_logic.IsEvolved)
                {
                    LaunchBoomerangs();
                }

                SpawnShockwaveEffect();

                // 2단계: 후속 동작(Follow-through) 대기
                float followDelay = m_logic.GetAdjustedWaitTime(m_logic.FollowThroughDelay);
                await UniTask.Delay(TimeSpan.FromSeconds(followDelay), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 정상 취소
            }
            finally
            {
                ResetShieldState();
            }
        }

        /// <summary>
        /// 충격파 이펙트를 생성(오브젝트 풀 활용)하고 초기화합니다.
        /// </summary>
        private void SpawnShockwaveEffect()
        {
            ShieldShockwave effect = WeaponPoolManager.Instance.Get<ShieldShockwave>();
            if (effect != null)
            {
                effect.transform.position = m_ownerTransform.position + effect.SpawnOffset;
                effect.transform.rotation = Quaternion.identity;
                effect.Init(m_logic.AttackPower, m_logic.MobStunTime, m_logic.AttackSpeed);
            }
        }

        /// <summary>
        /// 방패 파편(부메랑)들을 계산된 궤적으로 발사합니다.
        /// </summary>
        private void LaunchBoomerangs()
        {
            if (m_boomerangPrefab == null || m_playerTransform == null)
            {
                return;
            }

            Vector3 spawnPos = m_playerTransform.position;

            for (int i = 0; i < m_logic.BoomerangCount; i++)
            {
                var (direction, rotation) = m_logic.CalculateBoomerangLaunchInfo(i);

                var boomerang = WeaponPoolManager.Instance.Get<ShieldProjectile>();
                if (boomerang != null)
                {
                    boomerang.transform.SetPositionAndRotation(spawnPos, rotation);
                    boomerang.Init(
                        m_logic.AttackPower,
                        m_logic.MobStunTime,
                        m_playerTransform,
                        direction,
                        m_logic.BoomerangSpeed,
                        m_logic.BoomerangDistance,
                        m_logic.ReturnDelay,
                        m_logic.RotationsPerSecond
                    );
                }
            }
        }

        private void SetShieldActive(bool isActive)
        {
            if (m_shieldObj != null)
            {
                m_shieldObj.SetActive(isActive);
            }
        }

        #endregion

        #region 오브젝트 풀 관리 델리게이트

        private ShieldProjectile CreateBoomerangProjectile()
        {
            if (m_boomerangPrefab == null)
            {
                return null;
            }
            return UnityEngine.Object.Instantiate(m_boomerangPrefab).GetComponent<ShieldProjectile>();
        }

        private void OnGetBoomerangProjectile(ShieldProjectile p)
        {
            p.gameObject.SetActive(true);
        }

        private void OnReleaseBoomerangProjectile(ShieldProjectile p)
        {
            p.gameObject.SetActive(false);
        }

        private void OnDestroyBoomerangProjectile(ShieldProjectile p)
        {
            if (p != null)
            {
                UnityEngine.Object.Destroy(p.gameObject);
            }
        }

        private ShieldShockwave CreateShockwave()
        {
            if (m_shockwavePrefab == null)
            {
                return null;
            }
            return UnityEngine.Object.Instantiate(m_shockwavePrefab);
        }

        private void OnGetShockwave(ShieldShockwave effect)
        {
            effect.gameObject.SetActive(true);
        }

        private void OnReleaseShockwave(ShieldShockwave effect)
        {
            effect.gameObject.SetActive(false);
        }

        private void OnDestroyShockwave(ShieldShockwave effect)
        {
            if (effect != null)
            {
                UnityEngine.Object.Destroy(effect.gameObject);
            }
        }

        #endregion
    }
}
