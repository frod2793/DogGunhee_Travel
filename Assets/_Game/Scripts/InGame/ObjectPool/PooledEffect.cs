using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace InGame.vamsir
{
    /// <summary>
    /// 오브젝트 풀링되는 이펙트 프리팹에 부착되는 컴포넌트입니다.
    /// 파티클 시스템이 재생을 마치면 자동으로 자신을 풀에 반환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledEffect : MonoBehaviour
    {
        [SerializeField] private float overrideDuration = -1f;

        private IObjectPool<PooledEffect> _pool;
        private ParticleSystem _particleSystem;
        private Animator _animator;
        
        private CancellationTokenSource _cts;
        private bool _isReleasing = false;

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
            _isReleasing = false;
            StartAutoReturnToPool().Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // 외부 요인(예: 부모 비활성화)으로 인해 비활성화된 경우, 풀로 반환 시도
            // 단, 풀에서 Release를 호출하여 비활성화된 경우(_isReleasing)는 제외 (무한루프/중복반환 방지)
            if (!_isReleasing && _pool != null)
            {
                // 주의: 이미 Destroy된 경우나 앱 종료 시점 등 예외 처리 필요할 수 있음
                // 여기서는 간단하게 처리
                _isReleasing = true;
               try
               {
                    _pool.Release(this);
               }
               catch
               {
                   // 이미 풀에 있거나 파괴된 경우 무시
               }
               _isReleasing = false;
            }
        }

        private async UniTaskVoid StartAutoReturnToPool()
        {
            _cts = new CancellationTokenSource();
            float duration = GetDuration();

            if (duration > 0f)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                
                // 시간이 다 됨 -> 풀 반환
                if (gameObject.activeInHierarchy && !_isReleasing)
                {
                    _isReleasing = true;
                    _pool?.Release(this);
                    _isReleasing = false;
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