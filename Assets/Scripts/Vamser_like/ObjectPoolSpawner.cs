using System.Collections;
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
        public IObjectPool<VamserMobBase> MobObjectPool;
        public IObjectPool<EXP_Obj> ExpObjectPool;
        public IObjectPool<Coin_Obj> CoinObjectPool;
        
        [Header("<color=green>몹 오브젝트</color>")]
        [SerializeField] private int poolSizeMobCount = 20;
        
        [Header("<color=green>몹 프리팹</color>")]
        [SerializeField] private AssetReferenceGameObject mobPrefabReference;
        private GameObject _loadedMobPrefab;

        [Header("<color=green>몹 오브젝트 스폰 위치</color>")]
        [SerializeField] private Transform mobParent;
        
        // 몹 카운트 관련
        private int _mobCount;
        public int MobCount => _mobCount;
        
        private int _mobSpawnWave;
        public int MobSpawnWave => _mobSpawnWave;
        
        [Header("<color=green>경험치 오브젝트</color>")]
        [SerializeField] private EXP_Obj expPrefab;
        [SerializeField] private EXP_Obj bigExpPrefab;
        
        [Header("<color=green>코인 오브젝트</color>")]
        [SerializeField] private Coin_Obj coinPrefab;
        [SerializeField] private float coinSpawnPercent = 25;
        
        // 스폰 제어 변수
        private bool _isSpawningAllowed = true;
        private Coroutine _spawnCoroutine;
        
        // 기타 참조
        private Camera _mainCamera;
        
        #endregion

        #region 초기화 및 라이프사이클

        /// <summary>
        /// 컴포넌트 초기화 및 오브젝트 풀 생성
        /// </summary>
         private void Awake()
         {
             // InitializePools() 호출을 InitializeAndStartSpawning으로 이동했습니다.
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
                OnGet,
                OnRelease,
                OnDestory,
                maxSize: poolSizeMobCount
            );

            // 경험치 오브젝트 풀 초기화
            ExpObjectPool = new ObjectPool<EXP_Obj>(
                Create_EXP,
                OnGet_EXP,
                OnRelease_EXP,
                OnDestory_EXP,
                maxSize: poolSizeMobCount
            );

            // 코인 오브젝트 풀 초기화
            CoinObjectPool = new LinkedPool<Coin_Obj>(
                CreateCoin,
                OnGet_Coin,
                Onrelease_Coin,
                OnDestory_Coin,
                maxSize: poolSizeMobCount
            );
        }

        /// <summary>
        /// 이벤트 구독 설정
        /// </summary>
        private void Start()
        {
            SubscribeToEvents();
        }

        /// <summary>
        /// 게임 상태 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            PlayStateManager.OnGameStart -= GameStart; // 중복 구독 방지
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= GameEnd;

            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += GameEnd;
        }

        // /// <summary>
        // /// 이벤트 구독 해제
        // /// </summary>
        // private void OnDestroy()
        // {
        //     UnsubscribeFromEvents();
        // }

        /// <summary>
        /// VamserLikeGameManager에 의해 호출되어 스포너를 초기화하고 몹 스폰을 시작합니다.
        /// </summary>
        /// <param name="player">스폰된 플레이어의 참조</param>
        public async UniTask InitializeAndStartSpawning(PlayerBase player)
        {
            _player = player;
            if (_player == null)
            {
                Debug.LogError("플레이어 참조가 null입니다. 몹 스폰을 시작할 수 없습니다.");
                return;
            }

            // Addressable에서 몹 프리팹 로드
            if (_loadedMobPrefab == null)
            {
                if (mobPrefabReference != null && mobPrefabReference.RuntimeKeyIsValid())
                {
                    _loadedMobPrefab = await mobPrefabReference.LoadAssetAsync<GameObject>().ToUniTask();
                }
                
                if (_loadedMobPrefab == null)
                {
                    Debug.LogError("몹 프리팹을 Addressable에서 로드하는 데 실패했습니다.");
                    return;
                }
            }
            
            // 프리팹 로드 후 풀 초기화
            InitializePools();
            
            Debug.Log("ObjectPoolSpawner가 플레이어 참조를 받고 스폰을 시작합니다.");
            
            // GameStart 로직을 여기로 이동
            if (PlayStateManager.instance.isPlay)
            {
                // 초기 몹 스폰
                SpawnInitialMobs();
                _mobSpawnWave = 1;
                
                // 스폰 코루틴 시작 (필요시 활성화)
                // _spawnCoroutine = StartCoroutine(SpawnRoutine());
            }
        }
        /// <summary>
        /// 게임 상태 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= GameEnd;
        }

        #endregion

        #region 게임 상태 관리

        /// <summary>
        /// 게임 시작 시 몹 스폰 초기화 (이제 VamserLikeGameManager에서 호출됨)
        /// </summary>
        private void GameStart()
        {
            // 이 메서드의 로직은 InitializeAndStartSpawning으로 이동했습니다.
            // OnGameStart 이벤트에서 직접 호출되지 않습니다.
        }

        /// <summary>
        /// 게임 일시정지 처리
        /// </summary>
        private void Pause()
        {
            // 스폰 일시 중지
            _isSpawningAllowed = false;
    
            // 진행 중인 스폰 코루틴 중지
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
    
            Debug.Log("오브젝트 스폰 일시 중지됨");
        }

        /// <summary>
        /// 게임 재개 처리
        /// </summary>
        private void Resume()
        {
            // 스폰 재개
            _isSpawningAllowed = true;
    
            // 필요시 스폰 코루틴 재시작
            if (_spawnCoroutine == null && PlayStateManager.instance.isPlay)
            {
                // _spawnCoroutine = StartCoroutine(SpawnRoutine());
            }
    
            Debug.Log("오브젝트 스폰 재개됨");
        }

        /// <summary>
        /// 게임 종료 시 모든 오브젝트 풀 정리
        /// </summary>
        private void GameEnd()
        {
            // 모든 코루틴 종료
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
            
            // 모든 오브젝트 풀 정리
            MobObjectPool.Clear();
            ExpObjectPool.Clear();
            CoinObjectPool.Clear();
            
            Debug.Log("게임 종료: 모든 오브젝트 풀 정리됨");
        }

        #endregion

        #region 몹 스폰 및 관리

        /// <summary>
        /// 초기 몹 스폰
        /// </summary>
        private void SpawnInitialMobs()
        {
            for (int i = 0; i < poolSizeMobCount; i++)
            {
                if (_isSpawningAllowed)
                {
                    MobObjectPool.Get();
                    _mobCount++;
                }
            }
        }

        /// <summary>
        /// 남은 몹이 있는지 체크하고 없으면 리스폰 예약
        /// </summary>
        private void CheckMob()
        {
            if (_mobCount <= 0)
            {   
                // 몹이 모두 죽었을때 3초후 리스폰
                Invoke(nameof(ReSpawn), 3);
            }
        }

        /// <summary>
        /// 다음 웨이브 몹 리스폰
        /// </summary>
        private void ReSpawn()
        {
            if (!_isSpawningAllowed) return;
            
            _mobSpawnWave++;
            poolSizeMobCount += 5;
            
            Debug.Log($"Wave: {_mobSpawnWave}, 몹 스폰 수: {poolSizeMobCount}");
            
            for (int i = 0; i < poolSizeMobCount; i++)
            {
                if (_isSpawningAllowed)
                {
                    MobObjectPool.Get();
                    _mobCount++;
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
            exp.gameObject.SetActive(true);
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
                coin.gameObject.SetActive(true);
            }
        }

        #endregion

        #region 오브젝트 풀 - 몹

        /// <summary>
        /// 몹 오브젝트 생성
        /// </summary>
        private VamserMobBase Create_Mob()
        {
            if (_loadedMobPrefab == null)
            {
                Debug.LogError("몹 프리팹이 로드되지 않아 몹을 생성할 수 없습니다.");
                return null;
            }
            
            VamserMobBase mob = Instantiate(_loadedMobPrefab, mobParent).GetComponent<VamserMobBase>();
            mob.objectPoolSpawner = this;
            return mob;
        }

        /// <summary>
        /// 몹 오브젝트 풀에서 가져올 때 처리
        /// </summary>
        private void OnGet(VamserMobBase mob)
        {
            MoveObjectOffScreen(mob);
            mob.SetTarget(_player);
        }

        /// <summary>
        /// 몹 오브젝트를 풀에 반환할 때 처리
        /// </summary>
        private void OnRelease(VamserMobBase obj)
        {
            obj.gameObject.SetActive(false);
            _mobCount--;
            
            // 남은 몹 수 체크
            CheckMob();
            
            // 몹이 죽었을 때 아이템 생성
            SpawnExp(obj);
            SpawnCoin(obj);
        }

        /// <summary>
        /// 몹 오브젝트 파괴 시 처리
        /// </summary>
        private void OnDestory(VamserMobBase obj)
        {
            Destroy(obj.gameObject);
        }

        #endregion

        #region 오브젝트 풀 - 경험치

        /// <summary>
        /// 경험치 오브젝트 생성
        /// </summary>
        private EXP_Obj Create_EXP()
        {
            // TODO: 큰 경험치는 일정 웨이브 이후 또는 특정 조건에서 생성
            if (_mobSpawnWave >= 5 && Random.value > 0.9f && bigExpPrefab != null)
            {
                return CreateObject(bigExpPrefab);
            }
            
            return CreateObject(expPrefab);
        }

        /// <summary>
        /// 경험치 오브젝트 풀에서 가져올 때 처리
        /// </summary>
        private void OnGet_EXP(EXP_Obj obj)
        {
            OnGetObject(obj);
        }

        /// <summary>
        /// 경험치 오브젝트를 풀에 반환할 때 처리
        /// </summary>
        private void OnRelease_EXP(EXP_Obj obj)
        {
            OnReleaseObject(obj);
        }

        /// <summary>
        /// 경험치 오브젝트 파괴 시 처리
        /// </summary>
        private void OnDestory_EXP(EXP_Obj obj)
        {
            OnDestroyObject(obj);
        }

        #endregion

        #region 오브젝트 풀 - 코인

        /// <summary>
        /// 코인 오브젝트 생성
        /// </summary>
        private Coin_Obj CreateCoin()
        {
            return CreateObject(coinPrefab);
        }

        /// <summary>
        /// 코인 오브젝트 풀에서 가져올 때 처리
        /// </summary>
        private void OnGet_Coin(Coin_Obj obj)
        {
            OnGetObject(obj);
        }

        /// <summary>
        /// 코인 오브젝트를 풀에 반환할 때 처리
        /// </summary>
        private void Onrelease_Coin(Coin_Obj obj)
        {
            OnReleaseObject(obj);
        }

        /// <summary>
        /// 코인 오브젝트 파괴 시 처리
        /// </summary>
        private void OnDestory_Coin(Coin_Obj obj)
        {
            OnDestroyObject(obj);
        }

        #endregion

        #region 공통 유틸리티

        /// <summary>
        /// 제네릭 오브젝트 생성 메서드
        /// </summary>
        private T CreateObject<T>(T prefab) where T : MonoBehaviour
        {
            T obj = Instantiate(prefab.gameObject, mobParent).GetComponent<T>();
            
            // 오브젝트 타입에 따라 추가 설정
            if (obj is EXP_Obj expObj)
            {
                expObj.objectPoolSpawner = this;
            }
            else if (obj is Coin_Obj coinObj)
            {
                coinObj.objectPoolSpawner = this;
            }

            return obj;
        }

        /// <summary>
        /// 오브젝트 활성화 공통 처리
        /// </summary>
        private void OnGetObject<T>(T obj) where T : MonoBehaviour
        {
            obj.gameObject.SetActive(true);
        }

        /// <summary>
        /// 오브젝트 비활성화 공통 처리
        /// </summary>
        private void OnReleaseObject<T>(T obj) where T : MonoBehaviour
        {
            obj.gameObject.SetActive(false);
        }

        /// <summary>
        /// 오브젝트 파괴 공통 처리
        /// </summary>
        private void OnDestroyObject<T>(T obj) where T : MonoBehaviour
        {
            Destroy(obj.gameObject);
        }

        /// <summary>
        /// 오브젝트를 화면 밖에 위치시키는 메서드
        /// </summary>
        private void MoveObjectOffScreen(VamserMobBase obj)
        {
            if (_mainCamera == null)
            {
                Debug.LogWarning("Main Camera가 설정되지 않았습니다.");
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
}

