using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace InGame.Effect
{
    /// <summary>
    /// 오브젝트 풀링되는 이펙트(파티클, 애니메이션) 프리팹에 부착되는 컴포넌트입니다.
    /// <br/> 재생이 완료되면 자동으로 자신을 풀에 반환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledEffect : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("설정")]
        [SerializeField, Tooltip("자동 반환 시간을 강제로 설정합니다. (0보다 크면 우선 적용)")] 
        private float m_overrideDuration = -1f;

        #endregion

        #region 2. 내부 변수 (Fields)

        private IObjectPool<PooledEffect> m_pool;
        private ParticleSystem m_particleSystem;
        private Animator m_animator;
        
        // 비동기 제어
        private CancellationTokenSource m_cts;

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
            m_animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            // 활성화될 때마다 타이머 시작
            StartAutoReturnToPoolAsync().Forget();
        }

        private void OnDisable()
        {
            // 비활성화 시 진행 중인 타이머 취소
            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
                m_cts = null;
            }
            
            // 주의: 여기서 m_pool.Release(this)를 호출하면 안 됩니다.
            // Pool.Release()가 객체를 비활성화(OnDisable)시키기 때문에 무한 루프나 예외가 발생할 수 있습니다.
        }

        #endregion

        #region 4. 초기화 및 설정

        /// <summary>
        /// 이 객체를 관리하는 오브젝트 풀을 설정합니다. (생성 시 1회 호출)
        /// </summary>
        public void SetPool(IObjectPool<PooledEffect> pool)
        {
            m_pool = pool;
        }

        #endregion

        #region 5. 반환 로직

        private async UniTaskVoid StartAutoReturnToPoolAsync()
        {
            // 기존 토큰 정리
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = new CancellationTokenSource();
            
            var token = m_cts.Token;

            // 지속 시간 계산
            float duration = GetDuration();

            if (duration > 0f)
            {
                try
                {
                    // 시간 대기
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);

                    // 시간이 다 되면 풀로 반환
                    ReturnToPool();
                }
                catch (OperationCanceledException)
                {
                    // 비활성화되어 취소됨 -> 아무것도 하지 않음
                }
            }
            else if (duration == -1f)
            {
                // 무한 루프거나 지속 시간을 알 수 없는 경우 경고 로그 (필요 시 주석 처리)
                // LogManager.LogWarning($"[PooledEffect] '{name}'의 지속 시간을 계산할 수 없습니다. 수동으로 반환해야 합니다.", LogManager.LogCategory.Effect);
            }
        }

        /// <summary>
        /// 안전하게 객체를 풀에 반환합니다.
        /// </summary>
        public void ReturnToPool()
        {
            if (m_pool != null && gameObject.activeSelf)
            {
                m_pool.Release(this);
            }
            else if (m_pool == null)
            {
                // 풀이 없으면 그냥 파괴 (방어 코드)
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 이펙트의 재생 시간을 계산합니다.
        /// </summary>
        private float GetDuration()
        {
            // 1. 수동 설정 우선
            if (m_overrideDuration > 0f)
            {
                return m_overrideDuration;
            }

            // 2. 파티클 시스템 계산
            if (m_particleSystem != null)
            {
                // 루핑 파티클은 자동 반환 불가 (-1 반환)
                if (m_particleSystem.main.loop) return -1f;

                // Duration(방출 시간) + StartLifetime(입자 생존 시간) 중 가장 긴 것
                float maxLifetime = m_particleSystem.main.startLifetime.constantMax;
                return m_particleSystem.main.duration + maxLifetime;
            }

            // 3. 애니메이터 계산
            if (m_animator != null && m_animator.runtimeAnimatorController != null)
            {
                // 현재 상태의 길이를 가져오기 위해 클립 확인
                // OnEnable 직후에는 GetCurrentAnimatorStateInfo가 갱신되지 않았을 수 있으므로 클립 리스트 검색
                var clips = m_animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    // 첫 번째 클립의 길이를 사용하거나, 별도 로직으로 특정 클립 찾기
                    // 여기서는 가장 긴 클립을 기준으로 함 (안전책)
                    float maxDuration = 0f;
                    foreach (var clip in clips)
                    {
                        if (clip.length > maxDuration) maxDuration = clip.length;
                    }
                    return maxDuration;
                }
            }

            return -1f; // 결정 불가
        }

        #endregion
    }
}