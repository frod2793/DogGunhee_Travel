using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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

        private Dictionary<EffectType, IObjectPool<PooledEffect>> _effectPools;
        private Dictionary<EffectType, GameObject> _effectPrefabs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

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
    }
}
