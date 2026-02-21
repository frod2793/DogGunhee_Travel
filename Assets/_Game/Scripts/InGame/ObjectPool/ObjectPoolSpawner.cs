using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Managers;
using InGame.Core.Interfaces;
using InGame.Mob.MobBase;
using InGame.Player.Player_Base;
using InGame.vamsir;
using InGame.Mob.Systems;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace InGame.ObjectPool
{
    /// <summary>
    /// [설명]: 게임 내 모든 동적 오브젝트(몬스터, 아이템, 이펙트 등)의 생성과 재사용을 관리하는 스포너입니다.
    /// 실제 웨이브 진행 로직은 WaveSystem에 위임하고, 위치 계산은 SpawnPositionSolver를 사용합니다.
    /// </summary>
    public class ObjectPoolSpawner : MonoBehaviour
    {
        #region 에디터 설정

        [Header("맵 및 위치 설정")]
        [SerializeField, Tooltip("몬스터 스폰 영역을 제한하는 맵 스프라이트")]
        private SpriteRenderer m_mapRange;

        [SerializeField, Tooltip("스폰된 몬스터들이 배치될 부모 트랜스폼")]
        private Transform m_mobParent;

        [Header("데이터 및 풀 설정")]
        [SerializeField, Tooltip("스테이지/웨이브 데이터베이스")]
        private StageDatabase m_stageDatabase;

        [SerializeField, Tooltip("풀 최대 사이즈 (초과 시 파괴)")]
        private int m_maxPoolSize = 100;

        [SerializeField, Tooltip("일반 경험치 아이템")]
        private AssetReferenceGameObject m_expPrefabReference;

        [SerializeField, Tooltip("코인 아이템")]
        private AssetReferenceGameObject m_coinPrefabReference;

        [SerializeField, Tooltip("코인 드랍 확률 (0~100)")]
        private float m_coinSpawnPercent = 25f;

        #endregion

        #region 내부 상태 및 시스템

        /// <summary> 플레이어 본체 참조 </summary>
        private PlayerBase m_player;

        /// <summary> 메인 카메라 참조 </summary>
        private Camera m_mainCamera;

        /// <summary> 월드 맵의 경계 데이터 </summary>
        private Bounds m_mapBounds;

        /// <summary> 몬스터 통합 관리자 참조 </summary>
        private MobManager m_mobManager;

        /// <summary> 웨이브 시스템 비즈니스 로직 </summary>
        private WaveSystem m_waveSystem;

        /// <summary> 스폰 위치 계산 알고리즘 </summary>
        private SpawnPositionSolver m_positionSolver;

        /// <summary> 몬스터별 오브젝트 풀 데이터베이스 (Key: MobKey) </summary>
        private Dictionary<string, IObjectPool<MobBase>> m_mobPools = new Dictionary<string, IObjectPool<MobBase>>();

        /// <summary> 몬스터 프리팹 캐시 </summary>
        private Dictionary<string, GameObject> m_mobPrefabMap = new Dictionary<string, GameObject>();

        /// <summary> 아이템 프리팹 캐시 </summary>
        private Dictionary<AssetReferenceGameObject, GameObject> m_loadedItemPrefabs = new Dictionary<AssetReferenceGameObject, GameObject>();

        /// <summary> 비동기 스폰 루틴 취소 토큰 </summary>
        private CancellationTokenSource m_spawnCts;

        /// <summary> 플레이어 데이터 DTO 참조 </summary>
        private InGame.Data.PlayerDataDTO m_playerData;

        /// <summary> 사운드 매니저 참조 </summary>
        private InGame.Services.ISoundManager m_soundManager;
        private IGameStateService m_gameState;
        private ICombatContext m_combatCtx;
        private IPlayerContext m_playerCtx;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 경험치 아이템 오브젝트 풀입니다.
        /// </summary>
        public IObjectPool<EXP_Obj> ExpObjectPool { get; private set; }

        /// <summary>
        /// [설명]: 코인 아이템 오브젝트 풀입니다.
        /// </summary>
        public IObjectPool<Coin_Obj> CoinObjectPool { get; private set; }
        
        /// <summary> [설명]: 스포너에서 계산된 실제 맵 경계 데이터를 반환합니다. </summary>
        public Bounds MapBounds => m_mapBounds;

        /// <summary>
        /// [설명]: 현재 월드에 활성화된 전체 몬스터 수입니다.
        /// </summary>
        public int ActiveMobCount => m_waveSystem?.ActiveMobCount ?? 0;

        /// <summary>
        /// [설명]: 현재 진행 중인 웨이브 번호입니다.
        /// </summary>
        public int CurrentWave => m_waveSystem?.CurrentWaveId ?? 0;

        /// <summary>
        /// [설명]: 현재 진행 중인 스테이지 번호입니다.
        /// </summary>
        public int CurrentStage => m_waveSystem?.CurrentStageId ?? 0;

        #endregion

        #region 이벤트

        /// <summary> 웨이브 시작 시 발생하는 이벤트 </summary>
        public event Action<WaveData> OnWaveStarted;

        /// <summary> 웨이브 목표 달성 시 발생하는 이벤트 </summary>
        public event Action<WaveData> OnWaveCompleted;

        /// <summary> 스테이지 클리어 시 발생하는 이벤트 </summary>
        public event Action<int> OnStageCleared;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 기본적인 참조를 초기화합니다.
        /// </summary>
        private void Start()
        {
            m_mainCamera = Camera.main;

            InitializeMapBounds();
            InitializeSystems();
        }

        /// <summary>
        /// [설명]: 활성화 시 시스템 이벤트를 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            SubscribeEvents();
        }

        /// <summary>
        /// [설명]: 비활성화 시 이벤트를 해제하고 진행 중인 스폰 루틴을 중단합니다.
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeEvents();

            m_waveSystem?.Pause();

            if (m_spawnCts != null)
            {
                m_spawnCts.Cancel();
                m_spawnCts.Dispose();
                m_spawnCts = null;
            }
        }

        /// <summary>
        /// [설명]: 오브젝트가 파괴될 때 모든 풀과 리소스를 해제합니다.
        /// </summary>
        private void OnDestroy()
        {
            m_waveSystem?.Dispose();

            foreach (var pool in m_mobPools.Values)
            {
                pool.Clear();
            }
            ExpObjectPool?.Clear();
            CoinObjectPool?.Clear();

            foreach (var prefab in m_mobPrefabMap.Values)
            {
                Addressables.Release(prefab);
            }
            foreach (var prefab in m_loadedItemPrefabs.Values)
            {
                Addressables.Release(prefab);
            }

            m_mobPrefabMap.Clear();
            m_loadedItemPrefabs.Clear();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 맵의 경계 범위를 설정하고 초기화합니다.
        /// </summary>
        private void InitializeMapBounds()
        {
            if (m_mapRange == null)
            {
                var mapObj = GameObject.FindGameObjectWithTag("Map");
                if (mapObj != null)
                {
                    m_mapRange = mapObj.GetComponent<SpriteRenderer>();
                }
            }

            if (m_mapRange != null)
            {
                if (m_mapRange.sprite != null)
                {
                    m_mapBounds = m_mapRange.bounds;
                }
                else
                {
                    m_mapBounds = new Bounds(m_mapRange.transform.position, new Vector3(100f, 100f, 10f));
                }
            }
            else if (m_combatCtx != null && m_combatCtx.MapBounds.size.sqrMagnitude > 1f)
            {
                m_mapBounds = m_combatCtx.MapBounds;
            }
            else
            {
                m_mapBounds = new Bounds(transform.position, new Vector3(100f, 100f, 10f));
            }

            Vector3 center = m_mapBounds.center;
            center.z = 0;
            m_mapBounds.center = center;

            if (m_mapBounds.size.x < 1f || m_mapBounds.size.y < 1f)
            {
                m_mapBounds.size = new Vector3(100f, 100f, 10f);
            }
        }

        /// <summary>
        /// [설명]: 스폰 시스템에 필요한 로직 클래스들을 개별적으로 초기화합니다.
        /// </summary>
        private void InitializeSystems()
        {
            m_positionSolver = new SpawnPositionSolver(
                mapBounds: m_mapBounds,
                minSpawnDistance: 4f,
                maxSpawnDistance: 12f,
                maxAttempts: 50
            );

            m_waveSystem = new WaveSystem();
            m_waveSystem.OnWaveStarted += HandleWaveStarted;
            m_waveSystem.OnWaveCompleted += HandleWaveCompleted;
            m_waveSystem.OnStageCleared += HandleStageCleared;
        }

        /// <summary>
        /// [설명]: 스포너의 의존성을 주입하고 실제 웨이브를 시작합니다.
        /// </summary>
        public async UniTask InitializeAndStartSpawning(
            IPlayerContext playerContext, 
            MobManager mobManager, 
            InGame.Data.PlayerDataDTO playerData, 
            InGame.Services.ISoundManager soundManager, 
            IGameStateService gameState,
            ICombatContext combatContext,
            int startStageId = 1)
        {
            m_playerCtx = playerContext;
            m_player = m_playerCtx?.SpawnedPlayer;
            m_mobManager = mobManager;
            m_playerData = playerData;
            m_soundManager = soundManager;
            m_gameState = gameState;
            m_combatCtx = combatContext;
            
            LogManager.Log($"[ObjectPoolSpawner] InitializeAndStartSpawning 호출 - Player: {(m_player != null ? "있음" : "없음")}, Stage: {startStageId}", LogManager.LogCategory.System);

            if (m_player == null)
            {
                LogManager.LogWarning("[ObjectPoolSpawner] 플레이어가 없어 스폰을 시작할 수 없습니다. (SpawnedPlayer is null)", LogManager.LogCategory.System);
                return;
            }

            InitializeMapBounds();
            InitializeSystems();

            await LoadItemPrefabsAsync();
            InitializeItemPools();

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

            m_waveSystem.Start(stage);
        }

        /// <summary>
        /// [설명]: 경험치 및 코인 아이템을 위한 오브젝트 풀을 생성합니다.
        /// </summary>
        private void InitializeItemPools()
        {
            ExpObjectPool = new ObjectPool<EXP_Obj>(
                createFunc: () => CreateItem<EXP_Obj>(m_expPrefabReference),
                actionOnGet: OnGetItem,
                actionOnRelease: OnReleaseItem,
                actionOnDestroy: OnDestroyObject,
                defaultCapacity: 50,
                maxSize: m_maxPoolSize
            );

            CoinObjectPool = new LinkedPool<Coin_Obj>(
                createFunc: () => CreateItem<Coin_Obj>(m_coinPrefabReference),
                actionOnGet: OnGetItem,
                actionOnRelease: OnReleaseItem,
                actionOnDestroy: OnDestroyObject,
                maxSize: m_maxPoolSize
            );
        }

        #endregion

        #region 스폰 처리 로직

        /// <summary>
        /// [설명]: 비동기 루프를 통해 주기적으로 몬스터를 스폰합니다.
        /// </summary>
        private async UniTaskVoid SpawnWaveMobsAsync(WaveData wave)
        {
            foreach (var mobInfo in wave.mobs)
            {
                LogManager.Log($"[ObjectPoolSpawner] 프리팹 확인 중: {mobInfo.mobKey}", LogManager.LogCategory.System);
                await EnsureMobPrefabLoaded(mobInfo.mobKey);
            }

            LogManager.Log($"[ObjectPoolSpawner] 웨이브 {wave.waveId} 스폰 루프 진입 완료", LogManager.LogCategory.System);
            m_spawnCts = new CancellationTokenSource();
            var token = CancellationTokenSource
                .CreateLinkedTokenSource(m_spawnCts.Token, this.GetCancellationTokenOnDestroy()).Token;

            float timer = 0f;

            while (m_waveSystem.IsSpawningAllowed && m_waveSystem.CurrentWaveId == wave.waveId)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                timer += Time.deltaTime;
                if (timer >= wave.spawnInterval)
                {
                    timer = 0f;

                    if (m_waveSystem.CanSpawn(m_maxPoolSize))
                    {
                        string targetKey = GetRandomMobKey(wave.mobs);
                        SpawnMob(targetKey);
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>
        /// [설명]: 웨이브 설정 데이터에 따라 가중치 랜덤으로 스폰할 몬스터 키를 선택합니다.
        /// </summary>
        private string GetRandomMobKey(List<MobSpawnData> mobs)
        {
            if (mobs == null || mobs.Count == 0)
            {
                return string.Empty;
            }

            int totalRate = 0;
            foreach (var m in mobs)
            {
                totalRate += m.spawnRate;
            }

            int randomPoint = Random.Range(0, totalRate);
            int currentRate = 0;

            foreach (var m in mobs)
            {
                currentRate += m.spawnRate;
                if (randomPoint < currentRate)
                {
                    return m.mobKey;
                }
            }

            return mobs[0].mobKey;
        }

        /// <summary>
        /// [설명]: 사망 지점에 아이템(경험치, 코인)을 생성합니다.
        /// </summary>
        private void SpawnItems(Vector3 position)
        {
            if (ExpObjectPool != null)
            {
                var exp = ExpObjectPool.Get();
                if (exp != null)
                {
                    exp.transform.position = position;
                }
            }

            if (Random.Range(0f, 100f) < m_coinSpawnPercent && CoinObjectPool != null)
            {
                var coin = CoinObjectPool.Get();
                if (coin != null)
                {
                    coin.transform.position = position;
                }
            }
        }

        #endregion

        #region 오브젝트 풀 관리

        /// <summary>
        /// [설명]: 지정된 키에 해당하는 몬스터를 풀에서 가져와 월드에 배치합니다.
        /// </summary>
        private void SpawnMob(string mobKey)
        {
            if (string.IsNullOrEmpty(mobKey))
            {
                return;
            }

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

        /// <summary>
        /// [설명]: 몬스터 프리팹을 인스턴스화하고 풀링 인터페이스를 주입합니다.
        /// </summary>
        private MobBase CreateMobInstance(string mobKey)
        {
            if (!m_mobPrefabMap.TryGetValue(mobKey, out var prefab))
            {
                return null;
            }

            var obj = Instantiate(prefab, m_mobParent).GetComponent<MobBase>();
            if (obj == null)
            {
                return null;
            }

            if (obj is IObjectPoolUser poolUser)
            {
                poolUser.ObjectPoolSpawner = this;
            }

            obj.gameObject.name = mobKey;
            return obj;
        }

        /// <summary>
        /// [설명]: 풀에서 꺼낸 몬스터를 재배치하고 초기화합니다.
        /// </summary>
        private void OnGetMob(MobBase mob)
        {
            Vector3 spawnPos = m_positionSolver.CalculateSpawnPosition(m_mainCamera != null ? m_mainCamera : Camera.main);
            mob.transform.position = spawnPos;

            mob.gameObject.SetActive(true);


            mob.Init(m_mobManager, m_playerData, m_soundManager, m_gameState, m_combatCtx);
            mob.SetTarget(m_player);

            m_waveSystem?.OnMobSpawned();
        }

        /// <summary>
        /// [설명]: 몬스터가 죽었을 때 풀에 반납하며 아이템 드랍 처리를 수행합니다.
        /// </summary>
        private void OnReleaseMob(MobBase mob)
        {
            if (!mob.gameObject.activeSelf)
            {
                return;
            }

            mob.gameObject.SetActive(false);

            m_waveSystem?.OnMobDied();

            SpawnItems(mob.transform.position);
        }

        /// <summary>
        /// [설명]: 외부에서 호출하여 몬스터를 안전하게 풀로 반환합니다.
        /// </summary>
        public void ReturnMob(MobBase mob)
        {
            if (mob == null)
            {
                return;
            }

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
        /// [설명]: 특정 몬스터를 지정된 위치에 강제로 스폰합니다. (테스트용)
        /// </summary>
        /// <param name="mobKey">스폰할 몬스터의 Addressable Key</param>
        /// <param name="position">스폰 위치</param>
        public async UniTask SpawnMobForTest(string mobKey, Vector3 position)
        {
            if (string.IsNullOrEmpty(mobKey)) return;

            // 프리팹 로드 보장
            await EnsureMobPrefabLoaded(mobKey);

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

            var mob = pool.Get();
            if (mob != null)
            {
                // OnGetMob에서 설정된 랜덤 위치를 테스트용 위치로 덮어쓰기
                mob.transform.position = position;
                LogManager.Log($"[Test] 몬스터 스폰 완료: {mobKey} at {position}", LogManager.LogCategory.ObjectPoolSpawner);
            }
        }

        /// <summary>
        /// [설명]: 현재 활성화된 모든 몬스터를 즉시 풀로 반환합니다. (테스트용)
        /// </summary>
        public void ReturnAllMobsForTest()
        {
            if (m_mobManager == null) return;

            var activeTargets = m_mobManager.GetAllActiveTargets();
            List<InGame.Mob.MobBase.MobBase> mobsToReturn = new List<InGame.Mob.MobBase.MobBase>();

            // 원본 리스트 수정을 방지하기 위해 복사본 생성
            for (int i = 0; i < activeTargets.Count; i++)
            {
                if (activeTargets[i] is InGame.Mob.MobBase.MobBase mob)
                {
                    mobsToReturn.Add(mob);
                }
            }

            foreach (var mob in mobsToReturn)
            {
                ReturnMob(mob);
            }

            LogManager.Log($"[Test] 모든 몬스터 반환 완료 (총 {mobsToReturn.Count}마리)", LogManager.LogCategory.ObjectPoolSpawner);
        }

        /// <summary>
        /// [설명]: 외부에서 호출하여 아이템을 안전하게 풀로 반환합니다.
        /// </summary>
        public void ReturnItem(DropItemBase item)
        {
            if (item == null)
            {
                return;
            }

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

        private void OnGetItem<T>(T item) where T : MonoBehaviour
        {
            item.gameObject.SetActive(true);
        }

        private void OnReleaseItem<T>(T item) where T : MonoBehaviour
        {
            item.gameObject.SetActive(false);
        }

        private void OnDestroyObject<T>(T obj) where T : MonoBehaviour
        {
            if (obj != null)
            {
                Destroy(obj.gameObject);
            }
        }

        #endregion

        #region 이벤트 구독 핸들러

        /// <summary>
        /// [설명]: 전역 게임 상태 이벤트를 구독합니다.
        /// </summary>
        private void SubscribeEvents()
        {
            if (m_playerCtx != null) {
                m_playerCtx.OnPlayerChanged += OnPlayerChanged;
            }
            if (m_gameState == null || m_gameState.State == null)
            {
                return;
            }
            m_gameState.State.OnGamePause += OnPause;
            m_gameState.State.OnGameResume += OnResume;
            m_gameState.State.OnGameOver += OnGameOver;
        }

        /// <summary>
        /// [설명]: 시스템 파괴 시 등록된 이벤트를 모두 해제합니다.
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (m_playerCtx != null) {
                m_playerCtx.OnPlayerChanged -= OnPlayerChanged;
            }
            if (m_gameState == null || m_gameState.State == null)
            {
                return;
            }
            m_gameState.State.OnGamePause -= OnPause;
            m_gameState.State.OnGameResume -= OnResume;
            m_gameState.State.OnGameOver -= OnGameOver;
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

        #region 어드레서블 리소스 로드

        /// <summary>
        /// [설명]: 필요한 몬스터 프리팹이 로드되어 있는지 확인하고 없으면 비동기로 로드합니다.
        /// </summary>
        private async UniTask EnsureMobPrefabLoaded(string key)
        {
            if (m_mobPrefabMap.ContainsKey(key))
            {
                return;
            }

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

        /// <summary>
        /// [설명]: 아이템 프리팹들을 일괄적으로 사전 로드합니다.
        /// </summary>
        private async UniTask LoadItemPrefabsAsync()
        {
            await UniTask.WhenAll(
                LoadItemPrefab(m_expPrefabReference),
                LoadItemPrefab(m_coinPrefabReference)
            );
        }

        /// <summary>
        /// [설명]: 특정 아이템 프리팹을 어드레서블로 로드하고 캐시합니다.
        /// </summary>
        private async UniTask LoadItemPrefab(AssetReferenceGameObject reference)
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                return;
            }
            if (m_loadedItemPrefabs.ContainsKey(reference))
            {
                return;
            }

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

        /// <summary>
        /// [설명]: 아이템 풀에서 사용할 실제 인스턴스를 생성합니다.
        /// </summary>
        private T CreateItem<T>(AssetReferenceGameObject reference) where T : MonoBehaviour
        {
            if (!m_loadedItemPrefabs.TryGetValue(reference, out var prefab))
            {
                return null;
            }

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
    /// [설명]: 오브젝트 풀링을 사용하는 객체가 스포너 참조를 주입받기 위해 구현하는 인터페이스입니다.
    /// </summary>
    public interface IObjectPoolUser
    {
        ObjectPoolSpawner ObjectPoolSpawner { set; }
    }
}