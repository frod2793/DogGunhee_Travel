using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Manager;
using InGame.Mob.MobBase;
using InGame.Player.Player_Base;
using InGame.vamsir; // 아이템 관련 네임스페이스 (EXP_Obj, Coin_Obj 등)
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace InGame.ObjectPool
{
    /// <summary>
    /// 게임 내 모든 동적 오브젝트(몬스터, 아이템, 이펙트 등)의 생성과 재사용을 관리하는 스포너입니다.
    /// <br/> 실제 웨이브 진행 로직은 WaveSystem에 위임하고, 위치 계산은 SpawnPositionSolver를 사용합니다.
    /// </summary>
    public class ObjectPoolSpawner : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("맵 및 위치 설정")] [SerializeField, Tooltip("몬스터 스폰 영역을 제한하는 맵 스프라이트")]
        private SpriteRenderer m_mapRange;

        [SerializeField, Tooltip("스폰된 몬스터들이 배치될 부모 트랜스폼")]
        private Transform m_mobParent;

        [Header("데이터 및 풀 설정")] [SerializeField, Tooltip("스테이지/웨이브 데이터베이스")]
        private StageDatabase m_stageDatabase;

        [SerializeField, Tooltip("풀 최대 사이즈 (초과 시 파괴)")]
        private int m_maxPoolSize = 100;

        [SerializeField, Tooltip("일반 경험치 아이템")]
        private AssetReferenceGameObject m_expPrefabReference;

        [SerializeField, Tooltip("코인 아이템")] private AssetReferenceGameObject m_coinPrefabReference;

        [SerializeField, Tooltip("코인 드랍 확률 (0~100)")]
        private float m_coinSpawnPercent = 25f;

        #endregion

        #region 2. 내부 상태 및 시스템 (State & Systems)

        // 외부 참조
        private PlayerBase m_player;
        private Camera m_mainCamera;
        private Bounds m_mapBounds;

        // POCO 시스템 (로직 위임)
        private WaveSystem m_waveSystem;
        private SpawnPositionSolver m_positionSolver;

        // 오브젝트 풀 (Object Pools)
        // 몬스터 풀: Key = MobKey (string)
        private Dictionary<string, IObjectPool<MobBase>> m_mobPools = new Dictionary<string, IObjectPool<MobBase>>();

        // 프리팹 매핑 캐시
        private Dictionary<string, GameObject> m_mobPrefabMap = new Dictionary<string, GameObject>();

        private Dictionary<AssetReferenceGameObject, GameObject> m_loadedItemPrefabs =
            new Dictionary<AssetReferenceGameObject, GameObject>();

        // 비동기 작업 제어
        private CancellationTokenSource m_spawnCts;

        #endregion

        #region 3. 공개 프로퍼티 (Properties)

        /// <summary>경험치 아이템 풀 (외부 접근용)</summary>
        public IObjectPool<EXP_Obj> ExpObjectPool { get; private set; }

        /// <summary>코인 아이템 풀 (외부 접근용)</summary>
        public IObjectPool<Coin_Obj> CoinObjectPool { get; private set; }

        /// <summary>현재 활성화된 몬스터 수 (WaveSystem 위임)</summary>
        public int ActiveMobCount => m_waveSystem?.ActiveMobCount ?? 0;

        /// <summary>현재 진행 중인 웨이브 ID</summary>
        public int CurrentWave => m_waveSystem?.CurrentWaveId ?? 0;

        /// <summary>현재 진행 중인 스테이지 ID</summary>
        public int CurrentStage => m_waveSystem?.CurrentStageId ?? 0;

        #endregion

        #region 4. 이벤트 (Events)

        public event Action<WaveData> OnWaveStarted;
        public event Action<WaveData> OnWaveCompleted;
        public event Action<int> OnStageCleared;

        #endregion

        #region 5. 유니티 생명주기 (Lifecycle)

        private void Awake()
        {
            m_mainCamera = Camera.main;

            InitializeMapBounds();
            InitializeSystems();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            // 웨이브 일시정지 및 스폰 중단
            m_waveSystem?.Pause();

            if (m_spawnCts != null)
            {
                m_spawnCts.Cancel();
                m_spawnCts.Dispose();
                m_spawnCts = null;
            }
        }

        private void OnDestroy()
        {
            m_waveSystem?.Dispose();

            // 모든 풀 정리 (메모리 해제)
            foreach (var pool in m_mobPools.Values) pool.Clear();
            ExpObjectPool?.Clear();
            CoinObjectPool?.Clear();

            // Addressables 해제
            foreach (var prefab in m_mobPrefabMap.Values) Addressables.Release(prefab);
            foreach (var prefab in m_loadedItemPrefabs.Values) Addressables.Release(prefab);

            m_mobPrefabMap.Clear();
            m_loadedItemPrefabs.Clear();
        }

        #endregion

        #region 6. 초기화 및 설정 (Initialization)

        private void InitializeMapBounds()
        {
            if (m_mapRange == null)
            {
                var mapObj = GameObject.FindGameObjectWithTag("Map");
                if (mapObj != null) m_mapRange = mapObj.GetComponent<SpriteRenderer>();
            }

            // 맵이 없으면 임의의 큰 영역 설정
            m_mapBounds = m_mapRange != null
                ? m_mapRange.bounds
                : new Bounds(Vector3.zero, Vector3.one * 1000f);
        }

        private void InitializeSystems()
        {
            // 위치 계산기 초기화
            m_positionSolver = new SpawnPositionSolver(
                mapBounds: m_mapBounds,
                minSpawnDistance: 12f, // 카메라 밖에서 생성되도록 거리 조정 권장
                maxSpawnDistance: 20f,
                maxAttempts: 10
            );

            // 웨이브 시스템 초기화
            m_waveSystem = new WaveSystem();
            m_waveSystem.OnWaveStarted += HandleWaveStarted;
            m_waveSystem.OnWaveCompleted += HandleWaveCompleted;
            m_waveSystem.OnStageCleared += HandleStageCleared;
        }

        /// <summary>
        /// 스포너를 초기화하고 첫 웨이브를 시작합니다.
        /// </summary>
        public async UniTask InitializeAndStartSpawning(PlayerBase player, int startStageId = 1)
        {
            m_player = player;
            if (m_player == null) return;

            // 1. 아이템 프리팹 로드 및 풀 생성
            await LoadItemPrefabsAsync();
            InitializeItemPools();

            // 2. 스테이지 데이터 로드
            if (m_stageDatabase == null)
            {
                Debug.LogError("[ObjectPoolSpawner] StageDatabase 미할당");
                return;
            }

            StageData stage = m_stageDatabase.GetStage(startStageId);
            if (stage == null)
            {
                Debug.LogError($"[ObjectPoolSpawner] Stage {startStageId} 데이터 없음");
                return;
            }

            // 3. 웨이브 시작
            m_waveSystem.Start(stage);
        }

        private void InitializeItemPools()
        {
            // 경험치 풀
            ExpObjectPool = new ObjectPool<EXP_Obj>(
                createFunc: () => CreateItem<EXP_Obj>(m_expPrefabReference),
                actionOnGet: OnGetItem,
                actionOnRelease: OnReleaseItem,
                actionOnDestroy: OnDestroyObject,
                defaultCapacity: 50,
                maxSize: m_maxPoolSize
            );

            // 코인 풀
            CoinObjectPool = new LinkedPool<Coin_Obj>(
                createFunc: () => CreateItem<Coin_Obj>(m_coinPrefabReference),
                actionOnGet: OnGetItem,
                actionOnRelease: OnReleaseItem,
                actionOnDestroy: OnDestroyObject,
                maxSize: m_maxPoolSize
            );
        }

        #endregion

        #region 7. 스폰 로직 (Spawning Logic)

        // --- 웨이브 몬스터 스폰 ---

        private async UniTaskVoid SpawnWaveMobsAsync(WaveData wave)
        {
            // 1. 필요한 몬스터 프리팹 미리 로드
            foreach (var mobInfo in wave.mobs)
            {
                await EnsureMobPrefabLoaded(mobInfo.mobKey);
            }

            // 스폰용 취소 토큰 생성
            m_spawnCts = new CancellationTokenSource();
            var token = CancellationTokenSource
                .CreateLinkedTokenSource(m_spawnCts.Token, this.GetCancellationTokenOnDestroy()).Token;

            float timer = 0f;

            // 2. 스폰 루프
            while (m_waveSystem.IsSpawningAllowed && m_waveSystem.CurrentWaveId == wave.waveId)
            {
                if (token.IsCancellationRequested) break;

                timer += Time.deltaTime;
                if (timer >= wave.spawnInterval)
                {
                    timer = 0f;

                    // 최대 수 제한 체크
                    if (m_waveSystem.CanSpawn(m_maxPoolSize))
                    {
                        string targetKey = GetRandomMobKey(wave.mobs);
                        SpawnMob(targetKey);
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private string GetRandomMobKey(List<MobSpawnData> mobs)
        {
            if (mobs == null || mobs.Count == 0) return string.Empty;

            int totalRate = 0;
            foreach (var m in mobs) totalRate += m.spawnRate;

            int randomPoint = Random.Range(0, totalRate);
            int currentRate = 0;

            foreach (var m in mobs)
            {
                currentRate += m.spawnRate;
                if (randomPoint < currentRate) return m.mobKey;
            }

            return mobs[0].mobKey;
        }

        // --- 아이템 스폰 ---

        private void SpawnItems(Vector3 position)
        {
            // 경험치 생성 (필수)
            if (ExpObjectPool != null)
            {
                var exp = ExpObjectPool.Get();
                if (exp != null) exp.transform.position = position;
            }

            // 코인 생성 (확률)
            if (Random.Range(0f, 100f) < m_coinSpawnPercent && CoinObjectPool != null)
            {
                var coin = CoinObjectPool.Get();
                if (coin != null) coin.transform.position = position;
            }
        }

        #endregion

        #region 8. 오브젝트 풀 관리 (Pool Management)

        // --- 몬스터 풀 ---

        private void SpawnMob(string mobKey)
        {
            if (string.IsNullOrEmpty(mobKey)) return;

            // 풀이 없으면 생성
            if (!m_mobPools.TryGetValue(mobKey, out var pool))
            {
                pool = new ObjectPool<MobBase>(
                    createFunc: () => CreateMobInstance(mobKey),
                    actionOnGet: OnGetMob,
                    actionOnRelease: OnReleaseMob,
                    actionOnDestroy: OnDestroyObject,
                    defaultCapacity: 10,
                    maxSize: m_maxPoolSize
                );
                m_mobPools[mobKey] = pool;
            }

            pool.Get();
        }

        private MobBase CreateMobInstance(string mobKey)
        {
            if (!m_mobPrefabMap.TryGetValue(mobKey, out var prefab)) return null;

            var obj = Instantiate(prefab, m_mobParent).GetComponent<MobBase>();
            if (obj == null) return null;

            // IObjectPoolUser 인터페이스 주입
            if (obj is IObjectPoolUser poolUser)
            {
                poolUser.ObjectPoolSpawner = this;
            }

            obj.gameObject.name = mobKey; // 풀링 키로 이름 설정
            return obj;
        }

        private void OnGetMob(MobBase mob)
        {
            mob.gameObject.SetActive(true);

            // 위치 계산 및 배치
            Vector3 spawnPos =
                m_positionSolver.CalculateSpawnPosition(m_mainCamera != null ? m_mainCamera : Camera.main);
            mob.transform.position = spawnPos;

            // 상태 초기화
            mob.SetTarget(m_player);

            // 시스템 알림
            m_waveSystem?.OnMobSpawned();
        }

        private void OnReleaseMob(MobBase mob)
        {
            if (!mob.gameObject.activeSelf) return; // 이미 비활성화된 경우 스킵

            mob.gameObject.SetActive(false);

            // 시스템 알림
            m_waveSystem?.OnMobDied();

            // 아이템 드랍
            SpawnItems(mob.transform.position);
        }

        /// <summary>
        /// 몬스터를 풀에 반납합니다. (MobBase.OnDie에서 호출)
        /// </summary>
        public void ReturnMob(MobBase mob)
        {
            if (mob == null) return;

            string key = mob.gameObject.name;
            if (m_mobPools.TryGetValue(key, out var pool))
            {
                pool.Release(mob);
            }
            else
            {
                Destroy(mob.gameObject);
            }
        }

        /// <summary>
        /// 아이템을 적절한 풀에 반납합니다.
        /// </summary>
        public void ReturnItem(DropItemBase item)
        {
            if (item == null) return;

            if (item is EXP_Obj exp && ExpObjectPool != null)
            {
                ExpObjectPool.Release(exp);
            }
            else if (item is Coin_Obj coin && CoinObjectPool != null)
            {
                CoinObjectPool.Release(coin);
            }
            else
            {
                Destroy(item.gameObject);
            }
        }

        // --- 아이템 풀 핸들러 ---
        private void OnGetItem<T>(T item) where T : MonoBehaviour => item.gameObject.SetActive(true);
        private void OnReleaseItem<T>(T item) where T : MonoBehaviour => item.gameObject.SetActive(false);

        private void OnDestroyObject<T>(T obj) where T : MonoBehaviour
        {
            if (obj != null) Destroy(obj.gameObject);
        }

        #endregion

        #region 9. 이벤트 핸들러 (Events)

        private void SubscribeEvents()
        {
            GameManager.OnPlayerChanged += OnPlayerChanged;
            if (GameManager.Instance.State == null) return;
            GameManager.Instance.State.OnGamePause += OnPause;
            GameManager.Instance.State.OnGameResume += OnResume;
            GameManager.Instance.State.OnGameOver += OnGameOver;
        }

        private void UnsubscribeEvents()
        {
            GameManager.OnPlayerChanged -= OnPlayerChanged;
            if (GameManager.Instance.State == null) return;
            GameManager.Instance.State.OnGamePause -= OnPause;
            GameManager.Instance.State.OnGameResume -= OnResume;
            GameManager.Instance.State.OnGameOver -= OnGameOver;
        }

        private void OnPause() => m_waveSystem?.Pause();
        private void OnResume() => m_waveSystem?.Resume();
        private void OnGameOver() => m_waveSystem?.Stop();
        private void OnPlayerChanged(PlayerBase newPlayer) => m_player = newPlayer;

        private void HandleWaveStarted(WaveData wave)
        {
            OnWaveStarted?.Invoke(wave);
            SpawnWaveMobsAsync(wave).Forget();
        }

        private void HandleWaveCompleted(WaveData wave) => OnWaveCompleted?.Invoke(wave);
        private void HandleStageCleared(int stageId) => OnStageCleared?.Invoke(stageId);

        #endregion

        #region 10. 리소스 로드 (Addressables Helper)

        private async UniTask EnsureMobPrefabLoaded(string key)
        {
            if (m_mobPrefabMap.ContainsKey(key)) return;

            try
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(key);
                var prefab = await handle.ToUniTask();
                if (prefab != null)
                {
                    m_mobPrefabMap[key] = prefab;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Spawner] Mob Prefab 로드 실패 ({key}): {ex.Message}");
            }
        }

        private async UniTask LoadItemPrefabsAsync()
        {
            await UniTask.WhenAll(
                LoadItemPrefab(m_expPrefabReference),
                LoadItemPrefab(m_coinPrefabReference)
            );
        }

        private async UniTask LoadItemPrefab(AssetReferenceGameObject reference)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return;
            if (m_loadedItemPrefabs.ContainsKey(reference)) return;

            try
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(reference);
                var prefab = await handle.ToUniTask();
                if (prefab != null)
                {
                    m_loadedItemPrefabs[reference] = prefab;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Spawner] Item Prefab 로드 실패: {ex.Message}");
            }
        }

        private T CreateItem<T>(AssetReferenceGameObject reference) where T : MonoBehaviour
        {
            if (!m_loadedItemPrefabs.TryGetValue(reference, out var prefab)) return null;

            var obj = Instantiate(prefab, m_mobParent).GetComponent<T>();
            if (obj is IObjectPoolUser poolUser)
            {
                poolUser.ObjectPoolSpawner = this;
            }

            return obj;
        }

        #endregion
    }

    /// <summary>
    /// 오브젝트 풀링을 사용하는 객체가 스포너 참조를 갖기 위해 구현하는 인터페이스입니다.
    /// </summary>
    public interface IObjectPoolUser
    {
        ObjectPoolSpawner ObjectPoolSpawner { set; }
    }
}