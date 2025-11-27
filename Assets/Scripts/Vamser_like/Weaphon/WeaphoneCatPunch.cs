using UnityEngine;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    public class WeaphoneCatPunch : WeaphonBase
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
                m_attackCollider.enabled = false;
                m_attackCollider.isTrigger = true;
            }

            m_contactFilter.NoFilter();
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;
        }

        private void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            attackPower = m_initialAttackPower;
            coolTime = m_initialCoolTime;
            mobStunTime = m_initialMobStunTime;
            
            if (GameManager.Instance != null)
                m_playerController = GameManager.Instance.PlayerController;

            ResetWeaponState();
        }

        private void OnDisable()
        {
            ResetWeaponState();
        }
        #endregion

        #region 무기 동작 관리
        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (m_isAttacking) return;
            RotateWeaponToDirection(attackAngle);
            PerformAttackAsync().Forget();
        }

        private void ResetWeaponState()
        {
            m_isAttacking = false;
            m_hitMobInstanceIDs.Clear();

            if (m_attackCollider != null) m_attackCollider.enabled = false;
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

                if (m_attackCollider != null) m_attackCollider.enabled = true;

                await WaitForAnimationAndCheckCollision(token);

                if (m_attackCollider != null) m_attackCollider.enabled = false;

                float waitTime = (attackSpeed > 0) ? coolTime / attackSpeed : coolTime;
                await UniTask.Delay(System.TimeSpan.FromSeconds(waitTime), cancellationToken: token);
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        private async UniTask WaitForAnimationAndCheckCollision(System.Threading.CancellationToken token)
        {
            float timer = 0f;
            float duration = m_attackDuration;

            if (m_weaponAnimator != null)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                
                UpdateWeaponRotation(); 
                UpdateColliderShape(); 
                CheckCollision();

                while (m_weaponAnimator.IsInTransition(0))
                {
                    UpdateWeaponRotation();
                    UpdateColliderShape();
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
                UpdateColliderShape();
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

        private void UpdateColliderShape()
        {
            if (m_attackCollider == null || m_weaponSpriteRenderer == null || m_weaponSpriteRenderer.sprite == null) 
                return;

            int shapeCount = m_weaponSpriteRenderer.sprite.GetPhysicsShapeCount();
            
            if (shapeCount == 0)
            {
                m_attackCollider.pathCount = 0;
                return;
            }

            m_attackCollider.pathCount = shapeCount;

            for (int i = 0; i < shapeCount; i++)
            {
                m_shapePointsBuffer.Clear();
                m_weaponSpriteRenderer.sprite.GetPhysicsShape(i, m_shapePointsBuffer);
                m_attackCollider.SetPath(i, m_shapePointsBuffer);
            }
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

        #endregion
    }
}