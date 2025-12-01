using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace Vamser_like.vamsir
{
    /// <summary>
    /// 오브젝트 풀링되는 이펙트 프리팹에 부착되는 컴포넌트입니다.
    /// 파티클 시스템이 재생을 마치면 자동으로 자신을 풀에 반환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledEffect : MonoBehaviour
    {
        [Tooltip("이 값을 0보다 크게 설정하면, 파티클이나 애니메이션 시간 대신 이 시간(초) 후에 자동으로 풀에 반환됩니다. (스프라이트, 라인 렌더러 이펙트 등에 사용)")]
        [SerializeField] private float overrideDuration = -1f;

        private IObjectPool<PooledEffect> _pool;
        private ParticleSystem _particleSystem;
        private Animator _animator;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _animator = GetComponent<Animator>();
        }

        public void SetPool(IObjectPool<PooledEffect> pool)
        {
            _pool = pool;
        }

        private void OnEnable()
        {
            StartAutoReturnToPool().Forget();
        }

        private async UniTaskVoid StartAutoReturnToPool()
        {
            float duration = GetDuration();

            if (duration > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());
                
                // Delay 후에도 오브젝트가 여전히 활성 상태일 때만 풀에 반환합니다.
                if(gameObject.activeInHierarchy)
                {
                    _pool?.Release(this);
                }
            }
        }

        private float GetDuration()
        {
            // 1. 수동으로 설정한 지속 시간이 최우선입니다.
            if (overrideDuration > 0f)
            {
                return overrideDuration;
            }

            // 2. 파티클 시스템의 최대 지속 시간을 계산합니다.
            if (_particleSystem != null)
            {
                // 루핑 파티클은 자동으로 반환되지 않도록 하여, 수동으로 제어하거나 Override Duration을 사용하도록 유도합니다.
                if (_particleSystem.main.loop) return -1f; 
                return _particleSystem.main.duration + _particleSystem.main.startLifetime.constantMax;
            }

            // 3. 애니메이터의 현재 클립 길이를 가져옵니다.
            if (_animator != null && _animator.runtimeAnimatorController != null && _animator.GetCurrentAnimatorStateInfo(0).length > 0)
            {
                return _animator.GetCurrentAnimatorStateInfo(0).length;
            }

            // 4. 지속 시간을 결정할 수 없는 경우, 자동으로 반환하지 않습니다.
            LogManager.LogWarning($"'{gameObject.name}' 이펙트의 지속 시간을 자동으로 결정할 수 없습니다. 'Override Duration'을 설정하거나 수동으로 풀에 반환해야 합니다.", LogManager.LogCategory.EffectManager, this);
            return -1f;
        }
    }
}