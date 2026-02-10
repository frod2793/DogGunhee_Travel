using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.ObjectPool
{
    /// <summary>
    /// 스테이지 및 웨이브의 진행 흐름(시작, 대기, 종료)을 관리하는 순수 로직 클래스입니다.
    /// </summary>
    public class WaveSystem : IDisposable
    {
        #region 1. 내부 상태 및 데이터 (Fields)

        // 데이터 참조
        private StageData m_currentStage;
        
        // 상태 변수
        private int m_currentWaveIndex = -1;
        private int m_activeMobCount = 0;   // 현재 맵에 살아있는 몬스터 수
        private int m_spawnedMobCount = 0;  // 현재 웨이브에서 스폰된 총 몬스터 수
        
        // 상태 플래그
        private bool m_isSpawningAllowed = false;
        private bool m_isWaitingForNextWave = false; // 웨이브 사이 대기 시간(Intermission) 중인지 여부

        // 비동기 제어
        private CancellationTokenSource m_waveCts;

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        /// <summary>현재 진행 중인 스테이지 ID</summary>
        public int CurrentStageId => m_currentStage?.stageId ?? 0;

        /// <summary>현재 진행 중인 웨이브 ID</summary>
        public int CurrentWaveId => GetCurrentWaveData()?.waveId ?? 0;

        /// <summary>현재 맵에 존재하는 활성 몬스터 수</summary>
        public int ActiveMobCount => m_activeMobCount;

        /// <summary>외부 시스템(Spawner)이 몬스터 생성을 수행해도 되는지 여부</summary>
        public bool IsSpawningAllowed => m_isSpawningAllowed && !m_isWaitingForNextWave;

        #endregion

        #region 3. 이벤트 (Events)

        /// <summary>웨이브 시작 시 발생 (UI 갱신, 알림 등)</summary>
        public event Action<WaveData> OnWaveStarted;

        /// <summary>웨이브 종료 시 발생 (보상 지급 등)</summary>
        public event Action<WaveData> OnWaveCompleted;

        /// <summary>모든 웨이브 클리어 시 발생 (스테이지 종료)</summary>
        public event Action<int> OnStageCleared;

        #endregion

        #region 4. 초기화 및 제어 (Control Methods)

        /// <summary>
        /// 지정된 스테이지 데이터로 웨이브 시스템을 초기화하고 시작합니다.
        /// </summary>
        public void Start(StageData stageData)
        {
            if (stageData == null)
            {
                LogManager.LogError("[WaveSystem] 스테이지 데이터가 null입니다.", LogManager.LogCategory.System);
                return;
            }

            // 초기화
            m_currentStage = stageData;
            m_currentWaveIndex = -1;
            m_activeMobCount = 0;
            m_spawnedMobCount = 0;
            m_isSpawningAllowed = true;
            m_isWaitingForNextWave = false;

            // 첫 웨이브 시작
            ProcessNextWave();
        }

        /// <summary>
        /// 시스템을 일시 정지합니다. (스폰 중단, 타이머 일시정지 효과는 UniTask의 ignoreTimeScale 설정에 따라 다름)
        /// </summary>
        public void Pause()
        {
            m_isSpawningAllowed = false;
        }

        /// <summary>
        /// 시스템을 재개합니다.
        /// </summary>
        public void Resume()
        {
            m_isSpawningAllowed = true;
            // 재개 시 혹시 완료 조건이 충족되었는지 확인
            CheckWaveCompletionCondition();
        }

        /// <summary>
        /// 시스템을 강제로 중단하고 리소스를 정리합니다.
        /// </summary>
        public void Stop()
        {
            m_isSpawningAllowed = false;
            CancelCurrentTask();
            m_currentStage = null;
        }

        public void Dispose()
        {
            Stop();
        }

        #endregion

        #region 5. 상태 갱신 (Status Updates)

        /// <summary>
        /// 몬스터 스폰 시 호출됩니다. (Spawner -> System)
        /// </summary>
        public void OnMobSpawned()
        {
            m_activeMobCount++;
            m_spawnedMobCount++;
        }

        /// <summary>
        /// 몬스터 사망 시 호출됩니다. (Spawner -> System)
        /// </summary>
        public void OnMobDied()
        {
            m_activeMobCount--;
            if (m_activeMobCount < 0) m_activeMobCount = 0;

            CheckWaveCompletionCondition();
        }

        /// <summary>
        /// 현재 스폰 가능 여부를 판단합니다. (최대 수량 제한 포함)
        /// </summary>
        public bool CanSpawn(int maxPoolLimit)
        {
            // 1. 기본 시스템 상태 체크
            if (!IsSpawningAllowed) return false;
            
            // 2. 풀링 제한 체크
            if (m_activeMobCount >= maxPoolLimit) return false;

            var wave = GetCurrentWaveData();
            if (wave == null) return false;

            // 3. 웨이브 목표 수량 체크 (시간제 웨이브가 아닐 경우)
            // duration > 0 이면 시간제이므로 수량 제한 없이(혹은 풀 한계까지) 계속 나옴
            if (wave.duration <= 0 && wave.count > 0)
            {
                if (m_spawnedMobCount >= wave.count) return false;
            }

            return true;
        }

        public WaveData GetCurrentWaveData()
        {
            if (m_currentStage == null || m_currentWaveIndex < 0 || m_currentWaveIndex >= m_currentStage.waves.Count)
            {
                return null;
            }
            return m_currentStage.waves[m_currentWaveIndex];
        }

        #endregion

        #region 6. 내부 비즈니스 로직 (Internal Logic)

        /// <summary>
        /// 다음 웨이브를 준비하고 시작합니다.
        /// </summary>
        private void ProcessNextWave()
        {
            if (m_currentStage == null) return;

            m_currentWaveIndex++;

            // [스테이지 클리어 체크]
            if (m_currentWaveIndex >= m_currentStage.waves.Count)
            {
                m_isSpawningAllowed = false;
                LogManager.Log($"[WaveSystem] 스테이지 {m_currentStage.stageId} 클리어!", LogManager.LogCategory.System);
                OnStageCleared?.Invoke(m_currentStage.stageId);
                return;
            }

            // [웨이브 시작]
            WaveData wave = m_currentStage.waves[m_currentWaveIndex];
            
            // 상태 초기화
            m_spawnedMobCount = 0;
            // m_activeMobCount는 이전 웨이브에서 살아남은 몬스터가 있을 수 있으므로 0으로 초기화하지 않음 (누적)
            
            LogManager.Log($"[WaveSystem] 웨이브 {wave.waveId} 시작 (타입: {(wave.duration > 0 ? "시간제" : "처치제")})", LogManager.LogCategory.System);
            OnWaveStarted?.Invoke(wave);

            // 시간제 웨이브인 경우 타이머 시작
            if (wave.duration > 0)
            {
                RunWaveTimerAsync(wave.duration).Forget();
            }
        }

        /// <summary>
        /// 웨이브 완료 조건을 검사하고 필요 시 다음 단계로 넘어갑니다.
        /// </summary>
        private void CheckWaveCompletionCondition()
        {
            // 대기 중이거나 일시정지 상태면 체크 안 함
            if (m_isWaitingForNextWave || !m_isSpawningAllowed) return;

            WaveData wave = GetCurrentWaveData();
            if (wave == null) return;

            bool isWaveComplete = false;

            // 조건 1: 시간제 웨이브 (duration > 0)
            // -> 타이머(RunWaveTimerAsync)에 의해서만 완료됨. 여기서는 체크하지 않음.

            // 조건 2: 처치제 웨이브 (duration <= 0)
            // -> 목표 수만큼 스폰했고(spawned >= count) & 모두 죽었으면(active == 0) 완료
            if (wave.duration <= 0)
            {
                if (m_spawnedMobCount >= wave.count && m_activeMobCount == 0)
                {
                    isWaveComplete = true;
                }
            }

            if (isWaveComplete)
            {
                CompleteCurrentWave();
            }
        }

        /// <summary>
        /// 현재 웨이브를 종료하고 휴식(Wait) 시간을 갖습니다.
        /// </summary>
        private void CompleteCurrentWave()
        {
            if (m_isWaitingForNextWave) return;

            LogManager.Log($"[WaveSystem] 웨이브 {CurrentWaveId} 종료 조건 달성", LogManager.LogCategory.System);
            
            OnWaveCompleted?.Invoke(GetCurrentWaveData());
            
            // 다음 웨이브 대기 시작
            WaitAndStartNextWaveAsync().Forget();
        }

        // --- 비동기 루틴 ---

        /// <summary>
        /// 시간제 웨이브의 타이머를 실행합니다.
        /// </summary>
        private async UniTaskVoid RunWaveTimerAsync(float duration)
        {
            // 기존 작업 취소 및 새 토큰 생성
            CancelCurrentTask();
            m_waveCts = new CancellationTokenSource();
            var token = m_waveCts.Token;

            try
            {
                // 시간 대기 (일시정지 영향 받음: ignoreTimeScale = false 권장, 기획에 따라 변경)
                await UniTask.Delay(TimeSpan.FromSeconds(duration), ignoreTimeScale: false, cancellationToken: token);

                // 시간이 다 되면 무조건 웨이브 종료 (몬스터가 남아있어도 진행)
                if (!m_isWaitingForNextWave && m_isSpawningAllowed)
                {
                    CompleteCurrentWave();
                }
            }
            catch (OperationCanceledException)
            {
                // 웨이브 중단됨
            }
        }

        /// <summary>
        /// 웨이브 사이의 휴식 시간을 대기합니다.
        /// </summary>
        private async UniTaskVoid WaitAndStartNextWaveAsync()
        {
            m_isWaitingForNextWave = true;
            
            // 휴식 시간 가져오기
            float waitDuration = 3.0f;
            var currentWave = GetCurrentWaveData();
            if (currentWave != null)
            {
                waitDuration = currentWave.waitDuration;
            }

            // 토큰 갱신
            CancelCurrentTask();
            m_waveCts = new CancellationTokenSource();
            var token = m_waveCts.Token;

            try
            {
                if (waitDuration > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), ignoreTimeScale: false, cancellationToken: token);
                }

                if (m_isSpawningAllowed)
                {
                    m_isWaitingForNextWave = false;
                    ProcessNextWave();
                }
            }
            catch (OperationCanceledException)
            {
                // 대기 취소됨
            }
        }

        private void CancelCurrentTask()
        {
            if (m_waveCts != null)
            {
                m_waveCts.Cancel();
                m_waveCts.Dispose();
                m_waveCts = null;
            }
        }

        #endregion
    }
}