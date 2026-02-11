using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using InGame.Effect;

namespace InGame.Manager
{
    /// <summary>
    /// 게임 내 모든 시각 효과(VFX)와 연출(Camera Shake, Flash 등)을 중앙 관리하는 싱글톤 클래스입니다.
    /// <br/> UnityEngine.Pool을 사용하여 파티클 시스템을 효율적으로 재사용하며, DOTween을 사용하여 절차적 애니메이션을 처리합니다.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        #region 1. 싱글톤 (Singleton)

        private static EffectManager s_instance;

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

        #region 2. 에디터 설정 (Inspector)

        [Header("설정 데이터")] [Tooltip("이펙트 종류(Enum)와 프리팹(Prefab)을 매핑한 ScriptableObject")] [SerializeField]
        private EffectData m_effectData;

        [Header("카메라 흔들림 (Camera Shake)")] [SerializeField, Tooltip("피격 시 흔들림 지속 시간")]
        private float m_shakeDuration = 0.2f;

        [SerializeField, Tooltip("피격 시 흔들림 강도")]
        private float m_shakeStrength = 0.5f;

        [SerializeField, Tooltip("피격 시 흔들림 진동수")]
        private int m_shakeVibrato = 10;

        #endregion

        #region 3. 내부 변수 및 캐시 (Internal Fields)

        // 오브젝트 풀링
        private Dictionary<EffectType, IObjectPool<PooledEffect>> m_effectPools;
        private Dictionary<EffectType, GameObject> m_effectPrefabs;

        // 카메라 참조
        private Camera m_mainCamera;
        private Transform m_mainCameraTransform; // Transform 캐싱
        private Tween m_cameraShakeTween;

        #endregion

        #region 4. 유니티 생명주기 (Lifecycle)

        private void Awake()
        {
            // 싱글톤 초기화
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;

            // 카메라 참조 캐싱
            m_mainCamera = Camera.main;
            if (m_mainCamera != null)
            {
                m_mainCameraTransform = m_mainCamera.transform;
            }

            // 풀 초기화
            InitializePools();
        }

        private void OnDestroy()
        {
            // 트윈 정리
            if (m_cameraShakeTween != null && m_cameraShakeTween.IsActive())
            {
                m_cameraShakeTween.Kill();
            }
        }

        #endregion

        #region 5. 초기화 및 풀링 로직 (Initialization)

        /// <summary>
        /// EffectData를 기반으로 각 이펙트 타입에 대한 오브젝트 풀을 생성합니다.
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
                    LogManager.LogWarning($"[EffectManager] '{mapping.Type}' 타입의 프리팹이 누락되었습니다.",
                        LogManager.LogCategory.EffectManager);
                    continue;
                }

                // 프리팹 유효성 검사 및 등록
                GameObject prefab = mapping.Prefab;
                if (prefab.GetComponent<PooledEffect>() == null)
                {
                    // 런타임에 컴포넌트를 추가하는 것은 위험하므로(저장 안됨), 경고만 출력하고 스킵하거나 런타임 추가를 명시적으로 수행
                    LogManager.LogWarning($"[EffectManager] '{prefab.name}' 프리팹에 PooledEffect 컴포넌트가 없습니다. 런타임에 추가합니다.",
                        LogManager.LogCategory.EffectManager);
                    // 주의: 원본 프리팹을 수정하는 것이 아니라 인스턴스화 시점에 추가해야 함. 여기서는 생성 로직에서 처리.
                }

                m_effectPrefabs[mapping.Type] = prefab;

                // 오브젝트 풀 생성
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

        private PooledEffect CreateEffect(EffectType type)
        {
            if (!m_effectPrefabs.TryGetValue(type, out GameObject prefab)) return null;

            // EffectManager 하위에 생성하여 Hierarchy 정리
            GameObject instance = Instantiate(prefab, transform);

            // 컴포넌트 확보
            PooledEffect pooledEffect = instance.GetComponent<PooledEffect>();
            if (pooledEffect == null)
            {
                pooledEffect = instance.AddComponent<PooledEffect>();
            }

            // 풀 참조 주입 (반환을 위해)
            pooledEffect.SetPool(m_effectPools[type]);
            return pooledEffect;
        }

        // 풀 이벤트 핸들러
        private void OnGetEffect(PooledEffect effect) => effect.gameObject.SetActive(true);
        private void OnReleaseEffect(PooledEffect effect) => effect.gameObject.SetActive(false);

        private void OnDestroyEffect(PooledEffect effect)
        {
            if (effect != null) Destroy(effect.gameObject);
        }

        #endregion

        #region 6. 이펙트 재생 (Public Methods)

        /// <summary>
        /// 지정된 타입의 이펙트를 특정 위치와 회전값으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position, Quaternion rotation)
        {
            if (m_effectPools.TryGetValue(type, out var pool))
            {
                PooledEffect effect = pool.Get();
                if (effect != null)
                {
                    effect.transform.SetPositionAndRotation(position, rotation);
                    // PooledEffect 내부에서 OnEnable 등을 통해 파티클 재생 로직이 수행된다고 가정
                }
            }
            else
            {
                LogManager.LogWarning($"[EffectManager] '{type}' 타입의 풀을 찾을 수 없습니다.",
                    LogManager.LogCategory.EffectManager);
            }
        }

        /// <summary>
        /// 지정된 타입의 이펙트를 특정 위치에 기본 회전값(Identity)으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position)
        {
            PlayEffect(type, position, Quaternion.identity);
        }

        #endregion

        #region 7. 카메라 연출 (Camera Shake)

        /// <summary>
        /// 메인 카메라에 흔들림 효과를 줍니다. (플레이어 피격 등)
        /// </summary>
        public void PlayPlayerHitCameraShake()
        {
            if (m_mainCameraTransform == null) return;

            // 기존 트윈이 있다면 취소하여 부자연스러운 움직임 방지
            if (m_cameraShakeTween != null && m_cameraShakeTween.IsActive())
            {
                m_cameraShakeTween.Kill(complete: true); // complete: true로 하여 원래 위치 근처로 복귀 유도
            }

            // DOTween은 Transform을 대상으로 함
            m_cameraShakeTween = m_mainCameraTransform.DOShakePosition(
                m_shakeDuration,
                m_shakeStrength,
                m_shakeVibrato
            ).SetTarget(m_mainCameraTransform); // 안전한 파괴를 위해 타겟 설정
        }

        #endregion

        #region 8. 유닛 연출 (Sprite & Transform)

        /// <summary>
        /// 스프라이트를 노란색으로 점멸시켜 레벨업 효과를 연출합니다.
        /// </summary>
        public void PlayLevelUpEffect(SpriteRenderer targetRenderer)
        {
            if (targetRenderer == null) return;

            // 기존 트윈 중지 및 초기화
            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            // 시퀀스 생성: 노랑 -> 흰색 (3회 반복)
            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < 3; i++)
            {
                sequence.Append(targetRenderer.DOColor(Color.yellow, 0.1f).SetEase(Ease.OutQuad))
                    .Append(targetRenderer.DOColor(Color.white, 0.1f).SetEase(Ease.InQuad));
            }

            // 타겟이 파괴되면 시퀀스도 자동 중단되도록 설정
            sequence.SetTarget(targetRenderer.transform);
        }

        /// <summary>
        /// 스프라이트를 지정된 색상(기본: 빨강)으로 즉시 깜빡이게 합니다. (피격 효과)
        /// </summary>
        public void PlayImmediateFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null)
        {
            if (targetRenderer == null) return;

            Color targetColor = flashColor ?? Color.red;

            targetRenderer.DOKill();
            targetRenderer.color = Color.white;

            DOTween.Sequence()
                .Append(targetRenderer.DOColor(targetColor, 0.1f))
                .Append(targetRenderer.DOColor(Color.white, 0.1f))
                .SetTarget(targetRenderer.transform); // Transform을 타겟으로 잡는 것이 일반적
        }

        /// <summary>
        /// 비동기 방식의 플래시 이펙트 메서드입니다. (현재는 즉시 실행과 동일)
        /// </summary>
        public UniTask PlayQueuedFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null)
        {
            PlayImmediateFlashEffect(targetRenderer, flashColor);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 대상을 밀어내는 넉백 효과를 적용합니다.
        /// <br/> 주의: Rigidbody를 사용하는 오브젝트의 경우 물리 연산과 충돌할 수 있습니다.
        /// </summary>
        public void PlayKnockbackEffect(Transform targetTransform, Vector3 direction, float distance, float duration)
        {
            if (targetTransform == null) return;

            // 현재 위치에서 방향 벡터만큼 이동
            Vector3 targetPos = targetTransform.position + direction.normalized * distance;

            targetTransform.DOMove(targetPos, duration)
                .SetEase(Ease.OutQuad)
                .SetTarget(targetTransform);
        }

        #endregion
    }
}