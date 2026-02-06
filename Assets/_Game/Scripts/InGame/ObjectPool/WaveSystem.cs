using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.ObjectPool
{
    /// <summary>
    /// 웨이브 진행 및 스폰 규칙을 관리하는 POCO 클래스입니다.
    /// MonoBehaviour에 의존하지 않고 순수 C# 로직만 포함합니다.
    /// </summary>
    public class WaveSystem
    {
        #region 이벤트

        /// <summary>
        /// 새로운 웨이브가 시작될 때 발생합니다. (스폰할 몹 수 전달)
        /// </summary>
        public event Action<int> OnWaveStarted;

        /// <summary>
        /// 웨이브가 종료될 때 발생합니다. (클리어한 웨이브 번호 전달)
        /// </summary>
        public event Action<int> OnWaveCompleted;

        #endregion

        #region 속성

        /// <summary>
        /// 현재 웨이브 번호
        /// </summary>
        public int CurrentWave { get; private set; }

        /// <summary>
        /// 현재 활성화된 몹 수
        /// </summary>
        public int ActiveMobCount { get; private set; }

        /// <summary>
        /// 이번 웨이브에서 스폰되어야 할 몹 수
        /// </summary>
        public int CurrentWaveMobCount { get; private set; }

        /// <summary>
        /// 스폰이 허용된 상태인지 여부
        /// </summary>
        public bool IsSpawningAllowed { get; private set; }

        #endregion

        #region 설정 데이터

        private readonly int m_initialMobCount;
        private readonly int m_mobIncreasePerWave;
        private readonly int m_maxMobCount;
        private readonly float m_waveDelay;

        #endregion

        #region 내부 상태

        private CancellationTokenSource m_waveCts;
        private bool m_isWaitingForNextWave;

        #endregion

        #region 생성자

        /// <summary>
        /// WaveSystem을 초기화합니다.
        /// </summary>
        /// <param name="initialMobCount">첫 웨이브 몹 수</param>
        /// <param name="mobIncreasePerWave">웨이브당 몹 증가량</param>
        /// <param name="maxMobCount">최대 몹 수 제한</param>
        /// <param name="waveDelay">웨이브 간 딜레이 (초)</param>
        public WaveSystem(
            int initialMobCount = 20,
            int mobIncreasePerWave = 5,
            int maxMobCount = 100,
            float waveDelay = 3f)
        {
            m_initialMobCount = initialMobCount;
            m_mobIncreasePerWave = mobIncreasePerWave;
            m_maxMobCount = maxMobCount;
            m_waveDelay = waveDelay;

            CurrentWave = 0;
            ActiveMobCount = 0;
            CurrentWaveMobCount = m_initialMobCount;
            IsSpawningAllowed = false;
            m_isWaitingForNextWave = false;
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// 웨이브 시스템을 시작합니다. 첫 웨이브를 즉시 시작합니다.
        /// </summary>
        public void Start()
        {
            IsSpawningAllowed = true;
            CurrentWave = 0;
            CurrentWaveMobCount = m_initialMobCount;

            StartNextWave();
        }

        /// <summary>
        /// 웨이브 시스템을 일시 정지합니다.
        /// </summary>
        public void Pause()
        {
            IsSpawningAllowed = false;
        }

        /// <summary>
        /// 웨이브 시스템을 재개합니다.
        /// </summary>
        public void Resume()
        {
            IsSpawningAllowed = true;

            // 몹이 0마리인 상태에서 재개되었다면 다음 웨이브 시작
            if (ActiveMobCount <= 0 && !m_isWaitingForNextWave)
            {
                CheckAndTriggerNextWave();
            }
        }

        /// <summary>
        /// 웨이브 시스템을 종료하고 리소스를 정리합니다.
        /// </summary>
        public void Stop()
        {
            IsSpawningAllowed = false;
            CancelWaveTask();
        }

        /// <summary>
        /// 몹이 스폰되었을 때 호출합니다.
        /// </summary>
        public void OnMobSpawned()
        {
            ActiveMobCount++;
        }

        /// <summary>
        /// 몹이 죽었을 때 호출합니다.
        /// </summary>
        public void OnMobDied()
        {
            ActiveMobCount--;
            if (ActiveMobCount < 0) ActiveMobCount = 0;

            CheckAndTriggerNextWave();
        }

        /// <summary>
        /// 현재 웨이브 상태에서 스폰 가능한지 확인합니다.
        /// </summary>
        public bool CanSpawn()
        {
            return IsSpawningAllowed && ActiveMobCount < m_maxMobCount;
        }

        /// <summary>
        /// 리소스를 정리합니다.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        #endregion

        #region 내부 메서드

        private void StartNextWave()
        {
            CurrentWave++;
            CurrentWaveMobCount = Mathf.Min(m_initialMobCount + (CurrentWave - 1) * m_mobIncreasePerWave, m_maxMobCount);

            LogManager.Log($"[WaveSystem] Wave {CurrentWave} Start (MobCount: {CurrentWaveMobCount})", LogManager.LogCategory.ObjectPoolSpawner);

            OnWaveStarted?.Invoke(CurrentWaveMobCount);
        }

        private void CheckAndTriggerNextWave()
        {
            if (ActiveMobCount <= 0 && IsSpawningAllowed && !m_isWaitingForNextWave)
            {
                OnWaveCompleted?.Invoke(CurrentWave);
                ScheduleNextWaveAsync().Forget();
            }
        }

        private async UniTaskVoid ScheduleNextWaveAsync()
        {
            m_isWaitingForNextWave = true;
            CancelWaveTask();
            m_waveCts = new CancellationTokenSource();

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(m_waveDelay), ignoreTimeScale: true, cancellationToken: m_waveCts.Token);

                if (IsSpawningAllowed)
                {
                    StartNextWave();
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 취소
            }
            finally
            {
                m_isWaitingForNextWave = false;
            }
        }

        private void CancelWaveTask()
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
