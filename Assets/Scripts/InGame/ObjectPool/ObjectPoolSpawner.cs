using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Manager;
using InGame.Mob.MobBase;
using InGame.Player.Player_Base;
using InGame.vamsir;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace InGame
{
    public class ObjectPoolSpawner : MonoBehaviour
    {
        #region 필드 및 속성

        private PlayerBase m_player;
        private Camera m_mainCamera;

        [Header("Map Settings")]
        [SerializeField] private SpriteRenderer m_mapRange;
        private Bounds m_mapBounds;

        public IObjectPool<MobBase> MobObjectPool { get; private set; }
        public IObjectPool<EXP_Obj> ExpObjectPool { get; private set; }
        public IObjectPool<Coin_Obj> CoinObjectPool { get; private set; }

        private readonly Dictionary<GameObject, IObjectPool<GameObject>> m_genericPools = new Dictionary<GameObject, IObjectPool<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> m_instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        [Header("Mob Settings")]
        [SerializeField] private int m_initialMobCount = 20;
        [SerializeField] private int m_mobsPerWave = 20;
        [SerializeField] private int m_maxPoolSize = 100;
        [SerializeField] private AssetReferenceGameObject m_mobPrefabReference;
        [SerializeField] private Transform m_mobParent;

        public int ActiveMobCount { get; private set; }
        public int CurrentWave { get; private set; }

        [Header("Item Settings")]
        [SerializeField] private AssetReferenceGameObject m_expPrefabReference;
        [SerializeField] private AssetReferenceGameObject m_bigExpPrefabReference;
        [SerializeField] private AssetReferenceGameObject m_coinPrefabReference;
        [SerializeField] private float m_coinSpawnPercent = 25f;

        private readonly Dictionary<AssetReferenceGameObject, GameObject> m_loadedPrefabs = new Dictionary<AssetReferenceGameObject, GameObject>();
        private bool m_isSpawningAllowed = true;
        private CancellationTokenSource m_respawnCts;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;

            if (m_mapRange == null)
            {
                var mapObj = GameObject.FindGameObjectWithTag("Map");
                if (mapObj != null) m_mapRange = mapObj.GetComponent<SpriteRenderer>();
            }

            if (m_mapRange != null)
            {
                m_mapBounds = m_mapRange.bounds;
            }
            else
            {
                m_mapBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            CancelRespawnTask();
        }

        private void OnDestroy()
        {
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

        #region 초기화 및 이벤트

        public async UniTask InitializeAndStartSpawning(PlayerBase player)
        {
            m_player = player;
            if (m_player == null) return;

            await LoadAllPrefabsAsync();

            if (!IsAllPrefabsLoaded()) return;

            InitializePools();

            if (PlayStateManager.instance.IsPlaying)
            {
                SpawnInitialMobs();
                CurrentWave = 1;
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
        }

        #endregion

        #region 게임 상태 핸들러

        private void OnPause() => m_isSpawningAllowed = false;

        private void OnResume()
        {
            m_isSpawningAllowed = true;
            // [수정] 게임 재개 시, 몹이 0마리인 상태였다면 다음 웨이브를 진행하도록 체크
            CheckMobCount();
        }

        private void OnGameOver()
        {
            m_isSpawningAllowed = false;
            CancelRespawnTask();
        }

        private void OnPlayerChanged(PlayerBase newPlayer)
        {
            m_player = newPlayer;
        }

        private void CancelRespawnTask()
        {
            if (m_respawnCts != null)
            {
                m_respawnCts.Cancel();
                m_respawnCts.Dispose();
                m_respawnCts = null;
            }
        }

        #endregion

        #region 몹 스폰 로직

        private void SpawnInitialMobs()
        {
            m_mobsPerWave = m_initialMobCount;
            SpawnMobs(m_mobsPerWave);
        }

        private void SpawnMobs(int count)
        {
            if (!m_isSpawningAllowed) return;

            for (int i = 0; i < count; i++)
            {
                if (ActiveMobCount < m_maxPoolSize)
                {
                    MobObjectPool.Get();
                }
            }
        }

        private void CheckMobCount()
        {
            if (ActiveMobCount <= 0 && m_isSpawningAllowed)
            {
                CancelRespawnTask();
                m_respawnCts = new CancellationTokenSource();
                
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(m_respawnCts.Token, this.GetCancellationTokenOnDestroy());
                RespawnWaveAsync(linkedCts.Token).Forget();
            }
        }

        private async UniTaskVoid RespawnWaveAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(3), ignoreTimeScale: true, cancellationToken: token);
                
                if (!m_isSpawningAllowed) return;

                CurrentWave++;
                m_mobsPerWave = Mathf.Min(m_mobsPerWave + 5, m_maxPoolSize);
                
                LogManager.Log($"[Spawner] Wave {CurrentWave} Start (Count: {m_mobsPerWave})", LogManager.LogCategory.ObjectPoolSpawner);
                
                SpawnMobs(m_mobsPerWave);
            }
            catch (OperationCanceledException) { /* 정상 취소 */ }
        }

        #endregion

        #region 아이템 스폰

        private void SpawnItems(MobBase deadMob)
        {
            if (!m_isSpawningAllowed) return;

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
            
            Vector3 spawnPos = GetValidSpawnPosition();
            mob.transform.position = spawnPos;
            
            ActiveMobCount++;
            mob.SetTarget(m_player);
        }

        private void OnReleaseMob(MobBase mob)
        {
            OnReleaseObject(mob);
            ActiveMobCount--;
            
            SpawnItems(mob);   
            CheckMobCount();   
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

        private Vector3 GetValidSpawnPosition()
        {
            if (m_mainCamera == null) return Vector3.zero;

            Vector3 camPos = m_mainCamera.transform.position;
            
            float camHeight = m_mainCamera.orthographicSize;
            float camWidth = camHeight * m_mainCamera.aspect;

            float minSpawnDist = Mathf.Sqrt(camWidth * camWidth + camHeight * camHeight) + 1.5f;
            float maxSpawnDist = minSpawnDist + 5.0f; 

            int maxAttempts = 20;

            for (int i = 0; i < maxAttempts; i++)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minSpawnDist, maxSpawnDist);
                
                Vector3 candidatePos = camPos + (Vector3)(randomDir * distance);
                candidatePos.z = 0;

                if (m_mapBounds.Contains(candidatePos))
                {
                    return candidatePos;
                }
            }

            return GetRandomPositionInMap();
        }

        private Vector3 GetRandomPositionInMap()
        {
            float x = Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0f);
        }

        #endregion
    }

    public interface IObjectPoolUser
    {
        ObjectPoolSpawner ObjectPoolSpawner { set; }
    }
}