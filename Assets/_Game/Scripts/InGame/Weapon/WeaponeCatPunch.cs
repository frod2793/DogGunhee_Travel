using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Player.Player_Base;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Weapon
{
    public class WeaponeCatPunch : WeaponBase
    {
        #region 인스펙터 필드
        [Header("고양이 펀치 고유 스탯")]
        [SerializeField] private float m_initialAttackPower = 20f;
        [SerializeField] private float m_initialCoolTime = 1.5f;
        [SerializeField] private float m_initialMobStunTime = 0.3f;

        [Header("애니메이션 설정")]
        [SerializeField] private Animator m_weaponAnimator;
        [SerializeField] private SpriteRenderer m_weaponSpriteRenderer;
        [SerializeField] private float m_attackDuration = 0.4f;
        [SerializeField] private float m_rotationOffset = 0f;
        
        [Header("공격 판정 콜라이더")]
        [SerializeField] private PolygonCollider2D m_attackCollider;
        [SerializeField] private LayerMask m_targetLayer; 
        #endregion

        #region 내부 변수
        private bool m_isAttacking;
        private readonly HashSet<int> m_hitMobInstanceIDs = new HashSet<int>();
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        private ContactFilter2D m_contactFilter;
        private readonly List<Vector2> m_shapePointsBuffer = new List<Vector2>(64);
        
        private static readonly int k_AnimTriggerStab = Animator.StringToHash("Stab");
        private static readonly int k_AnimTriggerSlash = Animator.StringToHash("Slash");
        
        private PlayerControll m_playerController;
        #endregion

        #region Unity 라이프사이클
        private void Awake()
        {
            if (m_attackCollider == null) m_attackCollider = GetComponentInChildren<PolygonCollider2D>();
            if (m_weaponAnimator == null) m_weaponAnimator = GetComponentInChildren<Animator>();
            if (m_weaponSpriteRenderer == null) m_weaponSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (m_attackCollider != null) 
            {
                m_attackCollider.isTrigger = true;
                SetAttackColliderActive(false);
            }

            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;
            
            // [최적화] 콜라이더 모양을 1회만 초기화합니다.
            InitializeColliderShapeOnce();
        }

        private new void OnEnable()
        {
            SetWeaponState(WeaponState.Idle);
            attackPower = m_initialAttackPower;
            coolTime = m_initialCoolTime;
            mobStunTime = m_initialMobStunTime;
            
            if (GameManager.Instance != null)
                m_playerController = GameManager.Instance.PlayerController;

            ResetWeaponState();
        }

        private new void OnDisable()
        {
            ResetWeaponState();
        }
        #endregion

        #region 무기 동작 관리
        public override void Weapon_Attack(Vector3 attackAngle)
        {
            if (m_isAttacking) return;
            RotateWeaponToDirection(attackAngle);
            PerformAttackAsync().Forget();
        }

        private void ResetWeaponState()
        {
            m_isAttacking = false;
            m_hitMobInstanceIDs.Clear();

            SetAttackColliderActive(false);
            if (m_weaponAnimator != null) m_weaponAnimator.Rebind();
            
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
        #endregion

        #region 공격 로직

        private void RotateWeaponToDirection(Vector3 direction)
        {
            if (direction == Vector3.zero) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle + m_rotationOffset);

            if (Mathf.Abs(angle) > 90)
                transform.localScale = new Vector3(1, -1, 1);
            else
                transform.localScale = new Vector3(1, 1, 1);
        }

        private async UniTaskVoid PerformAttackAsync()
        {
            m_isAttacking = true;
            m_hitMobInstanceIDs.Clear();
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                if (m_weaponAnimator != null)
                {
                    int trigger = isEvolved ? k_AnimTriggerSlash : k_AnimTriggerStab;
                    m_weaponAnimator.speed = (attackSpeed > 0) ? attackSpeed : 1f;
                    m_weaponAnimator.SetTrigger(trigger);
                }

                SetAttackColliderActive(true);

                await WaitForAnimationAndCheckCollision(token);

                SetAttackColliderActive(false);

                float waitTime = (attackSpeed > 0) ? coolTime / attackSpeed : coolTime;
                await UniTask.Delay(System.TimeSpan.FromSeconds(waitTime), cancellationToken: token);
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        /// <summary>
        /// [최적화] 매 프레임 UpdateColliderShape 호출을 제거했습니다.
        /// 콜라이더는 프리팹에 미리 설정하거나 Awake에서 1회 초기화합니다.
        /// </summary>
        private async UniTask WaitForAnimationAndCheckCollision(System.Threading.CancellationToken token)
        {
            float timer = 0f;
            float duration = m_attackDuration;

            if (m_weaponAnimator != null)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                
                UpdateWeaponRotation(); 
                CheckCollision();

                while (m_weaponAnimator.IsInTransition(0))
                {
                    UpdateWeaponRotation();
                    CheckCollision();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                var stateInfo = m_weaponAnimator.GetCurrentAnimatorStateInfo(0);
                duration = stateInfo.length;
                if (stateInfo.speed > 0) duration /= stateInfo.speed;
            }

            while (timer < duration)
            {
                UpdateWeaponRotation(); 
                CheckCollision();
                
                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
            }
        }

        private void UpdateWeaponRotation()
        {
            if (m_playerController != null && m_playerController.MoveDirection != Vector3.zero)
            {
                RotateWeaponToDirection(m_playerController.MoveDirection);
            }
            else if (GameManager.Instance?.Joystick != null)
            {
                var joystick = GameManager.Instance.Joystick;
                Vector3 dir = new Vector3(joystick.Horizontal, joystick.Vertical, 0);
                if (dir.sqrMagnitude > 0.01f)
                {
                    RotateWeaponToDirection(dir.normalized);
                }
            }
        }

        /// <summary>
        /// [최적화] 콜라이더 모양을 1회만 초기화합니다. (Awake에서 호출)
        /// 이전에는 매 프레임 호출되어 심각한 GC 부하를 유발했습니다.
        /// </summary>
        private void InitializeColliderShapeOnce()
        {
            if (m_attackCollider == null || m_weaponSpriteRenderer == null || m_weaponSpriteRenderer.sprite == null) 
                return;

            int shapeCount = m_weaponSpriteRenderer.sprite.GetPhysicsShapeCount();
            
            if (shapeCount == 0)
            {
                // 스프라이트에 물리 모양이 없으면 기본 콜라이더 유지
                LogManager.LogWarning($"[CatPunch] 스프라이트에 물리 모양이 없습니다. 프리팹의 콜라이더를 사용합니다.", LogManager.LogCategory.Weapon);
                return;
            }

            m_attackCollider.pathCount = shapeCount;

            for (int i = 0; i < shapeCount; i++)
            {
                m_shapePointsBuffer.Clear();
                m_weaponSpriteRenderer.sprite.GetPhysicsShape(i, m_shapePointsBuffer);
                m_attackCollider.SetPath(i, m_shapePointsBuffer);
            }
            
            LogManager.Log($"[CatPunch] 콜라이더 초기화 완료 (PathCount: {shapeCount})", LogManager.LogCategory.Weapon);
        }

        private void CheckCollision()
        {
            if (m_attackCollider == null || m_attackCollider.pathCount == 0) return;

            int hitCount = m_attackCollider.Overlap(m_contactFilter, m_hitResults);

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                int id = target.gameObject.GetInstanceID();
                if (m_hitMobInstanceIDs.Contains(id)) continue;

                if (target.TryGetComponent(out MobBase mob))
                {
                    m_hitMobInstanceIDs.Add(id);
                    mob.TakeDamage(attackPower, mobStunTime);
                }
            }
        }

        private void SetAttackColliderActive(bool isActive)
        {
            if (m_attackCollider == null) return;

            if (m_attackCollider.gameObject != gameObject)
                m_attackCollider.gameObject.SetActive(isActive);
            else
                m_attackCollider.enabled = isActive;
        }

        #endregion
    }
}