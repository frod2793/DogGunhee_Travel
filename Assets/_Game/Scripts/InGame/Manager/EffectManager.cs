using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using InGame.Effect;
using InGame.Player.Player_Base;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 게임 내 모든 시각 효과(VFX)와 연출(Camera Shake, Flash 등)을 중앙 관리하는 싱글톤 매니저 클래스입니다.
    /// UnityEngine.Pool을 사용하여 파티클 시스템을 효율적으로 재사용하며, DOTween을 사용하여 절차적 애니메이션을 처리합니다.
    /// </summary>
    public class EffectManager : MonoBehaviour, IEffectService
    {
     

        #region 에디터 설정

        [Header("설정 데이터")]
        [Tooltip("이펙트 종류(Enum)와 프리팹(Prefab)을 매핑한 ScriptableObject")]
        [SerializeField] private EffectData m_effectData;

        [Header("카메라 흔들림")]
        [SerializeField, Tooltip("피격 시 흔들림 지속 시간")] private float m_shakeDuration = 0.2f;
        [SerializeField, Tooltip("피격 시 흔들림 강도")] private float m_shakeStrength = 0.5f;
        [SerializeField, Tooltip("피격 시 흔들림 진동수")] private int m_shakeVibrato = 10;

        #endregion

        #region 내부 필드

        private Dictionary<EffectType, IObjectPool<PooledEffect>> m_effectPools;
        private Dictionary<EffectType, GameObject> m_effectPrefabs;

        private Camera m_mainCamera;
        private Transform m_mainCameraTransform;
        private Tween m_cameraShakeTween;
        private PlayerCameraAgent m_cameraAgent;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {

            m_mainCamera = Camera.main;
            if (m_mainCamera != null)
            {
                m_mainCameraTransform = m_mainCamera.transform;
            }

            m_cameraAgent = FindAnyObjectByType<PlayerCameraAgent>();

            InitializePools();
        }

        private void OnDestroy()
        {
            if (m_cameraShakeTween != null && m_cameraShakeTween.IsActive())
            {
                m_cameraShakeTween.Kill();
            }
        }

        #endregion

        #region 초기화 및 풀링

        /// <summary>
        /// [설명]: EffectData를 기반으로 각 이펙트 타입에 대한 오브젝트 풀을 생성합니다.
        /// </summary>
        private void InitializePools()
        {
            m_effectPools = new Dictionary<EffectType, IObjectPool<PooledEffect>>();
            m_effectPrefabs = new Dictionary<EffectType, GameObject>();

            if (m_effectData == null)
            {
                LogManager.LogError("[EffectManager] EffectData가 할당되지 않았습니다.", LogManager.LogCategory.EffectManager);
                return;
            }

            foreach (var mapping in m_effectData.Effects)
            {
                if (mapping.Prefab == null)
                {
                    LogManager.LogWarning($"[EffectManager] '{mapping.Type}' 타입의 프리팹이 누락되었습니다.", LogManager.LogCategory.EffectManager);
                    continue;
                }

                GameObject prefab = mapping.Prefab;
                m_effectPrefabs[mapping.Type] = prefab;

                m_effectPools[mapping.Type] = new ObjectPool<PooledEffect>(
                    createFunc: () => CreateEffect(mapping.Type),
                    actionOnGet: OnGetEffect,
                    actionOnRelease: OnReleaseEffect,
                    actionOnDestroy: OnDestroyEffect,
                    defaultCapacity: 10,
                    maxSize: 50
                );
            }
        }

        /// <summary>
        /// [설명]: 특정 타입의 이펙트 객체를 생성하고 풀링 설정을 수행합니다.
        /// </summary>
        private PooledEffect CreateEffect(EffectType type)
        {
            if (!m_effectPrefabs.TryGetValue(type, out GameObject prefab))
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, transform);
            PooledEffect pooledEffect = instance.GetComponent<PooledEffect>();

            if (pooledEffect == null)
            {
                pooledEffect = instance.AddComponent<PooledEffect>();
            }

            pooledEffect.SetPool(m_effectPools[type]);
            return pooledEffect;
        }

        private void OnGetEffect(PooledEffect effect)
        {
            effect.gameObject.SetActive(true);
        }

        private void OnReleaseEffect(PooledEffect effect)
        {
            effect.gameObject.SetActive(false);
        }

        private void OnDestroyEffect(PooledEffect effect)
        {
            if (effect != null)
            {
                Destroy(effect.gameObject);
            }
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// [설명]: 지정된 타입의 이펙트를 특정 위치와 회전값으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position, Quaternion rotation)
        {
            if (m_effectPools.TryGetValue(type, out var pool))
            {
                PooledEffect effect = pool.Get();
                if (effect != null)
                {
                    effect.transform.SetPositionAndRotation(position, rotation);
                }
            }
            else
            {
                LogManager.LogWarning($"[EffectManager] '{type}' 타입의 풀을 찾을 수 없습니다.", LogManager.LogCategory.EffectManager);
            }
        }

        /// <summary>
        /// [설명]: 지정된 타입의 이펙트를 특정 위치에 기본 회전값(Identity)으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position)
        {
            PlayEffect(type, position, Quaternion.identity);
        }

        /// <summary>
        /// [설명]: 메인 카메라에 흔들림 효과를 적용합니다.
        /// </summary>
        public void PlayPlayerHitCameraShake()
        {
            if (m_mainCameraTransform == null)
            {
                if (Camera.main != null)
                {
                    m_mainCamera = Camera.main;
                    m_mainCameraTransform = m_mainCamera.transform;
                }
                else
                {
                    return;
                }
            }

            if (m_cameraShakeTween != null && m_cameraShakeTween.IsActive())
            {
                m_cameraShakeTween.Kill(complete: true);
            }

            if (m_cameraAgent == null)
            {
                m_cameraAgent = FindAnyObjectByType<PlayerCameraAgent>();
            }

            if (m_cameraAgent != null)
            {
                m_cameraAgent.enabled = false;
            }

            m_cameraShakeTween = m_mainCameraTransform.DOShakePosition(
                m_shakeDuration,
                m_shakeStrength,
                m_shakeVibrato
            ).OnComplete(() =>
            {
                if (m_cameraAgent != null)
                {
                    m_cameraAgent.enabled = true;
                }
            }).SetTarget(m_mainCameraTransform).SetUpdate(true);
        }

        /// <summary>
        /// [설명]: 스프라이트를 노란색으로 점멸시켜 레벨업 효과를 연출합니다.
        /// </summary>
        public void PlayLevelUpEffect(SpriteRenderer targetRenderer)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < 3; i++)
            {
                sequence.Append(targetRenderer.DOColor(Color.yellow, 0.1f).SetEase(Ease.OutQuad))
                        .Append(targetRenderer.DOColor(Color.white, 0.1f).SetEase(Ease.InQuad));
            }

            sequence.SetTarget(targetRenderer.transform).SetUpdate(true);
        }

        /// <summary>
        /// [설명]: 스프라이트를 지정된 색상으로 즉시 깜빡이게 합니다. (피격 효과)
        /// </summary>
        public void PlayImmediateFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null)
        {
            if (targetRenderer == null)
            {
                return;
            }

            Color targetColor = flashColor ?? Color.red;

            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            DOTween.Sequence()
                .Append(targetRenderer.DOColor(targetColor, 0.1f))
                .Append(targetRenderer.DOColor(Color.white, 0.1f))
                .SetTarget(targetRenderer.transform)
                .SetUpdate(true);
        }

        /// <summary>
        /// [설명]: 몬스터 피격 시 짧고 강렬한 점멸 효과를 재생합니다.
        /// </summary>
        public void PlayMobHitEffect(SpriteRenderer targetRenderer)
        {
            if (targetRenderer == null) return;

            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            // 순차적 점멸 (흰색 -> 원래색)
            DOTween.Sequence()
                .Append(targetRenderer.DOColor(new Color(1f, 0.4f, 0.4f, 1f), 0.05f)) // 약간 붉은 빛이 도는 흰색 (강조)
                .Append(targetRenderer.DOColor(Color.white, 0.05f))
                .SetTarget(targetRenderer.transform)
                .SetUpdate(true);
        }

        /// <summary>
        /// [설명]: 비동기 방식의 플래시 이펙트를 재생합니다.
        /// </summary>
        public UniTask PlayQueuedFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null)
        {
            PlayImmediateFlashEffect(targetRenderer, flashColor);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// [설명]: 대상을 밀어내는 넉백 효과를 적용합니다.
        /// </summary>
        public void PlayKnockbackEffect(Transform targetTransform, Vector3 direction, float distance, float duration)
        {
            if (targetTransform == null)
            {
                return;
            }

            Vector3 targetPos = targetTransform.position + direction.normalized * distance;

            targetTransform.DOMove(targetPos, duration)
                           .SetEase(Ease.OutQuad)
                           .SetTarget(targetTransform)
                           .SetUpdate(true);
        }

        #endregion
    }
}