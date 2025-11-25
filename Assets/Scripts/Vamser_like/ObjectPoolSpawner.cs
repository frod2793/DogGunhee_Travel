using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 오브젝트 풀 관리 및 스폰 시스템 (최적화됨)
    /// </summary>
    public class ObjectPoolSpawner : MonoBehaviour
    {
        #region 필드 및 속성

        // 외부 의존성
        private PlayerBase m_player;
        private Camera m_mainCamera;

        // [맵 설정] 스폰 범위를 제한할 맵
        [Header("Map Settings")]
        [Tooltip("몹 스폰 범위를 제한할 맵의 SpriteRenderer")]
        [SerializeField] private SpriteRenderer m_mapRange;
        private Bounds m_mapBounds;

        // 오브젝트 풀 (특정 타입)
        public IObjectPool<VamserMobBase> MobObjectPool { get; private set; }
        public IObjectPool<EXP_Obj> ExpObjectPool { get; private set; }
        public IObjectPool<Coin_Obj> CoinObjectPool { get; private set; }

        // 오브젝트 풀 (제네릭 프리팹 - 이펙트 등)
        private readonly Dictionary<GameObject, IObjectPool<GameObject>> m_genericPools = new Dictionary<GameObject, IObjectPool<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> m_instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        // [Header] 몹 설정
        [Header("Mob Settings")]
        [FormerlySerializedAs("initialMobCount")] [SerializeField] private int m_initialMobCount = 20;
        [FormerlySerializedAs("mobsPerWave")] [SerializeField] private int m_mobsPerWave = 20;
        [FormerlySerializedAs("maxPoolSize")] [SerializeField] private int m_maxPoolSize = 100;
        [FormerlySerializedAs("mobPrefabReference")] [SerializeField] private AssetReferenceGameObject m_mobPrefabReference;
        [FormerlySerializedAs("mobParent")] [SerializeField] private Transform m_mobParent;

        public int ActiveMobCount { get; private set; }
        public int CurrentWave { get; private set; }

        // [Header] 아이템 설정
        [Header("Item Settings")]
        [FormerlySerializedAs("expPrefabReference")] [SerializeField] private AssetReferenceGameObject m_expPrefabReference;
        [FormerlySerializedAs("bigExpPrefabReference")] [SerializeField] private AssetReferenceGameObject m_bigExpPrefabReference;
        [FormerlySerializedAs("coinPrefabReference")] [SerializeField] private AssetReferenceGameObject m_coinPrefabReference;
        [FormerlySerializedAs("coinSpawnPercent")] [SerializeField] private float m_coinSpawnPercent = 25f;

        // 내부 상태
        private readonly Dictionary<AssetReferenceGameObject, GameObject> m_loadedPrefabs = new Dictionary<AssetReferenceGameObject, GameObject>();
        private bool m_isSpawningAllowed = true;
        private CancellationTokenSource m_respawnCts;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;

            // 맵 범위 자동 찾기 (인스펙터 할당 안 되었을 시)
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
                // 맵이 없으면 무한대로 설정 (안전장치)
                m_mapBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
                LogManager.LogWarning("[Spawner] Map Range not found. Using default bounds.", LogManager.LogCategory.ObjectPoolSpawner);
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
            // 모든 풀 정리
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
            if (m_player == null)
            {
                LogManager.LogError("[Spawner] Player is null", LogManager.LogCategory.ObjectPoolSpawner);
                return;
            }

            // 리소스 로드
            await LoadAllPrefabsAsync();

            if (!IsAllPrefabsLoaded())
            {
                LogManager.LogError("[Spawner] Failed to load prefabs", LogManager.LogCategory.ObjectPoolSpawner);
                return;
            }

            // 풀 초기화
            InitializePools();

            LogManager.Log("[Spawner] Initialized and starting spawn", LogManager.LogCategory.ObjectPoolSpawner);

            // 게임 시작 시 초기 스폰
            if (PlayStateManager.instance.IsPlaying)
            {
                SpawnInitialMobs();
                CurrentWave = 1;
            }
        }

        private void InitializePools()
        {
            MobObjectPool = new ObjectPool<VamserMobBase>(CreateMob, OnGetMob, OnReleaseMob, OnDestroyObject, maxSize: m_maxPoolSize);
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
        private void OnResume() => m_isSpawningAllowed = true;
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

        /// <summary>
        /// 몹이 줄어들었는지 확인하고 다음 웨이브 예약
        /// </summary>
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
                await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: token);
                
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

        private void SpawnItems(VamserMobBase deadMob)
        {
            if (!m_isSpawningAllowed) return;

            Vector3 pos = deadMob.transform.position;

            // EXP 스폰
            var exp = ExpObjectPool.Get();
            exp.transform.position = pos;

            // Coin 스폰 (확률)
            if (Random.Range(0f, 100f) < m_coinSpawnPercent)
            {
                var coin = CoinObjectPool.Get();
                coin.transform.position = pos;
            }
        }

        #endregion

        #region Pool Callbacks (Mob/Item)

        // -- Create --
        private VamserMobBase CreateMob() => CreatePoolObject<VamserMobBase>(m_mobPrefabReference);
        private EXP_Obj CreateExp()
        {
            bool isBig = CurrentWave >= 5 && Random.value > 0.9f;
            return CreatePoolObject<EXP_Obj>(isBig ? m_bigExpPrefabReference : m_expPrefabReference);
        }
        private Coin_Obj CreateCoin() => CreatePoolObject<Coin_Obj>(m_coinPrefabReference);

        // -- Get --
        private void OnGetMob(VamserMobBase mob)
        {
            OnGetObject(mob);
            
            // [핵심] 카메라 밖이면서 맵 내부인 유효한 위치 선정
            Vector3 spawnPos = GetValidSpawnPosition();
            mob.transform.position = spawnPos;
            
            ActiveMobCount++;
            mob.SetTarget(m_player);
        }

        // -- Release --
        private void OnReleaseMob(VamserMobBase mob)
        {
            OnReleaseObject(mob);
            ActiveMobCount--;
            
            SpawnItems(mob);   
            CheckMobCount();   
        }

        // -- Common --
        private void OnGetObject<T>(T obj) where T : MonoBehaviour => obj.gameObject.SetActive(true);
        private void OnReleaseObject<T>(T obj) where T : MonoBehaviour => obj.gameObject.SetActive(false);
        private void OnDestroyObject<T>(T obj) where T : MonoBehaviour
        {
            if (obj != null) Destroy(obj.gameObject);
        }

        // -- Factory --
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

        #region 유틸리티 (로드 & 위치)

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

        /// <summary>
        /// 카메라 밖이면서 동시에 맵 내부인 유효한 스폰 위치를 반환합니다.
        /// </summary>
        private Vector3 GetValidSpawnPosition()
        {
            if (m_mainCamera == null) return Vector3.zero;

            Vector3 camPos = m_mainCamera.transform.position;
            
            // 카메라의 뷰포트 크기 계산 (높이 = Size * 2, 너비 = 높이 * 비율)
            float camHeight = m_mainCamera.orthographicSize;
            float camWidth = camHeight * m_mainCamera.aspect;

            // 카메라 화면 밖 최소 거리 (화면 대각선 + 여유분)
            // 원형으로 밖을 계산하면 모서리에서 너무 멀어질 수 있으므로, 사각형 밖을 기준으로 잡습니다.
            // 여기서는 간단하게 화면 절반 너비/높이보다 조금 더 먼 곳을 최소 거리로 잡습니다.
            float minSpawnDist = Mathf.Sqrt(camWidth * camWidth + camHeight * camHeight) + 1.5f;
            
            // 최대 검색 거리
            float maxSpawnDist = minSpawnDist + 5.0f; 

            int maxAttempts = 20; // 위치 찾기 시도 횟수 제한

            for (int i = 0; i < maxAttempts; i++)
            {
                // 1. 랜덤 방향과 거리 생성
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minSpawnDist, maxSpawnDist);
                
                // 2. 후보 위치 계산
                Vector3 candidatePos = camPos + (Vector3)(randomDir * distance);
                candidatePos.z = 0;

                // 3. 맵 내부인지 확인 (Bounds.Contains는 3D 기준이므로 Z축 주의)
                // 2D 게임이므로 Bounds의 Z축이 0을 포함하도록 맵이 설정되어 있어야 함.
                // 안전을 위해 Bounds의 z값과 상관없이 x,y만 체크하거나, candidatePos.z를 Bounds.center.z로 맞춤
                if (m_mapBounds.Contains(candidatePos))
                {
                    return candidatePos;
                }
            }

            // 4. 시도 실패 시 (예: 카메라가 맵 구석에 박혀있을 때)
            // 그냥 맵 내부의 랜덤 위치를 반환합니다. (화면에 보일 수도 있지만 스폰 안 되는 것보단 나음)
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

    /// <summary>
    /// 오브젝트 풀을 사용하는 객체가 구현해야 할 인터페이스
    /// </summary>
    public interface IObjectPoolUser
    {
        ObjectPoolSpawner ObjectPoolSpawner { set; }
    }
}