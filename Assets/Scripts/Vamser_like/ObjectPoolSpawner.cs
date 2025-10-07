using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 오브젝트 풀 관리 및 스폰 시스템
    /// </summary>
    public class ObjectPoolSpawner : MonoBehaviour
    {
        #region 필드 및 속성
        private PlayerBase _player;
        // 오브젝트 풀 참조
        public IObjectPool<VamserMobBase> MobObjectPool { get; private set; }
        public IObjectPool<EXP_Obj> ExpObjectPool { get; private set; }
        public IObjectPool<Coin_Obj> CoinObjectPool { get; private set; }
        // 제네릭 오브젝트 풀을 관리하기 위한 딕셔너리
        private readonly Dictionary<GameObject, IObjectPool<GameObject>> _genericObjectPools = new Dictionary<GameObject, IObjectPool<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefabMap = new Dictionary<GameObject, GameObject>();
        
        [Header("<color=green>몹 오브젝트</color>")]
        [SerializeField] private int initialMobCount = 20;
        [SerializeField] private int mobsPerWave = 20;
        [SerializeField] private int maxPoolSize = 100; // WebGL 환경을 위해 최대 풀 크기 제한
        
        [Header("<color=green>몹 프리팹</color>")]
        [SerializeField] private AssetReferenceGameObject mobPrefabReference;
        
        [Header("<color=green>몹 오브젝트 스폰 위치</color>")]
        [SerializeField] private Transform mobParent;
        
        // 몹 카운트 관련
        private int _activeMobCount;
        public int MobCount => _activeMobCount;
        
        private int _mobSpawnWave;
        public int MobSpawnWave => _mobSpawnWave;
        
        [Header("<color=green>경험치 오브젝트</color>")]
        [SerializeField] private AssetReferenceGameObject expPrefabReference;
        [SerializeField] private AssetReferenceGameObject bigExpPrefabReference;
        
        [Header("<color=green>코인 오브젝트</color>")]
        [SerializeField] private AssetReferenceGameObject coinPrefabReference;
        [SerializeField] private float coinSpawnPercent = 25;

        // 로드된 프리팹 캐시
        private readonly Dictionary<AssetReferenceGameObject, GameObject> _loadedPrefabs = new Dictionary<AssetReferenceGameObject, GameObject>();
        
        // 스폰 제어 변수
        private bool _isSpawningAllowed = true;
        private CancellationTokenSource _respawnCts;
        
        // 기타 참조
        private Camera _mainCamera;
        
        #endregion

        #region 초기화 및 라이프사이클

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
         private void Awake()
         {
             _mainCamera = Camera.main;
         }

        /// <summary>
        /// 오브젝트 풀 초기화
        /// </summary>
        private void InitializePools()
        {
            // 몹 오브젝트 풀 초기화
            MobObjectPool = new ObjectPool<VamserMobBase>(
                Create_Mob,
                OnGet_Mob,
                OnRelease_Mob,
                OnDestroy_PoolObject,
                maxSize: maxPoolSize
            );

            // 경험치 오브젝트 풀 초기화
            ExpObjectPool = new ObjectPool<EXP_Obj>(
                Create_EXP,
                OnGet_PoolObject,
                OnRelease_PoolObject,
                OnDestroy_PoolObject,
                maxSize: maxPoolSize
            );

            // 코인 오브젝트 풀 초기화
            CoinObjectPool = new LinkedPool<Coin_Obj>(
                Create_Coin,
                OnGet_PoolObject,
                OnRelease_PoolObject,
                OnDestroy_PoolObject,
                maxSize: maxPoolSize
            );
        }

        /// <summary>
        /// 이벤트 구독 설정
        /// </summary>
        private void OnEnable()
        {
            SubscribeToEvents();
        }

        /// <summary>
        /// 게임 상태 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            VamserLikeGameManager.OnPlayerChanged += HandlePlayerChanged;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= GameEnd;

            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += GameEnd;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeFromEvents();
            _respawnCts?.Cancel();
            _respawnCts?.Dispose();
            _respawnCts = null;
        }

        /// <summary>
        /// VamserLikeGameManager에 의해 호출되어 스포너를 초기화하고 몹 스폰을 시작합니다.
        /// </summary>
        /// <param name="player">스폰된 플레이어의 참조</param>
        public async UniTask InitializeAndStartSpawning(PlayerBase player)
        {
            _player = player;
            if (_player == null)
            {
                LogManager.LogError("플레이어 참조가 null입니다. 몹 스폰을 시작할 수 없습니다.", LogManager.LogCategory.ObjectPoolSpawner);
                return;
            }

            // Addressable에서 모든 프리팹 비동기 로드
            await LoadAllPrefabsAsync();

            if (!ArePrefabsLoaded())
            {
                LogManager.LogError("필수 프리팹 로드에 실패했습니다. 스폰을 시작할 수 없습니다.", LogManager.LogCategory.ObjectPoolSpawner);
                return;
            }
            
            // 프리팹 로드 후 풀 초기화
            InitializePools();
            
            LogManager.Log("ObjectPoolSpawner가 플레이어 참조를 받고 스폰을 시작합니다.", LogManager.LogCategory.ObjectPoolSpawner);
            
            //GameStart 로직
            if (PlayStateManager.instance.isPlay)
            {
                // 초기 몹 스폰
                SpawnInitialMobs();
                _mobSpawnWave = 1;
            }
        }
        /// <summary>
        /// 게임 상태 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            VamserLikeGameManager.OnPlayerChanged -= HandlePlayerChanged;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= GameEnd;
            _respawnCts?.Cancel();
            _respawnCts?.Dispose();
            _respawnCts = null;
        }

        #endregion

        #region 게임 상태 관리

        /// <summary>
        /// 게임 일시정지 처리
        /// </summary>
        private void Pause()
        {
            _isSpawningAllowed = false;
            LogManager.Log("오브젝트 스폰 일시 중지됨", LogManager.LogCategory.ObjectPoolSpawner);
        }

        /// <summary>
        /// 게임 재개 처리
        /// </summary>
        private void Resume()
        {
            _isSpawningAllowed = true;
            LogManager.Log("오브젝트 스폰 재개됨", LogManager.LogCategory.ObjectPoolSpawner);
        }

        /// <summary>
        /// 게임 종료 시 모든 오브젝트 풀 정리
        /// </summary>
        private void GameEnd()
        {
            _isSpawningAllowed = false;
            _respawnCts?.Cancel();
            _respawnCts?.Dispose();
            _respawnCts = null;
            
            // 모든 오브젝트 풀 정리
            MobObjectPool?.Clear();
            ExpObjectPool?.Clear();
            CoinObjectPool?.Clear();
            
            LogManager.Log("게임 종료: 모든 오브젝트 풀 정리됨", LogManager.LogCategory.ObjectPoolSpawner);
        }

        /// <summary>
        /// 플레이어 변경 이벤트를 수신하여 내부 플레이어 참조를 갱신합니다.
        /// </summary>
        private void HandlePlayerChanged(PlayerBase newPlayer)
        {
            _player = newPlayer;
            LogManager.Log($"플레이어 참조가 갱신되었습니다: {(newPlayer != null ? newPlayer.name : "null")}", LogManager.LogCategory.ObjectPoolSpawner);
        }

        #endregion

        #region 몹 스폰 및 관리

        /// <summary>
        /// 초기 몹 스폰
        /// </summary>
        private void SpawnInitialMobs()
        {
            mobsPerWave = initialMobCount;
            for (int i = 0; i < mobsPerWave; i++)
            {
                if (_isSpawningAllowed)
                {
                    MobObjectPool.Get();
                }
            }
        }

        /// <summary>
        /// 남은 몹이 있는지 체크하고 없으면 리스폰 예약
        /// WebGL 최적화를 위해 Invoke 대신 UniTask.Delay 사용
        /// </summary>
        private void CheckMob()
        {
            if (_activeMobCount <= 0 && _isSpawningAllowed)
            {   
                _respawnCts?.Cancel();
                _respawnCts = new CancellationTokenSource();
                ReSpawnAfterDelay(_respawnCts.Token).Forget();
            }
        }

        /// <summary>
        /// 다음 웨이브 몹 리스폰
        /// </summary>
        private async UniTaskVoid ReSpawnAfterDelay(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(3), cancellationToken: token);
                if (token.IsCancellationRequested || !_isSpawningAllowed) return;
                
                ReSpawn();
            }
            catch (System.OperationCanceledException) { }
        }
        private void ReSpawn()
        {
            
            _mobSpawnWave++;
            mobsPerWave = Mathf.Min(mobsPerWave + 5, maxPoolSize); // 최대 풀 크기를 넘지 않도록 제한
            
            LogManager.Log($"Wave: {_mobSpawnWave}, 몹 스폰 수: {mobsPerWave}", LogManager.LogCategory.ObjectPoolSpawner);
            
            for (int i = 0; i < mobsPerWave; i++)
            {
                if (_isSpawningAllowed)
                {
                    MobObjectPool.Get();
                }
            }
        }

        /// <summary>
        /// 지정된 몹 위치에 경험치 오브젝트 스폰
        /// </summary>
        private void SpawnExp(VamserMobBase obj)
        {
            if (!_isSpawningAllowed) return;
            
            EXP_Obj exp = ExpObjectPool.Get();
            exp.transform.position = obj.transform.position;
        }

        /// <summary>
        /// 확률에 따라 지정된 몹 위치에 코인 오브젝트 스폰
        /// </summary>
        private void SpawnCoin(VamserMobBase obj)
        {
            if (!_isSpawningAllowed) return;
            
            if (SpawnRandom(coinSpawnPercent))
            {
                Coin_Obj coin = CoinObjectPool.Get();
                coin.transform.position = obj.transform.position;
            }
        }

        #endregion

        #region 오브젝트 풀 - 몹

        /// <summary>
        /// 몹 오브젝트 생성
        /// </summary>
        private VamserMobBase Create_Mob()
        {
            return CreatePoolObject<VamserMobBase>(mobPrefabReference);
        }

        /// <summary>
        /// 몹 오브젝트 풀에서 가져올 때 처리
        /// </summary>
        private void OnGet_Mob(VamserMobBase mob)
        {
            OnGet_PoolObject(mob);
            MoveObjectOffScreen(mob);
            _activeMobCount++;
            mob.SetTarget(_player);
        }

        /// <summary>
        /// 몹 오브젝트를 풀에 반환할 때 처리
        /// </summary>
        private void OnRelease_Mob(VamserMobBase obj)
        {
            OnRelease_PoolObject(obj);
            _activeMobCount--;
            
            // 남은 몹 수 체크
            CheckMob();
            
            // 몹이 죽었을 때 아이템 생성
            SpawnExp(obj);
            SpawnCoin(obj);
        }

        #endregion

        #region 오브젝트 풀 - 경험치

        /// <summary>
        /// 경험치 오브젝트 생성
        /// </summary>
        private EXP_Obj Create_EXP()
        {
            // TODO: 큰 경험치는 일정 웨이브 이후 또는 특정 조건에서 생성
            bool canSpawnBigExp = _mobSpawnWave >= 5 && Random.value > 0.9f;
            AssetReferenceGameObject prefabRef = canSpawnBigExp ? bigExpPrefabReference : expPrefabReference;
            return CreatePoolObject<EXP_Obj>(prefabRef);
        }

        #endregion

        #region 오브젝트 풀 - 코인

        /// <summary>
        /// 코인 오브젝트 생성
        /// </summary>
        private Coin_Obj Create_Coin()
        {
            return CreatePoolObject<Coin_Obj>(coinPrefabReference);
        }

        #endregion

        #region 공통 풀링 로직

        /// <summary>
        /// 제네릭 오브젝트 생성 메서드
        /// </summary>
        private T CreatePoolObject<T>(AssetReferenceGameObject prefabRef) where T : MonoBehaviour
        {
            if (!_loadedPrefabs.TryGetValue(prefabRef, out var prefab) || prefab == null)
            {
                LogManager.LogError($"{typeof(T).Name}의 프리팹이 로드되지 않았습니다.", LogManager.LogCategory.ObjectPoolSpawner);
                return null;
            }
            
            T obj = Instantiate(prefab, mobParent).GetComponent<T>();
            
            // 오브젝트 타입에 따라 추가 설정
            if (obj is IObjectPoolSpawnerSettable poolObj)
            {
                poolObj.objectPoolSpawner = this;
            }

            return obj;
        }

        /// <summary>
        /// 오브젝트 활성화 공통 처리
        /// </summary>
        private void OnGet_PoolObject<T>(T obj) where T : MonoBehaviour
        {
            obj.gameObject.SetActive(true);
        }

        /// <summary>
        /// 오브젝트 비활성화 공통 처리
        /// </summary>
        private void OnRelease_PoolObject<T>(T obj) where T : MonoBehaviour
        {
            obj.gameObject.SetActive(false);
        }

        /// <summary>
        /// 오브젝트 파괴 공통 처리
        /// </summary>
        private void OnDestroy_PoolObject<T>(T obj) where T : MonoBehaviour
        {
            if (obj != null)
            {
                Destroy(obj.gameObject);
            }
        }

        #endregion

        #region 제네릭 오브젝트 풀링

        /// <summary>
        /// 지정된 프리팹을 사용하여 오브젝트 풀에서 게임 오브젝트를 스폰합니다.
        /// 풀이 없으면 새로 생성합니다.
        /// </summary>
        /// <param name="prefab">스폰할 프리팹</param>
        /// <param name="position">스폰 위치</param>
        /// <param name="rotation">스폰 회전값</param>
        /// <returns>스폰된 게임 오브젝트</returns>
        public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                LogManager.LogError("스폰하려는 프리팹이 null입니다.", LogManager.LogCategory.ObjectPoolSpawner);
                return null;
            }

            if (!_genericObjectPools.TryGetValue(prefab, out var pool))
            {
                // 이 프리팹에 대한 풀이 없으면 새로 생성합니다.
                pool = new ObjectPool<GameObject>(
                    createFunc: () => Instantiate(prefab),
                    // actionOnGet에서는 활성화만 처리합니다. 위치 설정은 Get() 이후에 수행합니다.
                    actionOnGet: (obj) => obj.SetActive(true),
                    actionOnRelease: (obj) => obj.SetActive(false),
                    actionOnDestroy: (obj) => Destroy(obj),
                    maxSize: 20 // 기본 풀 사이즈
                );
                _genericObjectPools[prefab] = pool;
            }

            // 1. 풀에서 인스턴스를 가져옵니다.
            var instance = pool.Get();
            
            // 2. 가져온 직후에 최신 위치와 회전값을 설정합니다.
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            
            _instanceToPrefabMap[instance] = prefab; // 반환 시 사용할 수 있도록 인스턴스와 프리팹을 매핑합니다.
            return instance;
        }

        /// <summary>
        /// 사용이 끝난 게임 오브젝트를 원래의 풀로 반환합니다.
        /// </summary>
        /// <param name="instance">반환할 게임 오브젝트 인스턴스</param>
        public void ReturnObject(GameObject instance)
        {
            if (instance == null) return;

            if (_instanceToPrefabMap.TryGetValue(instance, out var prefab) && _genericObjectPools.TryGetValue(prefab, out var pool))
            {
                pool.Release(instance);
                _instanceToPrefabMap.Remove(instance);
            }
            else
            {
                LogManager.LogWarning("풀링되지 않은 오브젝트를 반환하려고 시도했습니다. 오브젝트를 즉시 파괴합니다.", LogManager.LogCategory.ObjectPoolSpawner, instance);
                Destroy(instance);
            }
        }

        #endregion

        #region 공통 유틸리티

        /// <summary>
        /// 모든 프리팹을 비동기적으로 로드합니다.
        /// </summary>
        private async UniTask LoadAllPrefabsAsync()
        {
            var tasks = new List<UniTask>
            {
                LoadPrefabAsync(mobPrefabReference),
                LoadPrefabAsync(expPrefabReference),
                LoadPrefabAsync(bigExpPrefabReference),
                LoadPrefabAsync(coinPrefabReference)
            };
            await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// 단일 프리팹을 로드하고 캐시에 저장합니다.
        /// </summary>
        private async UniTask LoadPrefabAsync(AssetReferenceGameObject reference)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return;
            if (_loadedPrefabs.ContainsKey(reference)) return;

            var handle = Addressables.LoadAssetAsync<GameObject>(reference);
            await handle.ToUniTask();
            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                _loadedPrefabs[reference] = handle.Result;
            }
            else
            {
                LogManager.LogError($"에셋 로드 실패: {reference.AssetGUID}", LogManager.LogCategory.ObjectPoolSpawner);
            }
        }

        /// <summary>
        /// 필수 프리팹이 모두 로드되었는지 확인합니다.
        /// </summary>
        private bool ArePrefabsLoaded()
        {
            return _loadedPrefabs.ContainsKey(mobPrefabReference) &&
                   _loadedPrefabs.ContainsKey(expPrefabReference) &&
                   _loadedPrefabs.ContainsKey(coinPrefabReference);
        }

        /// <summary>
        /// 오브젝트를 화면 밖에 위치시키는 메서드
        /// </summary>
        private void MoveObjectOffScreen(VamserMobBase obj)
        {
            if (_mainCamera == null)
            {
                LogManager.LogWarning("Main Camera가 설정되지 않았습니다.", LogManager.LogCategory.ObjectPoolSpawner);
                return;
            }
            
            // 뷰포트 밖의 랜덤 위치 생성
            float x = Random.Range(-2.0f, 2.0f);
            float y = Random.Range(-2.0f, 2.0f);

            // 위치가 뷰포트 내부인 경우 강제로 외부로 이동
            if (x > 0 && x < 1) x = x < 0.5f ? -0.1f : 1.1f;
            if (y > 0 && y < 1) y = y < 0.5f ? -0.1f : 1.1f;

            // 뷰포트 위치를 월드 위치로 변환
            Vector3 offScreenPosition = _mainCamera.ViewportToWorldPoint(
                new Vector3(x, y, _mainCamera.nearClipPlane)
            );

            // 객체 위치 설정 및 활성화
            obj.transform.position = offScreenPosition;
            obj.gameObject.SetActive(true);
        }

        /// <summary>
        /// 확률에 따른 스폰 여부 결정
        /// </summary>
        private bool SpawnRandom(float percent)
        {
            return Random.Range(0, 100) < percent;
        }
        
        
        #endregion
    }

    /// <summary>
    /// ObjectPoolSpawner 참조를 설정하기 위한 인터페이스
    /// </summary>
    public interface IObjectPoolSpawnerSettable
    {
        ObjectPoolSpawner objectPoolSpawner { set; }
    }
}
