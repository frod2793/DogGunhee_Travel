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
        #region 설정 데이터

        private GameObject m_shieldObj;
        private Animator m_shieldAnimator;
        private ShieldShockwave m_shockwavePrefab;
        private GameObject m_boomerangPrefab;

        #endregion

        #region 내부 상태

        private ShieldWeaponLogic m_logic;
        private Transform m_shieldTransform;
        private Transform m_playerTransform;
        private CancellationTokenSource m_attackCts;

        // 애니메이션 해시 (Animator Normalize)
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region 초기화

        public override void Init(
            WeaponDataSO data,
            Transform owner,
            Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);

            // 1. 비주얼 생성 및 설정 컴포넌트 추출
            ShieldWeaponTuningData? tuningData = null;
            if (data.ModelPrefab != null)
            {
                m_shieldObj = UnityEngine.Object.Instantiate(data.ModelPrefab, m_ownerTransform);
                m_shieldTransform = m_shieldObj.transform;
                m_shieldAnimator = m_shieldObj.GetComponentInChildren<Animator>();
                
                // 중앙 제어: WeaponPoolManager 오브젝트에서 설정 컴포넌트 추출
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

            // 2. 로직 클래스 생성 (매핑된 설정 전달)
            m_logic = new ShieldWeaponLogic(m_runtimeStats, tuningData);

            // 3. 프리팹 캐싱
            if (data.EffectPrefab != null)
                m_shockwavePrefab = data.EffectPrefab.GetComponent<ShieldShockwave>();
            if (data.ProjectilePrefab != null)
                m_boomerangPrefab = data.ProjectilePrefab;

            if (GameManager.Instance != null)
                m_playerTransform = GameManager.Instance.PlayerTransfrom();

            ResetShieldState();
            RegisterPools();
        }

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

        #region IWeaponController 구현

        protected override void ExecuteAttack(Vector3 direction)
        {
            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            AnimateShieldAttackAsync(m_attackCts.Token).Forget();
        }

        public override void Dispose()
        {
            m_ownerTransform.DOKill();
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;
            ResetShieldState();
        }

        #endregion

        #region 애니메이션 및 로직 레이어

        private async UniTaskVoid AnimateShieldAttackAsync(CancellationToken token)
        {
            try
            {
                if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;

                // 애니메이터 신뢰성 강화 로직
                // 프리팹 내부의 자식의 자식 등 깊은 계층에 있는 애니메이터를 모두 활성화하고 재생합니다.
                if (m_shieldObj != null)
                {
                    SetShieldActive(true);
                    
                    var animators = m_shieldObj.GetComponentsInChildren<Animator>(true);
                    foreach (var anim in animators)
                    {
                        if (anim == null) continue;
                        
                        // 자식 오브젝트 자체가 비활성 상태일 수 있으므로 명시적으로 활성 및 컴포넌트 활성화
                        anim.gameObject.SetActive(true);
                        anim.enabled = true;
                    }

                    // 활성화 직후 상태 동기화를 위해 한 프레임 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    
                    foreach (var anim in animators)
                    {
                        if (anim == null || anim.runtimeAnimatorController == null) continue;

                        anim.speed = m_logic.AttackSpeed;
                        
                        // 신뢰성 극대화: Play(상태 강제 재생)와 SetTrigger(트리거 파라미터)를 동시에 호출
                        // 1. Play: "Attack"이라는 이름의 '상태'가 있으면 즉시 해당 프레임으로 이동
                        anim.Play(k_AnimHashAttack, -1, 0f);
                        
                        // 2. SetTrigger: "Attack"이라는 이름의 '트리거 파라미터'가 있으면 발동 (전이 조건 충족용)
                        anim.SetTrigger(k_AnimHashAttack);
                        
                        // 3. Update(0): 상태 변경사항을 즉시 반영하여 다음 프레임까지 기다리지 않음
                        anim.Update(0f);
                    }
                }

                // 임팩트 시점까지 대기
                float waitTime = m_logic.GetAdjustedWaitTime(m_logic.ImpactTriggerTime);
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                // 효과 발생
                if (m_logic.IsEvolved)
                {
                    LaunchBoomerangs();
                }
                SpawnShockwaveEffect();

                // 후속 동작(Follow-through) 대기
                float followDelay = m_logic.GetAdjustedWaitTime(m_logic.FollowThroughDelay);
                await UniTask.Delay(TimeSpan.FromSeconds(followDelay), cancellationToken: token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                ResetShieldState();
            }
        }

        private void SpawnShockwaveEffect()
        {
            ShieldShockwave effect = WeaponPoolManager.Instance.Get<ShieldShockwave>();
            if (effect != null)
            {
                // ShieldShockwave 컴포넌트에 설정된 에디터 보정값 적용
                effect.transform.position = m_ownerTransform.position + effect.SpawnOffset;
                effect.transform.rotation = Quaternion.identity;
                effect.Initialize(m_logic.AttackPower, m_logic.MobStunTime, m_logic.AttackSpeed);
            }
        }

        private void LaunchBoomerangs()
        {
            if (m_boomerangPrefab == null || m_playerTransform == null) return;

            Vector3 spawnPos = m_playerTransform.position;

            for (int i = 0; i < m_logic.BoomerangCount; i++)
            {
                var (direction, rotation) = m_logic.CalculateBoomerangLaunchInfo(i);

                var boomerang = WeaponPoolManager.Instance.Get<ShieldProjectile>();
                if (boomerang != null)
                {
                    boomerang.transform.SetPositionAndRotation(spawnPos, rotation);
                    boomerang.Initialize(
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
                m_shieldObj.SetActive(isActive);
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private ShieldProjectile CreateBoomerangProjectile()
        {
            if (m_boomerangPrefab == null) return null;
            return UnityEngine.Object.Instantiate(m_boomerangPrefab).GetComponent<ShieldProjectile>();
        }

        private void OnGetBoomerangProjectile(ShieldProjectile p) => p.gameObject.SetActive(true);
        private void OnReleaseBoomerangProjectile(ShieldProjectile p) => p.gameObject.SetActive(false);
        private void OnDestroyBoomerangProjectile(ShieldProjectile p)
        {
            if (p != null) UnityEngine.Object.Destroy(p.gameObject);
        }

        private ShieldShockwave CreateShockwave()
        {
            if (m_shockwavePrefab == null) return null;
            return UnityEngine.Object.Instantiate(m_shockwavePrefab);
        }

        private void OnGetShockwave(ShieldShockwave effect) => effect.gameObject.SetActive(true);
        private void OnReleaseShockwave(ShieldShockwave effect) => effect.gameObject.SetActive(false);
        private void OnDestroyShockwave(ShieldShockwave effect)
        {
            if (effect != null) UnityEngine.Object.Destroy(effect.gameObject);
        }

        #endregion
    }
}
