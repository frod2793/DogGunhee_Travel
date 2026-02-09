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
    /// View(Unity Component)와 Logic(POCO)을 중개합니다.
    /// </summary>
    public class CatPunchWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private Animator m_weaponAnimator;
        private SpriteRenderer m_weaponSpriteRenderer;
        private PolygonCollider2D m_attackCollider;
        
        private float m_attackDuration = 0.2f;
        private float m_rotationOffset = -90f;
        private LayerMask m_targetLayer;

        #endregion

        #region 내부 상태

        private CatPunchWeaponLogic m_logic;
        
        private bool m_isAttacking;
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        // 콜라이더 정점 처리를 위한 버퍼 (충분한 크기로 할당)
        private readonly List<Vector2> m_shapePointsBuffer = new List<Vector2>(512);
        private readonly List<Vector2> m_sampledPointsBuffer = new List<Vector2>(16);

        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("CatPhunch_Level1");
        private static readonly int k_AnimStateLevel2 = Animator.StringToHash("Catphunch_level2");

        private CancellationTokenSource m_attackCts;
        private GameObject m_weaponModelInstance;

        #endregion

        #region 초기화

        public override void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 모델 인스턴스화 (부모 없이 생성하여 Player의 Scale 영향 제거)
            if (data.ModelPrefab != null)
            {
                m_weaponModelInstance = UnityEngine.Object.Instantiate(data.ModelPrefab, null);
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
                m_weaponModelInstance.transform.rotation = Quaternion.identity;

                m_weaponAnimator = m_weaponModelInstance.GetComponentInChildren<Animator>();
                
                // 애니메이터와 동일한 오브젝트에 있는 렌더러를 우선 탐색
                if (m_weaponAnimator != null)
                {
                    m_weaponSpriteRenderer = m_weaponAnimator.GetComponent<SpriteRenderer>();
                }
                
                // 없다면 자식/부모/전체 탐색
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

        private void ResetWeaponState()
        {
            m_isAttacking = false;
            
            if (m_attackCollider != null) m_attackCollider.enabled = false;
            if (m_weaponAnimator != null) m_weaponAnimator.Rebind();

            if (m_weaponModelInstance != null)
            {
                m_weaponModelInstance.transform.rotation = Quaternion.identity;
                m_weaponModelInstance.transform.localScale = Vector3.one;
                // 초기 상태는 비활성화 (공격 시에만 켜짐)
                m_weaponModelInstance.SetActive(false);
            }
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime) 
        {
            base.OnUpdate(deltaTime);
        }

        public override void OnLateUpdate()
        {
            base.OnLateUpdate();
            
            // 무기 모델 위치 동기화 (Player Scale 영향 안 받음)
            if (m_weaponModelInstance != null && m_ownerTransform != null)
            {
                m_weaponModelInstance.transform.position = m_ownerTransform.position;
            }
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 공격 중이면 중복 실행 방지
            if (m_isAttacking) return;

            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            PerformAttackAsync(m_attackCts.Token).Forget();
        }

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

        #endregion

        #region 공격 로직

        private void RotateWeaponToDirection(Vector3 direction)
        {
            if (direction == Vector3.zero || m_weaponModelInstance == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 무기 인스턴스 회전 (z축)
            m_weaponModelInstance.transform.rotation = Quaternion.Euler(0, 0, angle + m_rotationOffset);

            // 무기 인스턴스 반전 (90도 넘어가면 y축 뒤집기 - 상하반전 방지)
            if (Mathf.Abs(angle) > 90)
                m_weaponModelInstance.transform.localScale = new Vector3(1, -1, 1);
            else
                m_weaponModelInstance.transform.localScale = new Vector3(1, 1, 1);
        }

        private async UniTaskVoid PerformAttackAsync(CancellationToken token)
        {
            // [Step 1] 상태 진입
            m_isAttacking = true;
            m_logic?.ResetHitHistory();

            try
            {
                // [Fix] 공격 시작 시 오브젝트 활성화
                if (m_weaponModelInstance != null) 
                {
                    m_weaponModelInstance.SetActive(true);
                }

                // [Step 2] 시각적/물리 셋업
                RotateWeaponToDirection(m_getTargetDirection?.Invoke() ?? Vector3.zero);

                if (m_weaponAnimator != null)
                {
                    // [Approved Fix] Trigger 방식이 씹히는 문제를 해결하기 위해 Play 사용
                    
                    // [Fix] Trigger 제어 제거 및 Play를 통한 명시적 상태 재생
                    // 업그레이드 여부에 따라 정확한 애니메이션 State 재생 보장
                    int stateHash;
                    if (m_runtimeStats.IsEvolved)
                    {
                        stateHash = k_AnimStateLevel2;
                        // Debug.Log("[CatPunch] Play Evolved Animation (Level 2)");
                    }
                    else
                    {
                        stateHash = k_AnimStateLevel1;
                        // Debug.Log("[CatPunch] Play Normal Animation (Level 1)");
                    }
                    
                    m_weaponAnimator.speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                    
                    // 0번 레이어의 해당 상태를 즉시 처음(0f)부터 실행
                    m_weaponAnimator.Play(stateHash, 0, 0f);
                    
                    // [Fix] Play 호출 즉시 애니메이터를 업데이트하여 SpriteRenderer.sprite가 첫 프레임으로 갱신되게 함
                    // 그래야 바로 뒤에 이어지는 UpdateColliderShape가 올바른 펀치 모양을 잡음
                    m_weaponAnimator.Update(0);
                }

                if (m_attackCollider != null) m_attackCollider.enabled = true;

                // [Fix] 공격 시작 즉시 충돌 판정 수행 (첫 프레임 지연 방지)
                ProcessCollisionFrameUpdate();

                // [Step 3] 고정 시간 루프 (애니메이션 상태에 의존하지 않음)
                await ExecuteAttackFixedLoop(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[CatPunch] {e.Message}"); }
            finally
            {
                // 상태 해제
                if (m_attackCollider != null) m_attackCollider.enabled = false;
                
                // 공격 종료 시 오브젝트 비활성화 (풀링 효과)
                if (m_weaponModelInstance != null)
                {
                    m_weaponModelInstance.SetActive(false);
                }
                
                m_isAttacking = false; 
            }
        }

        /// <summary>
        /// 설정된 m_attackDuration 동안 물리 판정을 수행하는 안정적인 루프입니다.
        /// </summary>
        private async UniTask ExecuteAttackFixedLoop(CancellationToken token)
        {
            float elapsedTime = 0f;
            float targetDuration = m_attackDuration;

            // 루프 시작
            while (elapsedTime < targetDuration)
            {
                elapsedTime += Time.deltaTime;
                
                // 매 프레임 위치/회전/충돌 갱신
                ProcessCollisionFrameUpdate();
                
                // 애니메이터가 Update에서 스프라이트를 교체하므로, 
                // LateUpdate 시점에 접근해야 방금 바뀐 스프라이트의 쉐입을 가져올 수 있음
                await UniTask.Yield(PlayerLoopTiming.LastUpdate, token);
            }
        }

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

            if (m_attackCollider.pathCount != shapeCount)
                m_attackCollider.pathCount = shapeCount;

            for (int i = 0; i < shapeCount; i++)
            {
                m_shapePointsBuffer.Clear();
                m_weaponSpriteRenderer.sprite.GetPhysicsShape(i, m_shapePointsBuffer);
                
                int originalCount = m_shapePointsBuffer.Count;

                // 정점 개수를 최대 9개로 제한하되, 샘플링 간격 계산 시 0으로 나누기 방지
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

        private void CheckCollision()
        {
            if (m_attackCollider == null || !m_attackCollider.enabled) return;

            int hitCount = m_attackCollider.Overlap(m_contactFilter, m_hitResults);
            
            // 히트 카운트가 0보다 클 때만 처리
            if (hitCount > 0)
            {
                // 충돌 처리 로직
            }

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                if (target == null) continue;

                int id = target.gameObject.GetInstanceID();
                if (m_logic.RegisterHit(id))
                {
                    // 직접 컴포넌트를 찾지 못할 경우 부모에서도 찾아봄 (HitBox 구조 대응)
                    if (target.TryGetComponent(out MobBase mob) || target.GetComponentInParent<MobBase>() != null)
                    {
                        if (mob == null) mob = target.GetComponentInParent<MobBase>();
                        
                        mob.TakeDamage(m_logic.AttackPower, m_logic.MobStunTime);
                    }
                }
            }
        }

        #endregion
    }
}
