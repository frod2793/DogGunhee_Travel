using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using System.Threading;

namespace InGame.Weapon
{
    /// <summary>
    /// [설명]: 친구 소환 무기(Friends)의 개별 소환수 캐릭터를 관리하는 컴포넌트입니다.
    /// 지정된 위치에 경고 후 낙하하여 적에게 데미지를 입히고 사라집니다.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class FriendCharacter : MonoBehaviour
    {
        #region 데이터 구조

        public enum FriendAnimationType
        {
            TypeA = 0,
            TypeB = 1,
            TypeC = 2,
            TypeD = 3
        }

        #endregion

        #region 내부 변수 및 설정

        [Header("모델 설정")]
        [Tooltip("소환될 친구 캐릭터 모델 리스트 (TypeA ~ TypeD 순서)")]
        [SerializeField] private GameObject[] m_friendModels;

        [Header("낙하 연출 설정")]
        [Tooltip("낙하 시작 높이 (Y축)")]
        [SerializeField] private float m_dropHeight = 15f;

        [Tooltip("낙하에 걸리는 시간 (초)")]
        [SerializeField] private float m_dropDuration = 0.5f;

        [Tooltip("낙하 애니메이션 이징(Easing) 타입")]
        [SerializeField] private Ease m_dropEase = Ease.InQuad;

        [Header("경고 설정")]
        [Tooltip("낙하 지점 경고 애니메이터")]
        [SerializeField] private Animator m_warningAnimator;

        [Tooltip("경고 표시 지속 시간 (초)")]
        [SerializeField] private float m_warningDuration = 0.5f;

        // 컴포넌트 및 데이터
        private CircleCollider2D m_collider;
        private float m_attackPower;
        private float m_mobStunTime;
        private WeaponPoolManager m_poolManager;
        private CancellationTokenSource m_lifeCts;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 콜라이더 캐싱 및 설정
            m_collider = GetComponent<CircleCollider2D>();

            if (m_collider != null)
            {
                m_collider.isTrigger = true;
                m_collider.enabled = false;
            }
        }

        private void OnDisable()
        {
            // 비동기 작업 취소
            if (m_lifeCts != null)
            {
                m_lifeCts.Cancel();
                m_lifeCts.Dispose();
                m_lifeCts = null;
            }

            // 트윈 정리
            transform.DOKill();
            
            // 모델 비활성화 (다음 사용을 위해)
            if (m_friendModels != null)
            {
                foreach (var model in m_friendModels)
                {
                    if (model != null)
                    {
                        model.transform.DOKill(); // 하위 모델 트윈도 정리
                        model.SetActive(false);
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
            {
                mob.TakeDamage(m_attackPower, m_mobStunTime);
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 친구 캐릭터를 초기화하고 소환 시퀀스를 시작합니다.
        /// </summary>
        /// <param name="position">소환(낙하 목표) 위치</param>
        /// <param name="animType">소환할 친구 타입</param>
        /// <param name="attackPower">공격력</param>
        /// <param name="mobStunTime">스턴 시간</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        public void Init(Vector3 position, FriendAnimationType animType, float attackPower, float mobStunTime, WeaponPoolManager poolManager)
        {
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;
            m_poolManager = poolManager;

            // 시퀀스 시작
            PlayDropSequenceAsync(position, (int)animType).Forget();
        }

        #endregion

        #region 비동기 시퀀스

        /// <summary>
        /// [설명]: 경고 이펙트 -> 모델 낙하 -> 충격 판정 -> 소멸 순서로 진행되는 시퀀스입니다.
        /// </summary>
        private async UniTaskVoid PlayDropSequenceAsync(Vector3 targetPosition, int typeIndex)
        {
            // 이전 토큰 정리 및 새 토큰 생성
            if (m_lifeCts != null) m_lifeCts.Dispose();
            m_lifeCts = new CancellationTokenSource();
            
            // Unity Destroy와 연동된 토큰도 함께 확인 (안전장치)
            var destroyToken = this.GetCancellationTokenOnDestroy();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(m_lifeCts.Token, destroyToken);
            var token = linkedCts.Token;

            transform.position = targetPosition;

            // 1. 경고 표시
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(true);
                bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(m_warningDuration), cancellationToken: token).SuppressCancellationThrow();
                if (canceled) return; // 취소 시 즉시 종료 (finally 호출 안함)

                m_warningAnimator.gameObject.SetActive(false);
            }

            // 2. 모델 선택 및 활성화
            GameObject activeModel = GetModelByIndex(typeIndex);
            if (activeModel == null)
            {
                // 모델이 없으면 즉시 반환
                ReleaseToPool();
                return;
            }

            activeModel.SetActive(true);
            activeModel.transform.localPosition = Vector3.up * m_dropHeight;
            
            if (m_collider != null) m_collider.enabled = false;

            try
            {
                // 3. 낙하 애니메이션 (DOTween)
                await activeModel.transform
                    .DOLocalMove(Vector3.zero, m_dropDuration)
                    .SetEase(m_dropEase)
                    .ToUniTask(cancellationToken: token);

                // 4. 착지 및 공격 판정 활성화
                if (m_collider != null) m_collider.enabled = true;

                // 판정 유지 시간 (1초 후 사라짐)
                await UniTask.Delay(System.TimeSpan.FromSeconds(1.0f), cancellationToken: token);
                
                // 정상 완료 시에만 풀 반환 호출
                ReleaseToPool();
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨 (OnDisable 등). 이미 정리 중이므로 아무것도 안함.
            }
            catch (System.Exception ex)
            {
                // 예기치 않은 오류 시 로그 후 정리
                Debug.LogError($"[FriendCharacter] Error in drop sequence: {ex.Message}");
                if (gameObject.activeInHierarchy) ReleaseToPool();
            }
            finally
            {
                // 여기서는 정리 로직만 수행 (상태 복구)
                // ReleaseToPool은 호출하지 않음 (루프 방지)
                if (m_collider != null) m_collider.enabled = false;
                
                if (activeModel != null)
                {
                    activeModel.transform.localPosition = Vector3.zero;
                    activeModel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// [설명]: 인덱스에 해당하는 모델 오브젝트를 안전하게 가져옵니다.
        /// </summary>
        private GameObject GetModelByIndex(int index)
        {
            if (m_friendModels != null && index >= 0 && index < m_friendModels.Length)
            {
                return m_friendModels[index];
            }
            return null;
        }

        private void ReleaseToPool()
        {
            if (!gameObject.activeInHierarchy) return;

            if (m_poolManager != null)
            {
                m_poolManager.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion
    }
}