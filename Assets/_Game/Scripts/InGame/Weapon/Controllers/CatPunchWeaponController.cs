using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.Player.Player_Base;
using InGame.Manager;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.ObjectPool;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 고양이 펀치 근접 공격을 담당하는 컨트롤러입니다.
    /// View와 Logic을 중개하며, 애니메이션 기반의 물리 판정을 제어합니다.
    /// </summary>
    public class CatPunchWeaponController : WeaponControllerBase
    {
        #region 내부 상태 및 변수

        private Animator m_weaponAnimator;
        private SpriteRenderer m_weaponSpriteRenderer;
        private PolygonCollider2D m_attackCollider;

        private CatPunchWeaponLogic m_logic;
        private CancellationTokenSource m_attackCts;
        private GameObject m_weaponModelInstance;

        private float m_attackDuration = 0.2f;
        private float m_rotationOffset = -90f;
        private LayerMask m_targetLayer;
        private bool m_isAttacking;

        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        private readonly List<Vector2> m_shapePointsBuffer = new List<Vector2>(512);
        private readonly List<Vector2> m_sampledPointsBuffer = new List<Vector2>(16);

        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("CatPhunch_Level1");
        private static readonly int k_AnimStateLevel2 = Animator.StringToHash("Catphunch_level2");

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 모델 생성 및 데이터 연동을 수행합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform ownerTransform, Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 모델 인스턴스화 (플레이어 스케일 영향을 피하기 위해 루트로 생성)
            if (data.ModelPrefab != null)
            {
                m_weaponModelInstance = UnityEngine.Object.Instantiate(data.ModelPrefab, null);
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
                m_weaponModelInstance.transform.rotation = Quaternion.identity;

                m_weaponAnimator = m_weaponModelInstance.GetComponentInChildren<Animator>();

                if (m_weaponAnimator != null)
                {
                    m_weaponSpriteRenderer = m_weaponAnimator.GetComponent<SpriteRenderer>();
                }

                if (m_weaponSpriteRenderer == null)
                {
                    m_weaponSpriteRenderer = m_weaponModelInstance.GetComponentInChildren<SpriteRenderer>();
                }

                m_attackCollider = m_weaponModelInstance.GetComponentInChildren<PolygonCollider2D>();
            }

            if (m_attackCollider != null)
            {
                m_attackCollider.isTrigger = true;
                m_attackCollider.enabled = false;
            }

            // 2. 튜닝 데이터 추출
            CatPunchWeaponView viewSettings = null;
            if (WeaponPoolManager.Instance != null)
            {
                viewSettings = WeaponPoolManager.Instance.GetComponent<CatPunchWeaponView>();
            }

            if (viewSettings != null)
            {
                m_attackDuration = viewSettings.AttackDuration;
                m_rotationOffset = viewSettings.RotationOffset;
                m_targetLayer = viewSettings.TargetLayer;
            }
            else
            {
                m_targetLayer = LayerMask.GetMask("Mob");
            }

            // 3. 로직 및 물리 설정
            m_logic = new CatPunchWeaponLogic(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);

            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;

            ResetWeaponState();
        }

        /// <summary>
        /// 무기 사용 중단 및 할당된 모델/태스크를 정리합니다.
        /// </summary>
        public override void Dispose()
        {
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;

            if (m_weaponModelInstance != null)
            {
                UnityEngine.Object.Destroy(m_weaponModelInstance);
            }
        }

        /// <summary>
        /// 무기의 상태를 비활성 초기 상태로 되돌립니다.
        /// </summary>
        private void ResetWeaponState()
        {
            m_isAttacking = false;

            if (m_attackCollider != null)
            {
                m_attackCollider.enabled = false;
            }

            if (m_weaponAnimator != null)
            {
                m_weaponAnimator.Rebind();
            }

            if (m_weaponModelInstance != null)
            {
                m_weaponModelInstance.transform.rotation = Quaternion.identity;
                m_weaponModelInstance.transform.localScale = Vector3.one;
                m_weaponModelInstance.SetActive(false);
            }
        }

        #endregion

        #region 업데이트 루프

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
        }

        public override void OnLateUpdate()
        {
            base.OnLateUpdate();

            // 위치 동기화 (Player 부모 하위가 아니므로 직접 동기화)
            if (m_weaponModelInstance != null && m_ownerTransform != null)
            {
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
            }
        }

        #endregion

        #region 공격 실행 및 비동기 루프

        /// <summary>
        /// 공격 명령을 수신하여 비동기 공격 루틴을 시작합니다.
        /// </summary>
        protected override void ExecuteAttack(Vector3 direction)
        {
            if (m_isAttacking)
            {
                return;
            }

            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            PerformAttackAsync(m_attackCts.Token).Forget();
        }

        /// <summary>
        /// 실제 공격 애니메이션 재생 및 판정 처리를 수행하는 비동기 메서드입니다.
        /// </summary>
        private async UniTaskVoid PerformAttackAsync(CancellationToken token)
        {
            m_isAttacking = true;
            m_logic?.ResetHitHistory();

            try
            {
                if (m_weaponModelInstance != null)
                {
                    m_weaponModelInstance.SetActive(true);
                }

                RotateWeaponToDirection(m_getTargetDirection?.Invoke() ?? Vector3.zero);

                if (m_weaponAnimator != null)
                {
                    int stateHash = m_runtimeStats.IsEvolved ? k_AnimStateLevel2 : k_AnimStateLevel1;
                    m_weaponAnimator.speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                    m_weaponAnimator.Play(stateHash, 0, 0f);
                    m_weaponAnimator.Update(0);
                }

                if (m_attackCollider != null)
                {
                    m_attackCollider.enabled = true;
                }

                ProcessCollisionFrameUpdate();

                // 설정된 기간 동안 판정 루프 수행
                await ExecuteAttackFixedLoop(token);
            }
            catch (OperationCanceledException)
            {
                // 정상 취소
            }
            catch (Exception e)
            {
                Debug.LogError($"[CatPunch] {e.Message}");
            }
            finally
            {
                if (m_attackCollider != null)
                {
                    m_attackCollider.enabled = false;
                }

                if (m_weaponModelInstance != null)
                {
                    m_weaponModelInstance.SetActive(false);
                }

                m_isAttacking = false;
            }
        }

        /// <summary>
        /// 지정된 공격 지속 시간 동안 물리 판정을 갱신하는 루프입니다.
        /// </summary>
        private async UniTask ExecuteAttackFixedLoop(CancellationToken token)
        {
            float elapsedTime = 0f;
            float targetDuration = m_attackDuration;

            while (elapsedTime < targetDuration)
            {
                elapsedTime += Time.deltaTime;
                ProcessCollisionFrameUpdate();

                // 애니메이션 프레임에 맞춰 콜라이더를 갱신하기 위해 LastUpdate 타이밍에 재개
                await UniTask.Yield(PlayerLoopTiming.LastUpdate, token);
            }
        }

        #endregion

        #region 물리 및 충돌 처리

        /// <summary>
        /// 프레임별로 위치, 회전, 콜라이더 쉐입을 갱신하고 충돌을 체크합니다.
        /// </summary>
        private void ProcessCollisionFrameUpdate()
        {
            if (m_weaponModelInstance != null && m_ownerTransform != null)
            {
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
            }

            RotateWeaponToDirection(m_getTargetDirection?.Invoke() ?? Vector3.zero);
            UpdateColliderShape();
            CheckCollision();
        }

        /// <summary>
        /// 무기를 공격 방향으로 회전시킵니다.
        /// </summary>
        private void RotateWeaponToDirection(Vector3 direction)
        {
            if (direction == Vector3.zero || m_weaponModelInstance == null)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            m_weaponModelInstance.transform.rotation = Quaternion.Euler(0, 0, angle + m_rotationOffset);

            // 상하 반전 처리
            if (Mathf.Abs(angle) > 90)
            {
                m_weaponModelInstance.transform.localScale = new Vector3(1, -1, 1);
            }
            else
            {
                m_weaponModelInstance.transform.localScale = new Vector3(1, 1, 1);
            }
        }

        /// <summary>
        /// 현재 애니메이션 스프라이트의 외형에 맞춰 물리 콜라이더 정점을 실시간으로 갱신합니다.
        /// </summary>
        private void UpdateColliderShape()
        {
            if (m_attackCollider == null || m_weaponSpriteRenderer == null || m_weaponSpriteRenderer.sprite == null)
            {
                return;
            }

            int shapeCount = m_weaponSpriteRenderer.sprite.GetPhysicsShapeCount();
            if (shapeCount == 0)
            {
                m_attackCollider.pathCount = 0;
                return;
            }

            if (m_attackCollider.pathCount != shapeCount)
            {
                m_attackCollider.pathCount = shapeCount;
            }

            for (int i = 0; i < shapeCount; i++)
            {
                m_shapePointsBuffer.Clear();
                m_weaponSpriteRenderer.sprite.GetPhysicsShape(i, m_shapePointsBuffer);

                int originalCount = m_shapePointsBuffer.Count;

                // 정점 개수를 최적화를 위해 9개로 제한 샘플링
                if (originalCount > 9)
                {
                    m_sampledPointsBuffer.Clear();
                    for (int j = 0; j < 9; j++)
                    {
                        int index = Mathf.FloorToInt((float)j * (originalCount - 1) / 8);
                        m_sampledPointsBuffer.Add(m_shapePointsBuffer[index]);
                    }
                    m_attackCollider.SetPath(i, m_sampledPointsBuffer);
                }
                else
                {
                    m_attackCollider.SetPath(i, m_shapePointsBuffer);
                }
            }
        }

        /// <summary>
        /// 갱신된 콜라이더 범위 내의 적을 탐색하여 데미지를 적용합니다.
        /// </summary>
        private void CheckCollision()
        {
            if (m_attackCollider == null || !m_attackCollider.enabled)
            {
                return;
            }

            int hitCount = m_attackCollider.Overlap(m_contactFilter, m_hitResults);
            if (hitCount <= 0)
            {
                return;
            }

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                if (target == null)
                {
                    continue;
                }

                int id = target.gameObject.GetInstanceID();
                if (m_logic.RegisterHit(id))
                {
                    if (target.TryGetComponent(out MobBase mob) || target.GetComponentInParent<MobBase>() != null)
                    {
                        if (mob == null)
                        {
                            mob = target.GetComponentInParent<MobBase>();
                        }
                        
                        mob.TakeDamage(m_logic.AttackPower, m_logic.MobStunTime);
                    }
                }
            }
        }

        #endregion
    }
}
