using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weaphon.Base;
using System;
using InGame.Manager;

namespace InGame.Weaphon
{
    /// <summary>
    /// 방패를 지면에 내려찍어 충격파로 공격하는 무기 클래스입니다.
    /// </summary>
    public class WeaphonShield : WeaphonBase
    {
        #region 인스펙터 필드

        [Header("<color=green> 방패 아이템 관련 변수")]
        [SerializeField] private GameObject m_shieldObj;
        
        [Header("애니메이션 설정")]
        [SerializeField] private Animator m_shieldAnimator;
        [SerializeField] private ShieldShockwave m_shockwavePrefab; 

        [Header("공격 타이밍 설정")]
        [SerializeField] private float m_impactTriggerTime = 1.07f; 

        [Header("부메랑 공격 설정")]
        [SerializeField] private GameObject m_boomerangPrefab;
        [SerializeField] private int m_boomerangCount = 5;
        [SerializeField] private float m_boomerangSpeed = 5f;
        [SerializeField] private float m_boomerangDistance = 3f;
        [SerializeField] private float m_returnDelay = 0.1f;
        [SerializeField] private float m_boomerangRotationsPerSecond = 2.5f;

        #endregion

        #region 내부 캐시 및 상태 변수

        private Transform m_shieldTransform;
        private Transform m_playerTransform;

        private bool m_canAttack = true; // 공격 가능 상태 플래그
        
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_shieldObj != null) m_shieldTransform = m_shieldObj.transform;
        }

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            
            if (GameManager.Instance != null)
                m_playerTransform = GameManager.Instance.PlayerTransfrom();

            ResetShieldState();
            SetShieldActive(false);
            m_canAttack = true; // 활성화 시 공격 가능하도록 초기화

            // WeaponPoolManager를 통해 shieldProjectile 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<shieldProjectile>(
                CreateBoomerangProjectile,
                OnGetBoomerangProjectile,
                OnReleaseBoomerangProjectile,
                OnDestroyBoomerangProjectile,
                defaultCapacity: m_boomerangCount,
                maxSize: m_boomerangCount * 3
            );

            // WeaponPoolManager를 통해 ShieldShockwave 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<ShieldShockwave>(
                CreateShockwave,
                OnGetShockwave,
                OnReleaseShockwave,
                OnDestroyShockwave,
                defaultCapacity: 2,
                maxSize: 5
            );
        }

        private new void OnDisable()
        {
            transform.DOKill();
        }
        

#if UNITY_EDITOR
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TestAttack();
            }
        }
#endif

        #endregion

        #region 무기 동작 관리

        [ContextMenu("Test Attack")]
        public void TestAttack()
        {
            if (!Application.isPlaying) return;
            Weaphon_Attack(Vector3.up);
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (!m_canAttack) return;
            AnimateShieldAttackAsync().Forget();
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

        #region 애니메이션 및 로직

        private async UniTaskVoid AnimateShieldAttackAsync()
        {
            if (!m_canAttack) return;
            m_canAttack = false;
            StartCooldownAsync().Forget();

            var token = this.GetCancellationTokenOnDestroy();



            try
            {
                if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;

                float speedMultiplier = (this.attackSpeed > 0) ? this.attackSpeed : 1.0f;

                if (m_shieldAnimator != null)
                {
                    SetShieldActive(true);
                    m_shieldAnimator.speed = speedMultiplier;
                    m_shieldAnimator.SetTrigger(k_AnimHashAttack);
                }

                float waitTime = m_impactTriggerTime / speedMultiplier;
                
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                if (isEvolved)
                {
                    LaunchBoomerangs();
                }

                SpawnShockwaveEffect();
            }
            finally
            {
                ResetShieldState();
            }
        }

        private async UniTaskVoid StartCooldownAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                float finalCoolTime = (this.attackSpeed > 0) ? this.coolTime / this.attackSpeed : this.coolTime;
                await UniTask.Delay(TimeSpan.FromSeconds(finalCoolTime), cancellationToken: token);
                m_canAttack = true;
            }
            catch (OperationCanceledException)
            {
                // 오브젝트 파괴 시 UniTask.Delay가 취소될 때 발생하는 예외입니다. 정상적인 동작이므로 아무것도 하지 않아도 됩니다.
            }
        }

        private void SpawnShockwaveEffect()
        {
            // WeaponPoolManager를 통해 충격파 효과를 가져옵니다.
            ShieldShockwave effect = WeaponPoolManager.Instance.Get<ShieldShockwave>();
            if (effect == null)
            {
                Debug.LogWarning("Failed to get ShieldShockwave from pool.");
                return;
            }
            
            effect.transform.position = transform.position; 
            effect.transform.rotation = Quaternion.identity;

            effect.Initialize(this.attackPower, this.mobStunTime, this.attackSpeed);
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

                // WeaponPoolManager를 통해 부메랑을 가져옵니다.
                var boomerang = WeaponPoolManager.Instance.Get<shieldProjectile>();
                if (boomerang == null)
                {
                    Debug.LogWarning("Failed to get shieldProjectile from pool.");
                    continue;
                }

                boomerang.transform.SetPositionAndRotation(spawnPos, rotation);
                
                boomerang.Initialize(
                    this.attackPower, 
                    this.mobStunTime, 
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

        #region Object Pooling Delegates (WeaponPoolManager에서 사용될 델리게이트)

        private shieldProjectile CreateBoomerangProjectile()
        {
            if (m_boomerangPrefab == null)
            {
                Debug.LogError("[WeaphonShield] Boomerang Prefab이 할당되지 않았습니다!");
                return null;
            }
            return Instantiate(m_boomerangPrefab).GetComponent<shieldProjectile>();
        }

        private void OnGetBoomerangProjectile(shieldProjectile p) => p.gameObject.SetActive(true);
        private void OnReleaseBoomerangProjectile(shieldProjectile p) => p.gameObject.SetActive(false);
        private void OnDestroyBoomerangProjectile(shieldProjectile p) => Destroy(p.gameObject);

        private ShieldShockwave CreateShockwave()
        {
            if (m_shockwavePrefab == null)
            {
                Debug.LogError("[WeaphonShield] Shockwave Prefab이 할당되지 않았습니다!");
                return null;
            }
            return Instantiate(m_shockwavePrefab);
        }

        private void OnGetShockwave(ShieldShockwave effect) => effect.gameObject.SetActive(true);
        private void OnReleaseShockwave(ShieldShockwave effect) => effect.gameObject.SetActive(false);
        private void OnDestroyShockwave(ShieldShockwave effect) => Destroy(effect.gameObject);

        #endregion
    }
}
