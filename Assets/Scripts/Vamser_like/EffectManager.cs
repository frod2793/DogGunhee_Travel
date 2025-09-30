using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Pool;
using DG.Tweening;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 게임 내 모든 시각 효과(VFX)를 관리하는 싱글톤 매니저입니다.
    /// 오브젝트 풀링을 사용하여 파티클 시스템과 같은 이펙트를 효율적으로 재사용합니다.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        [Tooltip("이펙트 종류와 프리팹을 매핑해놓은 ScriptableObject 데이터입니다.")]
        [SerializeField] private EffectData effectData;

        [Header("카메라 흔들림 효과")]
        [Tooltip("플레이어 피격 시 카메라 흔들림 지속 시간입니다.")]
        [SerializeField] private float shakeDuration = 0.2f;
        [Tooltip("플레이어 피격 시 카메라 흔들림 강도입니다.")]
        [SerializeField] private float shakeStrength = 0.5f;
        [Tooltip("플레이어 피격 시 카메라 흔들림의 진동수입니다.")]
        [SerializeField] private int shakeVibrato = 10;

        private Dictionary<EffectType, IObjectPool<PooledEffect>> _effectPools;
        private Dictionary<EffectType, GameObject> _effectPrefabs;
        private Camera _mainCamera;
        private Tween _cameraShakeTween;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _mainCamera = Camera.main;

            InitializePools();
        }

        /// <summary>
        /// EffectData를 기반으로 각 이펙트 타입에 대한 오브젝트 풀을 생성합니다.
        /// </summary>
        private void InitializePools()
        {
            _effectPools = new Dictionary<EffectType, IObjectPool<PooledEffect>>();
            _effectPrefabs = new Dictionary<EffectType, GameObject>();

            if (effectData == null)
            {
                LogManager.LogError("EffectData가 EffectManager에 할당되지 않았습니다.", LogManager.LogCategory.EffectManager, this);
                return;
            }

            foreach (var mapping in effectData.effects)
            {
                if (mapping.prefab == null)
                {
                    LogManager.LogWarning($"EffectType '{mapping.type}'에 대한 프리팹이 할당되지 않았습니다.", LogManager.LogCategory.EffectManager);
                    continue;
                }

                // 프리팹에 PooledEffect 컴포넌트가 없으면 자동으로 추가합니다.
                if (mapping.prefab.GetComponent<PooledEffect>() == null)
                {
                    mapping.prefab.AddComponent<PooledEffect>();
                    LogManager.LogWarning($"'{mapping.prefab.name}' 프리팹에 PooledEffect 컴포넌트를 추가했습니다. 프리팹을 확인하고 저장해주세요.", LogManager.LogCategory.EffectManager);
                }

                _effectPrefabs[mapping.type] = mapping.prefab;
                _effectPools[mapping.type] = new ObjectPool<PooledEffect>(
                    createFunc: () => CreateEffect(mapping.type),
                    actionOnGet: OnGetEffect,
                    actionOnRelease: OnReleaseEffect,
                    actionOnDestroy: OnDestroyEffect,
                    maxSize: 20 // 기본 풀 사이즈
                );
            }
        }

        /// <summary>
        /// 지정된 타입의 이펙트를 특정 위치와 회전으로 재생합니다.
        /// </summary>
        /// <param name="type">재생할 이펙트의 타입</param>
        /// <param name="position">이펙트가 생성될 월드 위치</param>
        /// <param name="rotation">이펙트의 초기 회전값</param>
        public void PlayEffect(EffectType type, Vector3 position, Quaternion rotation)
        {
            if (!_effectPools.ContainsKey(type))
            {
                LogManager.LogWarning($"'{type}' 타입에 대한 이펙트 풀이 존재하지 않습니다.", LogManager.LogCategory.EffectManager);
                return;
            }

            var effect = _effectPools[type].Get();
            effect.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// 지정된 타입의 이펙트를 특정 위치에 기본 회전값으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position)
        {
            PlayEffect(type, position, Quaternion.identity);
        }

        // 오브젝트 풀 델리게이트 메서드들
        private PooledEffect CreateEffect(EffectType type)
        {
            var prefab = _effectPrefabs[type];
            var instance = Instantiate(prefab, transform); // EffectManager를 부모로 하여 생성
            var pooledEffect = instance.GetComponent<PooledEffect>();
            pooledEffect.SetPool(_effectPools[type]);
            return pooledEffect;
        }

        private void OnGetEffect(PooledEffect effect) => effect.gameObject.SetActive(true);
        private void OnReleaseEffect(PooledEffect effect) => effect.gameObject.SetActive(false);
        private void OnDestroyEffect(PooledEffect effect)
        {
            if (effect != null) Destroy(effect.gameObject);
        }

        /// <summary>
        /// 플레이어 피격 시 카메라 흔들림 효과를 재생합니다.
        /// </summary>
        public void PlayPlayerHitCameraShake()
        {
            if (_mainCamera == null) return;

            // 이미 흔들림 효과가 진행 중이라면, 초기화 후 새로 시작합니다.
            _cameraShakeTween?.Kill();

            // 카메라 흔들림 효과를 생성하고 트윈을 저장합니다.
            _cameraShakeTween = _mainCamera.DOShakePosition(
                shakeDuration,
                shakeStrength,
                shakeVibrato
            ).SetTarget(_mainCamera); // 트윈의 생명주기를 카메라에 연결
        }
        
        /// <summary>
        /// 플레이어 레벨업 또는 스킬 선택 시 성장 이펙트를 재생합니다.
        /// 이펙트는 플레이어의 위치에서 재생됩니다.
        /// </summary>
        /// <param name="targetRenderer">효과를 적용할 플레이어의 SpriteRenderer</param>
        public void PlayLevelUpEffect(SpriteRenderer targetRenderer)
        {
            if (targetRenderer == null) return;
            
            // 기존 색상 트윈을 중지하고, 노란색으로 3번 점멸하는 시퀀스를 실행합니다.
            targetRenderer.DOKill();
            targetRenderer.color = Color.white; // 기본 색상으로 리셋

            var sequence = DOTween.Sequence();
            for (int i = 0; i < 3; i++)
            {
                sequence.Append(targetRenderer.DOColor(Color.yellow, 0.1f).SetEase(Ease.OutQuad))
                        .Append(targetRenderer.DOColor(Color.white, 0.1f).SetEase(Ease.InQuad));
            }
            sequence.SetTarget(targetRenderer.transform);
        }

        // 몹 피격 효과를 위한 큐
        private readonly Queue<(SpriteRenderer renderer, UniTaskCompletionSource completionSource)> _queuedFlashEffects = new();
        private bool _isProcessingQueuedFlashes = false;

        #region 인라인 이펙트 (Inline Effects)

        /// <summary>
        /// 대상 SpriteRenderer에 피격 시 붉게 깜빡이는 효과를 즉시 적용합니다.
        /// (플레이어 피격과 같이 우선순위가 높은 효과에 사용)
        /// </summary>
        /// <param name="targetRenderer">효과를 적용할 SpriteRenderer</param>
        public void PlayImmediateFlashEffect(SpriteRenderer targetRenderer)
        {
            if (targetRenderer == null) return;

            // 기존에 진행 중인 색상 관련 트윈을 중지하고, 즉시 흰색으로 리셋 후 새로운 시퀀스 시작
            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            DOTween.Sequence()
                .Append(targetRenderer.DOColor(Color.red, 0.1f))
                .Append(targetRenderer.DOColor(Color.white, 0.1f))
                .SetTarget(targetRenderer.transform); // 트윈의 생명주기를 대상 오브젝트에 연결
        }

        /// <summary>
        /// 대상 SpriteRenderer에 피격 시 붉게 깜빡이는 효과를 큐에 추가하여 순차적으로 적용합니다.
        /// (몹 피격과 같이 동시 발생 시 순서대로 처리될 수 있는 효과에 사용)
        /// </summary>
        /// <param name="targetRenderer">효과를 적용할 SpriteRenderer</param>
        /// <returns>효과 완료를 기다릴 수 있는 UniTask</returns>
        public UniTask PlayQueuedFlashEffect(SpriteRenderer targetRenderer)
        {
            if (targetRenderer == null) return UniTask.CompletedTask;

            var completionSource = new UniTaskCompletionSource();
            _queuedFlashEffects.Enqueue((targetRenderer, completionSource));

            if (!_isProcessingQueuedFlashes)
            {
                ProcessQueuedFlashesAsync().Forget();
            }

            return completionSource.Task;
        }

        private async UniTaskVoid ProcessQueuedFlashesAsync()
        {
            _isProcessingQueuedFlashes = true;
            while (_queuedFlashEffects.Count > 0)
            {
                var (renderer, completionSource) = _queuedFlashEffects.Dequeue();
                if (renderer == null || renderer.gameObject == null) { completionSource.TrySetResult(); continue; } // 오브젝트가 파괴된 경우 스킵
                PlayImmediateFlashEffect(renderer); // 실제 플래시 효과 재생
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f)); // 플래시 애니메이션 시간만큼 대기
                completionSource.TrySetResult();
            }
            _isProcessingQueuedFlashes = false;
        }

        /// <summary>
        /// 대상 Transform에 넉백 효과를 적용합니다.
        /// </summary>
        /// <param name="targetTransform">효과를 적용할 Transform</param>
        /// <param name="direction">밀려날 방향</param>
        /// <param name="distance">밀려날 거리</param>
        /// <param name="duration">넉백 지속 시간</param>
        public void PlayKnockbackEffect(Transform targetTransform, Vector3 direction, float distance, float duration)
        {
            if (targetTransform == null) return;

            targetTransform.DOMove(targetTransform.position + direction * distance, duration).SetEase(Ease.OutQuad);
        }

        #endregion
    }
}
