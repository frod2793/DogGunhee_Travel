using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.ObjectPool
{
    /// <summary>
    /// [설명]: 스테이지 및 웨이브의 진행 흐름(시작, 대기, 종료)을 관리하는 순수 로직 클래스입니다.
    /// 실시간 몬스터 카운트 및 웨이브 타이머 제어를 담당합니다.
    /// </summary>
    public class WaveSystem : IDisposable
    {
        #region 내부 상태 및 데이터

        /// <summary> 현재 진행 중인 스테이지 데이터 </summary>
        private StageData m_currentStage;

        /// <summary> 현재 진행 중인 웨이브의 리스트 인덱스 </summary>
        private int m_currentWaveIndex = -1;

        /// <summary> 현재 월드에 살아있는 활성 몬스터 수 </summary>
        private int m_activeMobCount = 0;

        /// <summary> 현재 웨이브에서 지금까지 스폰된 총 몬스터 수 </summary>
        private int m_spawnedMobCount = 0;

        /// <summary> 시스템 작동 허용 여부 </summary>
        private bool m_isSpawningAllowed = true;

        /// <summary> 웨이브 사이 휴식 시간(Intermission) 진행 여부 </summary>
        private bool m_isWaitingForNextWave = false;

        /// <summary> [설명]: 현재 진행 중인 웨이브/대기 시간의 남은 초 단위 시간입니다. </summary>
        private float m_remainingTime = 0f;

        /// <summary> 비동기 웨이브 루틴 제어 토큰 </summary>
        private CancellationTokenSource m_waveCts;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 현재 진행 중인 스테이지의 ID입니다.
        /// </summary>
        public int CurrentStageId => m_currentStage?.stageId ?? 0;

        /// <summary>
        /// [설명]: 현재 진행 중인 웨이브의 고유 ID입니다.
        /// </summary>
        public int CurrentWaveId => GetCurrentWaveData()?.waveId ?? 0;

        /// <summary>
        /// [설명]: 월드 상에 활성화된 몬스터 수입니다.
        /// </summary>
        public int ActiveMobCount => m_activeMobCount;

        /// <summary>
        /// [설명]: 외부 스포너에서 신규 몬스터를 생성할 수 있는 상태인지 여부입니다.
        /// </summary>
        public bool IsSpawningAllowed => m_isSpawningAllowed && !m_isWaitingForNextWave;

        #endregion

        #region 이벤트

        /// <summary> 새로운 웨이브가 시작될 때 발생하는 이벤트 </summary>
        public event Action<WaveData> OnWaveStarted;

        /// <summary> 웨이브가 성공적으로 종료되었을 때 발생하는 이벤트 </summary>
        public event Action<WaveData> OnWaveCompleted;

        /// <summary> 스테이지 내 모든 웨이브를 클리어했을 때 발생하는 이벤트 </summary>
        public event Action<int> OnStageCleared;

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 지정된 스테이지 데이터를 기반으로 웨이브 시스템을 가동합니다.
        /// </summary>
        /// <param name="stageData">시작할 스테이지 데이터</param>
        public void Start(StageData stageData)
        {
            if (stageData == null)
            {
                LogManager.LogError("[WaveSystem] 스테이지 데이터가 null입니다.", LogManager.LogCategory.System);
                return;
            }

            m_currentStage = stageData;
            m_currentWaveIndex = -1;
            m_activeMobCount = 0;
            m_spawnedMobCount = 0;
            
            // [수정]: 외부에서 이미 Pause()를 호출했다면 강제로 true로 설정하지 않습니다.
            // 다만, 처음 객체 생성 시 m_isSpawningAllowed는 false이므로, '이미 꺼져있는지'를 판단하기 위해 
            // m_isSpawningAllowed가 false인 경우 그대로 유지하도록 하면 처음 시작 시 작동하지 않는 문제가 생깁니다.
            // 따라서 '명시적으로 Pause가 호출되었는지'를 체크하는 로직으로 변경하거나,
            // 여기서는 m_isSpawningAllowed를 건드리지 않고 외부(Spawner)에서 제어하도록 맡깁니다.
            // 하지만 Start(StageData)는 '새 게임 시작'의 신호이므로 기본은 true여야 합니다. 
            // Spawner가 Start() 호출 직후에 다시 Pause()를 호출해주므로 이 라인을 제거하거나 그대로 둡니다.
            // Spawner 수정을 통해 해결되었으므로 여기서는 안전하게 기존 상태가 false인 경우(Pause됨) 유지하도록 합니다.
            if (m_isSpawningAllowed)
            {
                m_isSpawningAllowed = true;
            }

            m_isWaitingForNextWave = false;
            m_remainingTime = 0f;

            ProcessNextWave();
        }

        /// <summary>
        /// [설명]: 웨이브 시스템을 일시 정지 상태로 전환하며 진행 중인 모든 타이머를 중단합니다.
        /// </summary>
        public void Pause()
        {
            m_isSpawningAllowed = false;
            CancelCurrentTask();
            LogManager.Log($"[WaveSystem] 시스템 일시 중지 (남은 시간: {m_remainingTime:F2}s)", LogManager.LogCategory.System);
        }

        /// <summary>
        /// [설명]: 일시 정지된 웨이브 시스템을 다시 재개하며 중단되었던 타이머를 복구합니다.
        /// </summary>
        public void Resume()
        {
            if (m_isSpawningAllowed) return;

            m_isSpawningAllowed = true;
            
            if (m_isWaitingForNextWave)
            {
                WaitAndStartNextWaveAsync(m_remainingTime).Forget();
            }
            else
            {
                var wave = GetCurrentWaveData();
                if (wave != null && wave.duration > 0)
                {
                    RunWaveTimerAsync(m_remainingTime).Forget();
                }
                else
                {
                    // 처치제 웨이브이거나 대기 중이 아니었던 경우 상태 체크
                    CheckWaveCompletionCondition();
                }
            }
            
            LogManager.Log($"[WaveSystem] 시스템 재개 (남은 시간: {m_remainingTime:F2}s)", LogManager.LogCategory.System);
        }

        /// <summary>
        /// [설명]: 진행 중인 모든 작업을 중단하고 시스템을 초기화합니다.
        /// </summary>
        public void Stop()
        {
            m_isSpawningAllowed = false;
            CancelCurrentTask();
            m_currentStage = null;
        }

        /// <summary>
        /// [설명]: IDisposable 구현부로, 시스템 정리 시 Stop을 호출합니다.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        #endregion

        #region 상태 업데이트

        /// <summary>
        /// [설명]: 몬스터가 월드에 스폰될 때 호출하여 카운트를 갱신합니다.
        /// </summary>
        public void OnMobSpawned()
        {
            m_activeMobCount++;
            m_spawnedMobCount++;
        }

        /// <summary>
        /// [설명]: 몬스터가 사망하거나 풀로 반납될 때 호출하여 완료 조건을 검사합니다.
        /// </summary>
        public void OnMobDied()
        {
            m_activeMobCount--;
            if (m_activeMobCount < 0)
            {
                m_activeMobCount = 0;
            }

            CheckWaveCompletionCondition();
        }

        /// <summary>
        /// [설명]: 현재 시스템 상태와 웨이브 데이터를 기반으로 추가 스폰 가능 여부를 체크합니다.
        /// </summary>
        /// <param name="maxPoolLimit">최대 풀 수용 가능 인원</param>
        /// <returns>스폰 가능 여부</returns>
        public bool CanSpawn(int maxPoolLimit)
        {
            if (!IsSpawningAllowed)
            {
                return false;
            }

            if (m_activeMobCount >= maxPoolLimit)
            {
                return false;
            }

            var wave = GetCurrentWaveData();
            if (wave == null)
            {
                return false;
            }

            if (wave.duration <= 0 && wave.count > 0)
            {
                if (m_spawnedMobCount >= wave.count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// [설명]: 현재 활성화된 웨이브 정보를 가져옵니다.
        /// </summary>
        public WaveData GetCurrentWaveData()
        {
            if (m_currentStage == null || m_currentWaveIndex < 0 || m_currentWaveIndex >= m_currentStage.waves.Count)
            {
                return null;
            }
            return m_currentStage.waves[m_currentWaveIndex];
        }

        #endregion

        #region 웨이브 비즈니스 로직

        /// <summary>
        /// [설명]: 다음 순서의 웨이브로 진행하거나, 모든 웨이브 완료 시 스테이지 클리어를 호출합니다.
        /// </summary>
        private void ProcessNextWave()
        {
            if (m_currentStage == null)
            {
                return;
            }

            m_currentWaveIndex++;

            // 모든 웨이브 종료 체크
            if (m_currentWaveIndex >= m_currentStage.waves.Count)
            {
                m_isSpawningAllowed = false;
                LogManager.Log($"[WaveSystem] 스테이지 {m_currentStage.stageId} 클리어!", LogManager.LogCategory.System);
                OnStageCleared?.Invoke(m_currentStage.stageId);
                return;
            }

            WaveData wave = m_currentStage.waves[m_currentWaveIndex];

            m_spawnedMobCount = 0;

            LogManager.Log($"[WaveSystem] 웨이브 진행: {wave.waveId} (스테이지 내 인덱스: {m_currentWaveIndex}/{m_currentStage.waves.Count - 1})", LogManager.LogCategory.System);
            LogManager.Log($"[WaveSystem] 웨이브 디테일 - 타입: {(wave.duration > 0 ? $"시간제({wave.duration}s)" : $"처치제({wave.count}마리)")}", LogManager.LogCategory.System);
            OnWaveStarted?.Invoke(wave);

            if (wave.duration > 0)
            {
                RunWaveTimerAsync(wave.duration).Forget();
            }
        }

        /// <summary>
        /// [설명]: 현재 웨이브의 종료 조건(시간 경과 또는 목표 처치 수 달성)을 상시 검사합니다.
        /// </summary>
        private void CheckWaveCompletionCondition()
        {
            if (m_isWaitingForNextWave || !m_isSpawningAllowed)
            {
                return;
            }

            WaveData wave = GetCurrentWaveData();
            if (wave == null)
            {
                return;
            }

            bool isWaveComplete = false;

            // 처치제 웨이브 (duration <= 0) 체크
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
        /// [설명]: 웨이브를 공식적으로 종료하고 휴식 시간 루틴으로 전환합니다.
        /// </summary>
        private void CompleteCurrentWave()
        {
            if (m_isWaitingForNextWave)
            {
                return;
            }

            LogManager.Log($"[WaveSystem] 웨이브 {CurrentWaveId} 종료 조건 달성", LogManager.LogCategory.System);

            OnWaveCompleted?.Invoke(GetCurrentWaveData());

            WaitAndStartNextWaveAsync().Forget();
        }

        /// <summary>
        /// [설명]: 시간제 웨이브를 처리하기 위한 비동기 타이머 루틴입니다. 
        /// 프레임 단위로 남은 시간을 차감하여 일시 정지/재개가 가능하게 합니다.
        /// </summary>
        private async UniTaskVoid RunWaveTimerAsync(float duration)
        {
            m_remainingTime = duration;
            CancelCurrentTask();
            m_waveCts = new CancellationTokenSource();
            var token = m_waveCts.Token;

            try
            {
                while (m_remainingTime > 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    m_remainingTime -= UnityEngine.Time.deltaTime;
                }

                if (!m_isWaitingForNextWave && m_isSpawningAllowed)
                {
                    CompleteCurrentWave();
                }
            }
            catch (OperationCanceledException)
            {
                // Pause 시 취소되지만 m_remainingTime은 보존됨
            }
        }

        /// <summary>
        /// [설명]: 웨이브 종료 후 다음 웨이브 시작 전까지 설정된 시간만큼 대기합니다.
        /// </summary>
        private async UniTaskVoid WaitAndStartNextWaveAsync(float? customDuration = null)
        {
            m_isWaitingForNextWave = true;

            float waitDuration = customDuration ?? 3.0f;
            if (customDuration == null)
            {
                var currentWave = GetCurrentWaveData();
                if (currentWave != null)
                {
                    waitDuration = currentWave.waitDuration;
                }
            }
            
            m_remainingTime = waitDuration;

            CancelCurrentTask();
            m_waveCts = new CancellationTokenSource();
            var token = m_waveCts.Token;

            try
            {
                while (m_remainingTime > 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    m_remainingTime -= UnityEngine.Time.deltaTime;
                }

                if (m_isSpawningAllowed)
                {
                    m_isWaitingForNextWave = false;
                    ProcessNextWave();
                }
            }
            catch (OperationCanceledException)
            {
                // 취소됨
            }
        }

        /// <summary>
        /// [설명]: 현재 진행 중인 비동기 타이머 작업을 안전하게 중단합니다.
        /// </summary>
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