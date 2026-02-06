using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Manager;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using InGame.Player.Player_Base;
using InGame.vamsir;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace InGame
{
    /// <summary>
    /// 오브젝트 풀 관리 및 스폰 시스템을 담당하는 MonoBehaviour 컴포넌트입니다.
    /// 실제 로직은 WaveSystem과 SpawnPositionSolver POCO 클래스에 위임합니다.
    /// </summary>
    public class ObjectPoolSpawner : MonoBehaviour
    {
        #region 필드 및 속성

        private PlayerBase m_player;
        private Camera m_mainCamera;

        [Header("Map Settings")]
        [SerializeField] private SpriteRenderer m_mapRange;
        private Bounds m_mapBounds;

        // 오브젝트 풀
        public IObjectPool<MobBase> MobObjectPool { get; private set; }
        public IObjectPool<EXP_Obj> ExpObjectPool { get; private set; }
        public IObjectPool<Coin_Obj> CoinObjectPool { get; private set; }

        private readonly Dictionary<GameObject, IObjectPool<GameObject>> m_genericPools = new Dictionary<GameObject, IObjectPool<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> m_instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        [Header("Mob Settings")]
        [SerializeField] private int m_initialMobCount = 20;
        [SerializeField] private int m_mobIncreasePerWave = 5;
        [SerializeField] private int m_maxPoolSize = 100;
        [SerializeField] private float m_waveDelay = 3f;
        [SerializeField] private AssetReferenceGameObject m_mobPrefabReference;
        [SerializeField] private Transform m_mobParent;

        // POCO 시스템 (로직 분리)
        private WaveSystem m_waveSystem;
        private SpawnPositionSolver m_positionSolver;

        /// <summary>
        /// 현재 활성 몹 수 (WaveSystem에서 관리)
        /// </summary>
        public int ActiveMobCount => m_waveSystem?.ActiveMobCount ?? 0;

        /// <summary>
        /// 현재 웨이브 번호 (WaveSystem에서 관리)
        /// </summary>
        public int CurrentWave => m_waveSystem?.CurrentWave ?? 0;

        [Header("Item Settings")]
        [SerializeField] private AssetReferenceGameObject m_expPrefabReference;
        [SerializeField] private AssetReferenceGameObject m_bigExpPrefabReference;
        [SerializeField] private AssetReferenceGameObject m_coinPrefabReference;
        [SerializeField] private float m_coinSpawnPercent = 25f;

        private readonly Dictionary<AssetReferenceGameObject, GameObject> m_loadedPrefabs = new Dictionary<AssetReferenceGameObject, GameObject>();

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;
            InitializeMapBounds();
            InitializePOCOSystems();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            m_waveSystem?.Pause();
        }

        private void OnDestroy()
        {
            m_waveSystem?.Dispose();

            MobObjectPool?.Clear();
            ExpObjectPool?.Clear();
            CoinObjectPool?.Clear();

            foreach (var pool in m_genericPools.Values)
            {
                pool.Clear();
            }
            m_genericPools.Clear();
            m_instanceToPrefabMap.Clear();
        }

        #endregion

        #region 초기화

        private void InitializeMapBounds()
        {
            if (m_mapRange == null)
            {
                var mapObj = GameObject.FindGameObjectWithTag("Map");
                if (mapObj != null) m_mapRange = mapObj.GetComponent<SpriteRenderer>();
            }

            m_mapBounds = m_mapRange != null
                ? m_mapRange.bounds
                : new Bounds(Vector3.zero, Vector3.one * 1000f);
        }

        private void InitializePOCOSystems()
        {
            // 위치 계산 POCO 초기화
            m_positionSolver = new SpawnPositionSolver(
                mapBounds: m_mapBounds,
                minSpawnDistance: 2f,
                maxSpawnDistance: 10f,
                maxAttempts: 30
            );

            // 웨이브 시스템 POCO 초기화
            m_waveSystem = new WaveSystem(
                initialMobCount: m_initialMobCount,
                mobIncreasePerWave: m_mobIncreasePerWave,
                maxMobCount: m_maxPoolSize,
                waveDelay: m_waveDelay
            );

            // 웨이브 이벤트 구독
            m_waveSystem.OnWaveStarted += OnWaveStarted;
        }

        public async UniTask InitializeAndStartSpawning(PlayerBase player)
        {
            m_player = player;
            if (m_player == null) return;

            await LoadAllPrefabsAsync();

            if (!IsAllPrefabsLoaded()) return;

            InitializePools();

            if (PlayStateManager.instance.IsPlaying)
            {
                m_waveSystem.Start();
            }
        }

        private void InitializePools()
        {
            MobObjectPool = new ObjectPool<MobBase>(CreateMob, OnGetMob, OnReleaseMob, OnDestroyObject, maxSize: m_maxPoolSize);
            ExpObjectPool = new ObjectPool<EXP_Obj>(CreateExp, OnGetObject, OnReleaseObject, OnDestroyObject, maxSize: m_maxPoolSize);
            CoinObjectPool = new LinkedPool<Coin_Obj>(CreateCoin, OnGetObject, OnReleaseObject, OnDestroyObject, maxSize: m_maxPoolSize);
        }

        private void SubscribeEvents()
        {
            GameManager.OnPlayerChanged += OnPlayerChanged;
            PlayStateManager.OnGamePause += OnPause;
            PlayStateManager.OnGameResume += OnResume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        private void UnsubscribeEvents()
        {
            GameManager.OnPlayerChanged -= OnPlayerChanged;
            PlayStateManager.OnGamePause -= OnPause;
            PlayStateManager.OnGameResume -= OnResume;
            PlayStateManager.OnGameOver -= OnGameOver;

            if (m_waveSystem != null)
            {
                m_waveSystem.OnWaveStarted -= OnWaveStarted;
            }
        }

        #endregion

        #region 게임 상태 핸들러

        private void OnPause() => m_waveSystem?.Pause();

        private void OnResume() => m_waveSystem?.Resume();

        private void OnGameOver() => m_waveSystem?.Stop();

        private void OnPlayerChanged(PlayerBase newPlayer)
        {
            m_player = newPlayer;
        }

        #endregion

        #region 웨이브 이벤트 핸들러

        private void OnWaveStarted(int spawnCount)
        {
            SpawnMobs(spawnCount);
        }

        private void SpawnMobs(int count)
        {
            if (m_waveSystem == null || !m_waveSystem.IsSpawningAllowed) return;

            for (int i = 0; i < count; i++)
            {
                if (m_waveSystem.CanSpawn())
                {
                    MobObjectPool.Get();
                }
            }
        }

        #endregion

        #region 아이템 스폰

        private void SpawnItems(MobBase deadMob)
        {
            if (m_waveSystem == null || !m_waveSystem.IsSpawningAllowed) return;

            Vector3 pos = deadMob.transform.position;

            var exp = ExpObjectPool.Get();
            exp.transform.position = pos;

            if (Random.Range(0f, 100f) < m_coinSpawnPercent)
            {
                var coin = CoinObjectPool.Get();
                coin.transform.position = pos;
            }
        }

        #endregion

        #region Pool Callbacks

        private MobBase CreateMob() => CreatePoolObject<MobBase>(m_mobPrefabReference);

        private EXP_Obj CreateExp()
        {
            bool isBig = CurrentWave >= 5 && Random.value > 0.9f;
            return CreatePoolObject<EXP_Obj>(isBig ? m_bigExpPrefabReference : m_expPrefabReference);
        }

        private Coin_Obj CreateCoin() => CreatePoolObject<Coin_Obj>(m_coinPrefabReference);

        private void OnGetMob(MobBase mob)
        {
            OnGetObject(mob);

            // POCO를 통한 위치 계산
            Vector3 spawnPos = m_positionSolver.CalculateSpawnPosition(m_mainCamera);
            mob.transform.position = spawnPos;

            m_waveSystem?.OnMobSpawned();
            mob.SetTarget(m_player);
        }

        private void OnReleaseMob(MobBase mob)
        {
            OnReleaseObject(mob);
            m_waveSystem?.OnMobDied();

            SpawnItems(mob);
        }

        private void OnGetObject<T>(T obj) where T : MonoBehaviour => obj.gameObject.SetActive(true);
        private void OnReleaseObject<T>(T obj) where T : MonoBehaviour => obj.gameObject.SetActive(false);
        private void OnDestroyObject<T>(T obj) where T : MonoBehaviour
        {
            if (obj != null) Destroy(obj.gameObject);
        }

        private T CreatePoolObject<T>(AssetReferenceGameObject refObj) where T : MonoBehaviour
        {
            if (!m_loadedPrefabs.TryGetValue(refObj, out var prefab) || prefab == null) return null;

            var obj = Instantiate(prefab, m_mobParent).GetComponent<T>();
            if (obj is IObjectPoolUser poolUser)
            {
                poolUser.ObjectPoolSpawner = this;
            }
            return obj;
        }

        #endregion

        #region 제네릭 오브젝트 풀링

        public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!m_genericPools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<GameObject>(
                    createFunc: () => Instantiate(prefab),
                    actionOnGet: (obj) => obj.SetActive(true),
                    actionOnRelease: (obj) => obj.SetActive(false),
                    actionOnDestroy: (obj) => Destroy(obj),
                    maxSize: 50
                );
                m_genericPools[prefab] = pool;
            }

            var instance = pool.Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = prefab.transform.localScale;

            m_instanceToPrefabMap[instance] = prefab;
            return instance;
        }

        public void ReturnObject(GameObject instance)
        {
            if (instance == null) return;

            if (m_instanceToPrefabMap.TryGetValue(instance, out var prefab) &&
                m_genericPools.TryGetValue(prefab, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        #endregion

        #region 유틸리티

        private async UniTask LoadAllPrefabsAsync()
        {
            var tasks = new List<UniTask>
            {
                LoadAssetAsync(m_mobPrefabReference),
                LoadAssetAsync(m_expPrefabReference),
                LoadAssetAsync(m_bigExpPrefabReference),
                LoadAssetAsync(m_coinPrefabReference)
            };
            await UniTask.WhenAll(tasks);
        }

        private async UniTask LoadAssetAsync(AssetReferenceGameObject reference)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return;
            if (m_loadedPrefabs.ContainsKey(reference)) return;

            try
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(reference);
                var result = await handle.ToUniTask();
                if (result != null)
                {
                    m_loadedPrefabs[reference] = result;
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[Spawner] Asset Load Failed: {reference.AssetGUID} / {e.Message}", LogManager.LogCategory.ObjectPoolSpawner);
            }
        }

        private bool IsAllPrefabsLoaded()
        {
            return m_loadedPrefabs.ContainsKey(m_mobPrefabReference) &&
                   m_loadedPrefabs.ContainsKey(m_expPrefabReference) &&
                   m_loadedPrefabs.ContainsKey(m_coinPrefabReference);
        }

        #endregion
    }

    public interface IObjectPoolUser
    {
        ObjectPoolSpawner ObjectPoolSpawner { set; }
    }
}