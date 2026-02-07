using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.ObjectPool;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 방패 충격파 공격 및 진화 시 부메랑 공격을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class ShieldWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private GameObject m_shieldObj;
        private Animator m_shieldAnimator;
        private ShieldShockwave m_shockwavePrefab;
        private GameObject m_boomerangPrefab;

        private float m_impactTriggerTime;
        private int m_boomerangCount;
        private float m_boomerangSpeed;
        private float m_boomerangDistance;
        private float m_returnDelay;
        private float m_boomerangRotationsPerSecond;

        #endregion

        #region 내부 상태

        private Transform m_shieldTransform;
        private Transform m_playerTransform;
        private bool m_canAttack = true;

        private CancellationTokenSource m_attackCts;

        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region 초기화

        /// <summary>
        /// ShieldWeaponController를 초기화합니다.
        /// </summary>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            GameObject shieldObj,
            Animator shieldAnimator,
            ShieldShockwave shockwavePrefab,
            GameObject boomerangPrefab,
            float impactTriggerTime = 1.07f,
            int boomerangCount = 5,
            float boomerangSpeed = 5f,
            float boomerangDistance = 3f,
            float returnDelay = 0.1f,
            float boomerangRotationsPerSecond = 2.5f)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_shieldObj = shieldObj;
            m_shieldAnimator = shieldAnimator;
            m_shockwavePrefab = shockwavePrefab;
            m_boomerangPrefab = boomerangPrefab;

            m_impactTriggerTime = impactTriggerTime;
            m_boomerangCount = boomerangCount;
            m_boomerangSpeed = boomerangSpeed;
            m_boomerangDistance = boomerangDistance;
            m_returnDelay = returnDelay;
            m_boomerangRotationsPerSecond = boomerangRotationsPerSecond;

            if (m_shieldObj != null) m_shieldTransform = m_shieldObj.transform;

            if (GameManager.Instance != null)
                m_playerTransform = GameManager.Instance.PlayerTransfrom();

            ResetShieldState();
            SetShieldActive(false);
            m_canAttack = true;

            // 풀 등록
            RegisterPools();
        }

        private void RegisterPools()
        {
            WeaponPoolManager.Instance.GetOrAddPool<shieldProjectile>(
                CreateBoomerangProjectile,
                OnGetBoomerangProjectile,
                OnReleaseBoomerangProjectile,
                OnDestroyBoomerangProjectile,
                defaultCapacity: m_boomerangCount,
                maxSize: m_boomerangCount * 3
            );

            WeaponPoolManager.Instance.GetOrAddPool<ShieldShockwave>(
                CreateShockwave,
                OnGetShockwave,
                OnReleaseShockwave,
                OnDestroyShockwave,
                defaultCapacity: 2,
                maxSize: 5
            );
        }

        private void ResetShieldState()
        {
            if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;

            if (m_shieldAnimator != null)
            {
                m_shieldAnimator.Rebind();
                m_shieldAnimator.speed = 1f;
            }
            SetShieldActive(false);
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // Shield는 Attack 호출에 의존하므로 별도 Update 로직 불필요
        }

        public override void Attack(Vector3 direction)
        {
            if (!m_canAttack) return;

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

        #region 애니메이션 및 로직

        private async UniTaskVoid AnimateShieldAttackAsync(CancellationToken token)
        {
            if (!m_canAttack) return;
            m_canAttack = false;
            StartCooldownAsync(token).Forget();

            try
            {
                if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;

                float speedMultiplier = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1.0f;

                if (m_shieldAnimator != null)
                {
                    SetShieldActive(true);
                    m_shieldAnimator.speed = speedMultiplier;
                    m_shieldAnimator.SetTrigger(k_AnimHashAttack);
                }

                float waitTime = m_impactTriggerTime / speedMultiplier;

                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                if (m_runtimeStats.IsEvolved)
                {
                    LaunchBoomerangs();
                }

                SpawnShockwaveEffect();
            }
            catch (OperationCanceledException)
            {
                // 취소됨
            }
            finally
            {
                ResetShieldState();
            }
        }

        private async UniTaskVoid StartCooldownAsync(CancellationToken token)
        {
            try
            {
                float finalCoolTime = m_runtimeStats.AttackSpeed > 0
                    ? m_runtimeStats.CoolTime / m_runtimeStats.AttackSpeed
                    : m_runtimeStats.CoolTime;
                await UniTask.Delay(TimeSpan.FromSeconds(finalCoolTime), cancellationToken: token);
                m_canAttack = true;
            }
            catch (OperationCanceledException)
            {
                // 취소됨
            }
        }

        private void SpawnShockwaveEffect()
        {
            ShieldShockwave effect = WeaponPoolManager.Instance.Get<ShieldShockwave>();
            if (effect == null)
            {
                LogManager.LogWarning("ShieldWeaponController: 풀에서 ShieldShockwave를 가져오지 못했습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            effect.transform.position = m_ownerTransform.position;
            effect.transform.rotation = Quaternion.identity;

            effect.Initialize(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime, m_runtimeStats.AttackSpeed);
        }

        private void LaunchBoomerangs()
        {
            if (m_boomerangPrefab == null || m_playerTransform == null) return;

            float angleStep = 360f / m_boomerangCount;
            Vector3 spawnPos = m_playerTransform.position;

            for (int i = 0; i < m_boomerangCount; i++)
            {
                float currentAngle = i * angleStep;
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);
                Vector3 direction = rotation * Vector3.up;

                var boomerang = WeaponPoolManager.Instance.Get<shieldProjectile>();
                if (boomerang == null)
                {
                    LogManager.LogWarning("ShieldWeaponController: 풀에서 shieldProjectile을 가져오지 못했습니다.", LogManager.LogCategory.Weapon);
                    continue;
                }

                boomerang.transform.SetPositionAndRotation(spawnPos, rotation);

                boomerang.Initialize(
                    m_runtimeStats.AttackPower,
                    m_runtimeStats.MobStunTime,
                    m_playerTransform,
                    direction,
                    m_boomerangSpeed,
                    m_boomerangDistance,
                    m_returnDelay,
                    m_boomerangRotationsPerSecond
                );
            }
        }

        private void SetShieldActive(bool isActive)
        {
            if (m_shieldAnimator != null)
                m_shieldAnimator.gameObject.SetActive(isActive);
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private shieldProjectile CreateBoomerangProjectile()
        {
            if (m_boomerangPrefab == null)
            {
                LogManager.LogError("ShieldWeaponController: Boomerang 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }
            return UnityEngine.Object.Instantiate(m_boomerangPrefab).GetComponent<shieldProjectile>();
        }

        private void OnGetBoomerangProjectile(shieldProjectile p) => p.gameObject.SetActive(true);
        private void OnReleaseBoomerangProjectile(shieldProjectile p) => p.gameObject.SetActive(false);
        private void OnDestroyBoomerangProjectile(shieldProjectile p)
        {
            if (p != null) UnityEngine.Object.Destroy(p.gameObject);
        }

        private ShieldShockwave CreateShockwave()
        {
            if (m_shockwavePrefab == null)
            {
                LogManager.LogError("ShieldWeaponController: Shockwave 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }
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
