using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.Player.Player_Base;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 고양이 펀치 근접 공격을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class CatPunchWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private Animator m_weaponAnimator;
        private SpriteRenderer m_weaponSpriteRenderer;
        private PolygonCollider2D m_attackCollider;
        private float m_attackDuration;
        private float m_rotationOffset;
        private LayerMask m_targetLayer;

        #endregion

        #region 내부 상태

        private bool m_isAttacking;
        private readonly HashSet<int> m_hitMobInstanceIDs = new HashSet<int>();
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        private ContactFilter2D m_contactFilter;
        private readonly List<Vector2> m_shapePointsBuffer = new List<Vector2>(64);

        private static readonly int k_AnimTriggerStab = Animator.StringToHash("Stab");
        private static readonly int k_AnimTriggerSlash = Animator.StringToHash("Slash");

        private CancellationTokenSource m_attackCts;

        #endregion

        #region 초기화

        /// <summary>
        /// CatPunchWeaponController를 초기화합니다.
        /// </summary>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            Animator weaponAnimator,
            SpriteRenderer weaponSpriteRenderer,
            PolygonCollider2D attackCollider,
            float attackDuration,
            float rotationOffset,
            LayerMask targetLayer)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_weaponAnimator = weaponAnimator;
            m_weaponSpriteRenderer = weaponSpriteRenderer;
            m_attackCollider = attackCollider;
            m_attackDuration = attackDuration;
            m_rotationOffset = rotationOffset;
            m_targetLayer = targetLayer;

            if (m_attackCollider != null)
            {
                m_attackCollider.isTrigger = true;
                SetAttackColliderActive(false);
            }

            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;

            ResetWeaponState();
        }

        private void ResetWeaponState()
        {
            m_isAttacking = false;
            m_hitMobInstanceIDs.Clear();

            SetAttackColliderActive(false);
            if (m_weaponAnimator != null) m_weaponAnimator.Rebind();

            m_ownerTransform.localRotation = Quaternion.identity;
            m_ownerTransform.localScale = Vector3.one;
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // CatPunch는 공격 시에만 콜라이더 업데이트를 수행합니다.
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            if (m_isAttacking) return;

            RotateWeaponToDirection(direction);

            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            PerformAttackAsync(m_attackCts.Token).Forget();
        }

        public override void Dispose()
        {
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;

            ResetWeaponState();
        }

        #endregion

        #region 공격 로직

        private void RotateWeaponToDirection(Vector3 direction)
        {
            if (direction == Vector3.zero) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            m_ownerTransform.rotation = Quaternion.Euler(0, 0, angle + m_rotationOffset);

            if (Mathf.Abs(angle) > 90)
                m_ownerTransform.localScale = new Vector3(1, -1, 1);
            else
                m_ownerTransform.localScale = new Vector3(1, 1, 1);
        }

        private async UniTaskVoid PerformAttackAsync(CancellationToken token)
        {
            m_isAttacking = true;
            m_hitMobInstanceIDs.Clear();

            try
            {
                if (m_weaponAnimator != null)
                {
                    int trigger = m_runtimeStats.IsEvolved ? k_AnimTriggerSlash : k_AnimTriggerStab;
                    m_weaponAnimator.speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                    m_weaponAnimator.SetTrigger(trigger);
                }

                SetAttackColliderActive(true);

                await WaitForAnimationAndCheckCollision(token);

                SetAttackColliderActive(false);

                float waitTime = m_runtimeStats.AttackSpeed > 0 
                    ? m_runtimeStats.CoolTime / m_runtimeStats.AttackSpeed 
                    : m_runtimeStats.CoolTime;
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 취소됨
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        private async UniTask WaitForAnimationAndCheckCollision(CancellationToken token)
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
            // 델리게이트를 통해 현재 조준 방향 가져오기
            Vector3 direction = m_getTargetDirection?.Invoke() ?? Vector3.zero;
            if (direction != Vector3.zero)
            {
                RotateWeaponToDirection(direction);
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
                    mob.TakeDamage(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);
                }
            }
        }

        private void SetAttackColliderActive(bool isActive)
        {
            if (m_attackCollider == null) return;

            if (m_attackCollider.gameObject != m_ownerTransform.gameObject)
                m_attackCollider.gameObject.SetActive(isActive);
            else
                m_attackCollider.enabled = isActive;
        }

        #endregion
    }
}
