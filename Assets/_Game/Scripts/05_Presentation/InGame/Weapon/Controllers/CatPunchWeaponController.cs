using InGame.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 고양이 펀치(Cat Punch) 근접 무기를 제어하는 컨트롤러입니다.
    /// 애니메이션 재생에 맞춰 Sprite의 형상을 PolygonCollider2D로 실시간 변환하여 정밀한 타격 판정을 수행합니다.
    /// </summary>
    public class CatPunchWeaponController : WeaponControllerBase
    {
        #region 내부 변수 및 컴포넌트

        // 비주얼 및 물리 컴포넌트 (생성된 모델 내부)
        private GameObject m_weaponModelInstance;
        private Animator m_weaponAnimator;
        private SpriteRenderer m_weaponSpriteRenderer;
        private PolygonCollider2D m_attackCollider;

        // 로직 및 제어 변수
        private CatPunchWeaponLogic m_logic;
        private CancellationTokenSource m_attackCts;
        private bool m_isAttacking;

        // 설정 값 (View에서 주입)
        private float m_attackDuration = 0.2f;
        private float m_rotationOffset = -90f;
        private LayerMask m_targetLayer;

        // 물리 연산용 버퍼 (GC Alloc 방지)
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        private readonly List<Vector2> m_shapePointsBuffer = new List<Vector2>(512);
        private readonly List<Vector2> m_sampledPointsBuffer = new List<Vector2>(16);

        // 애니메이션 해시
        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("CatPhunch_Level1");
        private static readonly int k_AnimStateLevel2 = Animator.StringToHash("Catphunch_level2");

        #endregion

        #region 초기화 및 해제

        public override void Init(
            WeaponDataSO data, 
            Transform owner, 
            WeaponPoolManager poolManager, 
            Func<Vector3> getTargetDirection,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            base.Init(data, owner, poolManager, getTargetDirection, gameState, combatContext, playerContext);

            // 1. 무기 모델 인스턴스화
            if (data.ModelPrefab != null)
            {
                m_weaponModelInstance = UnityEngine.Object.Instantiate(data.ModelPrefab, null); // World Position 사용
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
                m_weaponModelInstance.transform.rotation = Quaternion.identity;

                // 컴포넌트 캐싱
                m_weaponAnimator = m_weaponModelInstance.GetComponentInChildren<Animator>();
                m_weaponSpriteRenderer = m_weaponAnimator != null 
                    ? m_weaponAnimator.GetComponent<SpriteRenderer>() 
                    : m_weaponModelInstance.GetComponentInChildren<SpriteRenderer>();
                
                m_attackCollider = m_weaponModelInstance.GetComponentInChildren<PolygonCollider2D>();
            }

            // 콜라이더 초기 설정
            if (m_attackCollider != null)
            {
                m_attackCollider.isTrigger = true;
                m_attackCollider.enabled = false;
            }

            // 2. 뷰 데이터(View Data) 바인딩
            ApplyViewSettings();

            // 3. 로직 및 물리 필터 설정
            m_logic = new CatPunchWeaponLogic(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);

            m_contactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = m_targetLayer
            };

            // 상태 초기화
            ResetWeaponState();
        }

        private void ApplyViewSettings()
        {
            // 기본값 설정
            m_targetLayer = LayerMask.GetMask("Enemy");

            if (m_poolManager != null)
            {
                var view = m_poolManager.GetComponent<CatPunchWeaponView>();
                if (view != null)
                {
                    m_attackDuration = view.AttackDuration;
                    m_rotationOffset = view.RotationOffset;
                    m_targetLayer = view.TargetLayer;
                }
            }
        }

        public override void Dispose()
        {
            CancelAttack();

            if (m_weaponModelInstance != null)
            {
                UnityEngine.Object.Destroy(m_weaponModelInstance);
                m_weaponModelInstance = null;
            }

            base.Dispose();
        }

        private void ResetWeaponState()
        {
            m_isAttacking = false;

            if (m_attackCollider != null) m_attackCollider.enabled = false;
            if (m_weaponAnimator != null) m_weaponAnimator.Rebind();

            if (m_weaponModelInstance != null)
            {
                m_weaponModelInstance.transform.rotation = Quaternion.identity;
                m_weaponModelInstance.transform.localScale = Vector3.one;
                m_weaponModelInstance.SetActive(false);
            }
        }

        #endregion

        #region 업데이트 루프

        public override void OnLateUpdate()
        {
            base.OnLateUpdate();

            // 플레이어 위치 동기화 (공격 중에도 따라다녀야 함)
            if (m_weaponModelInstance != null && m_ownerTransform != null)
            {
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
            }
        }

        #endregion

        #region 공격 실행 로직

        protected override void ExecuteAttack(Vector3 direction)
        {
            if (m_isAttacking) return;

            // 이전 작업 취소 및 새 토큰 생성
            CancelAttack();
            m_attackCts = new CancellationTokenSource();

            // 비동기 공격 시작
            PerformAttackAsync(m_attackCts.Token).Forget();
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

        private async UniTaskVoid PerformAttackAsync(CancellationToken token)
        {
            m_isAttacking = true;
            m_logic?.ResetHitHistory();

            try
            {
                // 1. 활성화 및 초기 배치
                if (m_weaponModelInstance != null) m_weaponModelInstance.SetActive(true);
                
                Vector3 targetDir = m_getTargetDirection?.Invoke() ?? Vector3.zero;
                RotateWeaponToDirection(targetDir);

                // 2. 애니메이션 재생
                if (m_weaponAnimator != null)
                {
                    int animHash = m_runtimeStats.IsEvolved ? k_AnimStateLevel2 : k_AnimStateLevel1;
                    float speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                    
                    m_weaponAnimator.speed = speed;
                    m_weaponAnimator.Play(animHash, 0, 0f);
                    // 즉시 업데이트하여 첫 프레임 깜빡임 방지
                    m_weaponAnimator.Update(0f); 
                }

                if (m_attackCollider != null) m_attackCollider.enabled = true;

                // 3. 물리 판정 루프 (지속 시간 동안)
                float elapsedTime = 0f;
                // m_attackDuration은 초 단위
                while (elapsedTime < m_attackDuration)
                {
                    // 프레임별 물리 갱신 처리
                    ProcessCollisionFrame();

                    // 다음 물리 프레임 대기
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                    elapsedTime += Time.fixedDeltaTime;
                }
            }
            catch (OperationCanceledException)
            {
                // 공격 취소됨 (정상 흐름)
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CatPunch] 공격 중 오류 발생: {ex.Message}");
            }
            finally
            {
                // 4. 종료 처리
                ResetWeaponState();
            }
        }

        #endregion

        #region 물리 및 충돌 처리

        /// <summary>
        /// 매 프레임(FixedUpdate) 호출되어 위치 동기화, 회전, 콜라이더 갱신, 충돌 체크를 수행합니다.
        /// </summary>
        private void ProcessCollisionFrame()
        {
            // 위치 동기화 (빠른 움직임 대응)
            if (m_weaponModelInstance != null && m_ownerTransform != null)
            {
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
            }

            // 방향 갱신
            Vector3 targetDir = m_getTargetDirection?.Invoke() ?? Vector3.zero;
            RotateWeaponToDirection(targetDir);

            // 콜라이더 쉐입 갱신 및 충돌 판정
            UpdateColliderShapeFromSprite();
            CheckCollision();
        }

        private void RotateWeaponToDirection(Vector3 direction)
        {
            if (direction == Vector3.zero || m_weaponModelInstance == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 회전 적용 (기본 오프셋 포함)
            m_weaponModelInstance.transform.rotation = Quaternion.Euler(0, 0, angle + m_rotationOffset);

            // 상하 반전 처리 (스프라이트가 뒤집히지 않게 Y축 스케일 조정)
            // 각도가 -90 ~ 90 범위를 벗어나면 뒤집힘
            bool isFlipped = Mathf.Abs(angle) > 90f;
            float scaleY = isFlipped ? -1f : 1f;
            m_weaponModelInstance.transform.localScale = new Vector3(1f, scaleY, 1f);
        }

        /// <summary>
        /// SpriteRenderer의 현재 프레임 이미지를 기반으로 PolygonCollider2D의 형태를 재설정합니다.
        /// 최적화를 위해 정점 개수를 다운샘플링합니다.
        /// </summary>
        private void UpdateColliderShapeFromSprite()
        {
            if (m_attackCollider == null || m_weaponSpriteRenderer == null || m_weaponSpriteRenderer.sprite == null)
            {
                return;
            }

            Sprite sprite = m_weaponSpriteRenderer.sprite;
            int shapeCount = sprite.GetPhysicsShapeCount();

            // 쉐입 개수 맞추기
            if (m_attackCollider.pathCount != shapeCount)
            {
                m_attackCollider.pathCount = shapeCount;
            }

            for (int i = 0; i < shapeCount; i++)
            {
                m_shapePointsBuffer.Clear();
                sprite.GetPhysicsShape(i, m_shapePointsBuffer);

                int originalCount = m_shapePointsBuffer.Count;

                // 정점이 너무 많으면 9개로 다운샘플링 (최적화)
                if (originalCount > 9)
                {
                    m_sampledPointsBuffer.Clear();
                    for (int j = 0; j < 9; j++)
                    {
                        // 등간격 인덱스 추출
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

        private void CheckCollision()
        {
            if (m_attackCollider == null || !m_attackCollider.enabled) return;

            // 충돌 감지
            int hitCount = m_attackCollider.Overlap(m_contactFilter, m_hitResults);
            if (hitCount <= 0) return;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = m_hitResults[i];
                if (col == null) continue;

                // 중복 타격 방지 (Logic 위임)
                int instanceId = col.gameObject.GetInstanceID();
                if (m_logic.RegisterHit(instanceId))
                {
                    // 데미지 적용
                    if (col.TryGetComponent(out MobBase mob) || (mob = col.GetComponentInParent<MobBase>()))
                    {
                        mob.TakeDamage(m_logic.AttackPower, m_logic.MobStunTime);
                    }
                }
            }
        }

        #endregion
    }
}