using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using DG.Tweening; // DOTween 추가

namespace InGame.Weaphon
{
    public enum FriendAnimationType
    {
        TypeA = 0,
        TypeB = 1,
        TypeC = 2,
        TypeD = 3
    }
    
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CircleCollider2D))] 
    public class FriendCharacter : MonoBehaviour
    {
        [Header("Model Settings")]
        [Tooltip("4종류의 친구 캐릭터 모델 (TypeA ~ TypeD 순서)")]
        [SerializeField] private GameObject[] m_friendModels;

        [Header("Drop Settings")]
        [SerializeField] private float m_dropHeight = 15f;
        [SerializeField] private float m_dropDuration = 0.5f;
        [SerializeField] private Ease m_dropEase = Ease.InQuad;

        [Header("Warning Settings")]
        [SerializeField] private Animator m_warningAnimator;
        [SerializeField] private float m_warningDuration = 0.5f;

        [SerializeField]
        private CircleCollider2D m_collider;

        private float m_attackPower;
        private float m_mobStunTime;

        private void Awake()
        {
            // Animator 캐싱 제거 (자식 오브젝트에 각각 존재하거나 없을 수 있음)
            if (m_collider == null)
            {
                if (!TryGetComponent(out m_collider))
                {
                    m_collider = gameObject.AddComponent<CircleCollider2D>();
                }
            }
            m_collider.isTrigger = true; // 물리적 충돌 없이 이벤트만 발생
            m_collider.enabled = false; // 기본적으로 비활성화
        }

        /// <summary>
        /// 친구 캐릭터를 초기화하고 애니메이션을 시작합니다.
        /// </summary>
        /// <param name="position">캐릭터가 소환될 위치</param>
        /// <param name="animType">재생할 애니메이션 타입</param>
        /// <param name="attackPower">캐릭터의 공격력</param>
        /// <param name="mobStunTime">몬스터 기절 시간</param>
        public void Initialize(Vector3 position, FriendAnimationType animType, float attackPower, float mobStunTime)
        {
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;

            int typeIndex = (int)animType;

            // 모든 모델 비활성화 (초기화)
            if (m_friendModels != null)
            {
                foreach (var model in m_friendModels)
                {
                    if (model != null) model.SetActive(false);
                }
            }

            PlayDropActionAsync(position, typeIndex).Forget();
        }

        private async UniTaskVoid PlayDropActionAsync(Vector3 targetPosition, int typeIndex)
        {
            var token = this.GetCancellationTokenOnDestroy();
            
            // 1. 경고 단계
            // 목표 위치에 그대로 둠
            transform.position = targetPosition;
            
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(true);
                await UniTask.Delay(System.TimeSpan.FromSeconds(m_warningDuration), cancellationToken: token);
                // m_warningAnimator.gameObject.SetActive(false); // 낙하 완료 전까지 유지
            }

            // 2. 낙하 단계
            // 모델 활성화 찾기 및 활성화
            GameObject activeModel = null;
            if (m_friendModels != null && typeIndex >= 0 && typeIndex < m_friendModels.Length)
            {
                activeModel = m_friendModels[typeIndex];
            }

            if (activeModel != null)
            {
                activeModel.SetActive(true); // 낙하 시작 시 활성화
            }

            if (activeModel == null)
            {
                // 모델이 없으면 그냥 위치만 설정하고 종료 (예외처리)
                WeaponPoolManager.Instance.Release(this);
                return;
            }

            // 모델의 로컬 위치를 위로 올림 (Root는 바닥에 고정)
            activeModel.transform.localPosition = Vector3.up * m_dropHeight;
            
            m_collider.enabled = false;

            try
            {
                // 모델만 로컬 좌표 0으로 낙하
                await activeModel.transform.DOLocalMove(Vector3.zero, m_dropDuration)
                    .SetEase(m_dropEase)
                    .ToUniTask(cancellationToken: token);

                // 착지 후 콜라이더 활성화
                m_collider.enabled = true;
                
                // 구멍(경고) 닫기 (착지 후)
                if (m_warningAnimator != null)
                {
                    m_warningAnimator.gameObject.SetActive(false);
                }

                await UniTask.Delay(System.TimeSpan.FromSeconds(1.0f), cancellationToken: token);
            }
            finally
            {
                m_collider.enabled = false;
                // 풀 반납 전 모델 위치 초기화 (다음 사용을 위해)
                if (activeModel != null)
                {
                    activeModel.transform.localPosition = Vector3.zero;
                    activeModel.SetActive(false); // 애니메이션 종료 시 비활성화
                }
                WeaponPoolManager.Instance.Release(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob") && other.TryGetComponent<MobBase>(out var mob))
            {
                if (!mob.IsDead)
                {
                    mob.TakeDamage(m_attackPower, m_mobStunTime);
                }
            }
        }
    }
}