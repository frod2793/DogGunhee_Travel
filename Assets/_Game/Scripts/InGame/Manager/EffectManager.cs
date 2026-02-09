using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Pool;
using DG.Tweening;
using InGame.vamsir;

namespace InGame
{
    /// <summary>
    /// 게임 내 모든 시각 효과(VFX)를 관리하는 싱글톤 매니저입니다.
    /// 오브젝트 풀링을 사용하여 파티클 시스템과 같은 이펙트를 효율적으로 재사용합니다.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        #region 싱글톤

        private static EffectManager s_instance;

        /// <summary>EffectManager의 전역 싱글톤 인스턴스입니다.</summary>
        public static EffectManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<EffectManager>();
                }
                return s_instance;
            }
        }

        #endregion

        #region 인스펙터 필드

        [Tooltip("이펙트 종류와 프리팹을 매핑해놓은 ScriptableObject 데이터입니다.")]
        [SerializeField] private EffectData m_effectData;

        [Header("카메라 흔들림 효과")]
        [Tooltip("플레이어 피격 시 카메라 흔들림 지속 시간입니다.")]
        [SerializeField] private float m_shakeDuration = 0.2f;
        [Tooltip("플레이어 피격 시 카메라 흔들림 강도입니다.")]
        [SerializeField] private float m_shakeStrength = 0.5f;
        [Tooltip("플레이어 피격 시 카메라 흔들림의 진동수입니다.")]
        [SerializeField] private int m_shakeVibrato = 10;

        #endregion

        #region 내부 필드

        private Dictionary<EffectType, IObjectPool<PooledEffect>> m_effectPools;
        private Dictionary<EffectType, GameObject> m_effectPrefabs;
        private Camera m_mainCamera;
        private Tween m_cameraShakeTween;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            m_mainCamera = Camera.main;

            InitializePools();
        }

        #endregion

        #region 초기화 로직

        /// <summary>
        /// EffectData를 기반으로 각 이펙트 타입에 대한 오브젝트 풀을 생성합니다.
        /// </summary>
        private void InitializePools()
        {
            m_effectPools = new Dictionary<EffectType, IObjectPool<PooledEffect>>();
            m_effectPrefabs = new Dictionary<EffectType, GameObject>();

            if (m_effectData == null)
            {
                LogManager.LogError("EffectData가 EffectManager에 할당되지 않았습니다.", LogManager.LogCategory.EffectManager, this);
                return;
            }

            foreach (var mapping in m_effectData.effects)
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

                m_effectPrefabs[mapping.type] = mapping.prefab;
                m_effectPools[mapping.type] = new ObjectPool<PooledEffect>(
                    createFunc: () => CreateEffect(mapping.type),
                    actionOnGet: OnGetEffect,
                    actionOnRelease: OnReleaseEffect,
                    actionOnDestroy: OnDestroyEffect,
                    maxSize: 20 // 기본 풀 사이즈
                );
            }
        }

        private PooledEffect CreateEffect(EffectType type)
        {
            var prefab = m_effectPrefabs[type];
            var instance = Instantiate(prefab, transform); // EffectManager를 부모로 하여 생성
            var pooledEffect = instance.GetComponent<PooledEffect>();
            pooledEffect.SetPool(m_effectPools[type]);
            return pooledEffect;
        }

        private void OnGetEffect(PooledEffect effect) => effect.gameObject.SetActive(true);
        private void OnReleaseEffect(PooledEffect effect) => effect.gameObject.SetActive(false);
        private void OnDestroyEffect(PooledEffect effect)
        {
            if (effect != null) Destroy(effect.gameObject);
        }

        #endregion

        #region 이펙트 재생 (Pool)

        /// <summary>
        /// 지정된 타입의 이펙트를 특정 위치와 회전으로 재생합니다.
        /// </summary>
        /// <param name="type">재생할 이펙트의 타입</param>
        /// <param name="position">이펙트가 생성될 월드 위치</param>
        /// <param name="rotation">이펙트의 초기 회전값</param>
        public void PlayEffect(EffectType type, Vector3 position, Quaternion rotation)
        {
            if (!m_effectPools.ContainsKey(type))
            {
                LogManager.LogWarning($"'{type}' 타입에 대한 이펙트 풀이 존재하지 않습니다.", LogManager.LogCategory.EffectManager);
                return;
            }

            var effect = m_effectPools[type].Get();
            effect.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// 지정된 타입의 이펙트를 특정 위치에 기본 회전값으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position)
        {
            PlayEffect(type, position, Quaternion.identity);
        }

        #endregion

        #region 카메라 효과 (DOTween)

        /// <summary>
        /// 플레이어 피격 시 카메라 흔들림 효과를 재생합니다.
        /// </summary>
        public void PlayPlayerHitCameraShake()
        {
            if (m_mainCamera == null) return;

            // 이미 흔들림 효과가 진행 중이라면, 초기화 후 새로 시작합니다.
            m_cameraShakeTween?.Kill();

            // 카메라 흔들림 효과를 생성하고 트윈을 저장합니다.
            m_cameraShakeTween = m_mainCamera.DOShakePosition(
                m_shakeDuration,
                m_shakeStrength,
                m_shakeVibrato
            ).SetTarget(m_mainCamera); // 트윈의 생명주기를 카메라에 연결
        }

        #endregion

        #region 트윈 이펙트 (SpriteRenderer)

        /// <summary>
        /// 플레이어 레벨업 또는 스킬 선택 시 성장 이펙트를 재생합니다.
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

        /// <summary>
        /// 대상 SpriteRenderer에 피격 시 붉게 깜빡이는 효과를 즉시 적용합니다.
        /// </summary>
        /// <param name="targetRenderer">효과를 적용할 SpriteRenderer</param>
        /// <param name="flashColor">깜빡일 색상 (기본값: Red)</param>
        public void PlayImmediateFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null)
        {
            if (targetRenderer == null) return;

            Color targetColor = flashColor ?? Color.red;

            // 기존에 진행 중인 색상 관련 트윈을 중지하고, 즉시 흰색으로 리셋 후 새로운 시퀀스 시작
            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            DOTween.Sequence()
                .Append(targetRenderer.DOColor(targetColor, 0.1f))
                .Append(targetRenderer.DOColor(Color.white, 0.1f))
                .SetTarget(targetRenderer.transform);
        }

        /// <summary>
        /// 대상 SpriteRenderer에 피격 시 붉게 깜빡이는 효과를 대기 없이 즉시 실행합니다.
        /// </summary>
        public UniTask PlayQueuedFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null)
        {
            PlayImmediateFlashEffect(targetRenderer, flashColor);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 대상 Transform에 넉백 효과를 적용합니다.
        /// </summary>
        public void PlayKnockbackEffect(Transform targetTransform, Vector3 direction, float distance, float duration)
        {
            if (targetTransform == null) return;

            targetTransform.DOMove(targetTransform.position + direction * distance, duration).SetEase(Ease.OutQuad);
        }

        #endregion
    }
}
