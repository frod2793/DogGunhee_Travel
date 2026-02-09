using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using DG.Tweening;
using System.Threading;

namespace InGame.Weapon
{
    /// <summary>
    /// 친구 소환 무기(Friends)의 개별 소환수 캐릭터를 관리하는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
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

        #region 내부 상태 및 변수

        [Header("모델 설정")]
        [Tooltip("4종류의 친구 캐릭터 모델 (TypeA ~ TypeD 순서)")]
        [SerializeField] private GameObject[] m_friendModels;

        [Header("낙하 설정")]
        [SerializeField] private float m_dropHeight = 15f;
        [SerializeField] private float m_dropDuration = 0.5f;
        [SerializeField] private Ease m_dropEase = Ease.InQuad;

        [Header("경고 설정")]
        [SerializeField] private Animator m_warningAnimator;
        [SerializeField] private float m_warningDuration = 0.5f;

        private CircleCollider2D m_collider;
        private float m_attackPower;
        private float m_mobStunTime;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_collider == null && !TryGetComponent(out m_collider))
            {
                m_collider = gameObject.AddComponent<CircleCollider2D>();
            }
            m_collider.isTrigger = true;
            m_collider.enabled = false;
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

        #endregion

        #region 초기화 및 상태 관리

        /// <summary>
        /// 친구 캐릭터를 초기화하고 소환 시퀀스를 시작합니다. (Initialize -> Init)
        /// </summary>
        public void Init(Vector3 position, FriendAnimationType animType, float attackPower, float mobStunTime)
        {
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;

            if (m_friendModels != null)
            {
                foreach (var model in m_friendModels)
                {
                    if (model != null)
                    {
                        model.SetActive(false);
                    }
                }
            }

            PlayDropSequenceAsync(position, (int)animType).Forget();
        }

        #endregion

        #region 비동기 소환 시퀀스

        /// <summary>
        /// 경고 이펙트 후 하늘에서 떨어지는 소환 시퀀스를 수행합니다.
        /// </summary>
        private async UniTaskVoid PlayDropSequenceAsync(Vector3 targetPosition, int typeIndex)
        {
            var token = this.GetCancellationTokenOnDestroy();
            transform.position = targetPosition;

            // 1. 경고 표시
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(true);
                await UniTask.Delay(System.TimeSpan.FromSeconds(m_warningDuration), cancellationToken: token);
            }

            // 2. 모델 활성화
            GameObject activeModel = (m_friendModels != null && typeIndex >= 0 && typeIndex < m_friendModels.Length) ? m_friendModels[typeIndex] : null;

            if (activeModel != null)
            {
                activeModel.SetActive(true);
            }
            else
            {
                if (WeaponPoolManager.Instance != null)
                {
                    WeaponPoolManager.Instance.Release(this);
                }
                return;
            }

            activeModel.transform.localPosition = Vector3.up * m_dropHeight;
            m_collider.enabled = false;

            try
            {
                // 3. 낙하 애니메이션
                await activeModel.transform.DOLocalMove(Vector3.zero, m_dropDuration)
                    .SetEase(m_dropEase)
                    .ToUniTask(cancellationToken: token);

                // 4. 충격 판정 및 유지
                m_collider.enabled = true;
                if (m_warningAnimator != null)
                {
                    m_warningAnimator.gameObject.SetActive(false);
                }

                await UniTask.Delay(System.TimeSpan.FromSeconds(1.0f), cancellationToken: token);
            }
            finally
            {
                // 5. 리셋 및 풀 반환
                m_collider.enabled = false;
                if (activeModel != null)
                {
                    activeModel.transform.localPosition = Vector3.zero;
                    activeModel.SetActive(false);
                }

                if (WeaponPoolManager.Instance != null)
                {
                    WeaponPoolManager.Instance.Release(this);
                }
            }
        }

        #endregion
    }
}