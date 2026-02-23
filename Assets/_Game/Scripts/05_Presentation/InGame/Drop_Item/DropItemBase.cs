using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using InGame.ObjectPool;
using InGame.Managers;

namespace InGame.vamsir
{
    /// <summary>
    /// [설명]: 몬스터가 드랍하는 아이템(코인, 경험치 등)의 베이스 클래스입니다.
    /// </summary>
    public class DropItemBase : MonoBehaviour, IObjectPoolUser
    {
        #region 내부 필드 

        // 인터페이스 구현
        public ObjectPoolSpawner ObjectPoolSpawner { get; set; }

        [Header("플로팅 효과 설정")]
        [Tooltip("아이템이 위아래로 떠다니는 최대 높이")]
        [FormerlySerializedAs("floatHeight")]
        [SerializeField]
        protected float m_floatHeight = 0.5f;

        [Tooltip("한 번 위아래(Yoyo) 왕복하는 시간")]
        [FormerlySerializedAs("floatDuration")]
        [SerializeField]
        protected float m_floatDuration = 1.0f;

        [Header("회전 효과 설정")]
        [Tooltip("회전 각도 (Y축 기준, 3D 느낌 연출 시 사용)")]
        [FormerlySerializedAs("rotationAngle")]
        [SerializeField]
        protected float m_rotationAngle = 180f;

        [Tooltip("회전 지속 시간")]
        [FormerlySerializedAs("rotationDuration")]
        [SerializeField]
        protected float m_rotationDuration = 2.0f;

        [Header("생명주기 설정")]
        [Tooltip("자동으로 사라지기까지의 시간 (초)")]
        [FormerlySerializedAs("lifeTime")]
        [SerializeField]
        protected float m_lifeTime = 30f;

        // 내부 변수
        private CancellationTokenSource m_lifeTimeCts;
        private Tween m_floatTween;
        private Tween m_rotateTween;
        private Vector3 m_initialLocalScale; // 스케일 복구용

        #endregion

        #region 유니티 생명주기 

        protected virtual void Awake()
        {
            // 풀링 시 크기가 변형될 수 있으므로 초기 크기 저장
            m_initialLocalScale = transform.localScale;

            // 트리거 판정을 보장하여 물리적 밀림 방지
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        protected virtual void OnEnable()
        {
            // 상태 초기화
            transform.localScale = m_initialLocalScale;
            transform.rotation = Quaternion.identity;

            // 효과 및 타이머 시작
            StartVisualEffects();
            StartReturnTimer();
        }

        protected virtual void OnDisable()
        {
            // 타이머 취소
            if (m_lifeTimeCts != null)
            {
                m_lifeTimeCts.Cancel();
                m_lifeTimeCts.Dispose();
                m_lifeTimeCts = null;
            }

            // 트윈 정리 (SetLink를 썼지만 명시적 종료가 풀링에서 더 안전함)
            m_floatTween?.Kill();
            m_rotateTween?.Kill();
        }

        #endregion

        #region 비주얼 효과 

        /// <summary>
        /// [설명]: 플로팅 및 회전 효과를 시작합니다.
        /// </summary>
        protected virtual void StartVisualEffects()
        {
            // 1. 플로팅 효과 (위아래 둥둥)
            m_floatTween = transform.DOLocalMoveY(m_floatHeight, m_floatDuration)
                .SetRelative(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject); // 오브젝트 파괴 시 자동 Kill

            // 2. 회전 효과
            m_rotateTween = transform.DOLocalRotate(new Vector3(0, m_rotationAngle, 0), m_rotationDuration,
                    RotateMode.FastBeyond360)
                .SetRelative(true)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental) // 계속해서 회전
                .SetLink(gameObject);
        }

        #endregion

        #region 생명주기 관리 

        private void StartReturnTimer()
        {
            // 이전 CTS가 남아있다면 정리
            m_lifeTimeCts?.Cancel();
            m_lifeTimeCts?.Dispose();
            m_lifeTimeCts = new CancellationTokenSource();

            ReturnToPoolAfterDelayAsync(m_lifeTimeCts.Token).Forget();
        }

        private async UniTaskVoid ReturnToPoolAfterDelayAsync(CancellationToken token)
        {
            try
            {
                // 지정된 시간만큼 대기
                await UniTask.Delay(TimeSpan.FromSeconds(m_lifeTime), cancellationToken: token);

                // 대기 후 반환 로직
                ReturnToPool();
            }
            catch (OperationCanceledException)
            {
                // OnDisable에 의해 취소된 경우 (정상 동작)
            }
        }

        /// <summary>
        /// [설명]: 아이템을 풀로 반환하거나 파괴합니다.
        /// </summary>
        public void ReturnToPool()
        {
            // 이미 비활성화된 경우 중복 반환 방지
            if (!gameObject.activeSelf) return;

            if (ObjectPoolSpawner != null)
            {
                ObjectPoolSpawner.ReturnItem(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion
    }
}