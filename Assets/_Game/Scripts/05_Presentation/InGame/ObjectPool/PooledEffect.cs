using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace InGame.Effect
{
    /// <summary>
    /// [설명]: 오브젝트 풀링되는 이펙트(파티클, 애니메이션) 프리팹에 부착되는 컴포넌트입니다.
    /// 재생이 완료되면 자동으로 자신을 풀에 반환하는 역할을 수행합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledEffect : MonoBehaviour
    {
        #region 에디터 설정

        [Header("설정")]
        [SerializeField, Tooltip("자동 반환 시간을 강제로 설정합니다. (0보다 크면 우선 적용)")]
        private float m_overrideDuration = -1f;

        #endregion

        #region 내부 필드

        /// <summary> 자신을 관리하는 오브젝트 풀 참조 </summary>
        private IObjectPool<PooledEffect> m_pool;

        /// <summary> 파티클 시스템 컴포넌트 캐시 </summary>
        private ParticleSystem m_particleSystem;

        /// <summary> 애니메이터 컴포넌트 캐시 </summary>
        private Animator m_animator;

        /// <summary> 비동기 대기 루틴 취소 토큰 </summary>
        private CancellationTokenSource m_cts;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 관련 컴포넌트를 캐싱합니다.
        /// </summary>
        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
            m_animator = GetComponent<Animator>();
        }

        /// <summary>
        /// [설명]: 이펙트가 활성화될 때마다 자동으로 풀에 반환되는 타이머를 시작합니다.
        /// </summary>
        private void OnEnable()
        {
            StartAutoReturnToPoolAsync().Forget();
        }

        /// <summary>
        /// [설명]: 비활성화 시 진행 중인 대기 작업을 취소하고 리소스를 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
                m_cts = null;
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 이 객체를 관리하는 오브젝트 풀을 주입받습니다.
        /// </summary>
        /// <param name="pool">할당할 오브젝트 풀</param>
        public void SetPool(IObjectPool<PooledEffect> pool)
        {
            m_pool = pool;
        }

        #endregion

        #region 반환 처리 로직

        /// <summary>
        /// [설명]: 이펙트의 재생 시간에 맞춰 일정 시간 대기 후 풀로 반환하는 비동기 루틴입니다.
        /// </summary>
        private async UniTaskVoid StartAutoReturnToPoolAsync()
        {
            // 기존 토큰 정리
            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
            }
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
                    // 비활성화되어 취소됨
                }
            }
            else if (duration == -1f)
            {
                // 루핑 이펙트 등의 경우 수동 반환이 필요할 수 있음
            }
        }

        /// <summary>
        /// [설명]: 활성화된 상태라면 안전하게 오브젝트 풀로 반납합니다.
        /// </summary>
        public void ReturnToPool()
        {
            if (m_pool != null && gameObject.activeSelf)
            {
                m_pool.Release(this);
            }
            else if (m_pool == null)
            {
                // 풀 정보가 없으면 일반 파괴 처리
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// [설명]: 파티클 시스템이나 애니메이터 설정을 읽어 이펙트의 실제 재생 시간을 산출합니다.
        /// </summary>
        /// <returns>산출된 지속 시간 (초), 결정 불가 시 -1</returns>
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
                if (m_particleSystem.main.loop)
                {
                    return -1f;
                }

                float maxLifetime = m_particleSystem.main.startLifetime.constantMax;
                return m_particleSystem.main.duration + maxLifetime;
            }

            // 3. 애니메이터 계산
            if (m_animator != null && m_animator.runtimeAnimatorController != null)
            {
                var clips = m_animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    float maxDuration = 0f;
                    foreach (var clip in clips)
                    {
                        if (clip.length > maxDuration)
                        {
                            maxDuration = clip.length;
                        }
                    }
                    return maxDuration;
                }
            }

            return -1f;
        }

        #endregion
    }
}