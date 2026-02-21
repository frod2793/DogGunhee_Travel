using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using InGame.Services;
using InGame;

namespace InGame.Data.Managers
{
    /// <summary>
    /// [설명]: 리모트 데이터(구글 시트 기반 JSON)의 업데이트를 조율하고 데이터베이스를 갱신하는 매니저 클래스입니다.
    /// </summary>
    public class RemoteDataUpdateManager : MonoBehaviour, IRemoteDataUpdateService, InGame.Core.ISceneInitializer
    {
        #region 외부 인터페이스 구현 (ISceneInitializer)
        /// <summary>
        /// [설명]: 씬 초기화 시 호출되어 원격 데이터 동기화를 수행합니다.
        /// SceneLoader는 이 작업이 완료될 때까지 로딩 화면을 유지합니다.
        /// </summary>
        public async UniTask OnInitialize(object payload)
        {
            // [수정]: GetActiveScene().name은 로드 중인 씬이 활성화되기 전에는 이전 씬(Lobby)을 가리킬 수 있습니다.
            // SceneLoader에서 기록한 정적 타겟 씬 이름을 기반으로 스킵 여부를 정확히 판단합니다.
            string targetSceneName = SceneLoader.CurrentLoadingSceneName;

            // [최적화]: 이미 동기화가 완료되었고 인게임 씬인 경우 중복 실행 방지
            if (m_isSyncCompleted && (targetSceneName == SceneNames.RunGame || targetSceneName == SceneNames.VamSerLike))
            {
                LogManager.Log($"[RemoteDataUpdateManager] 이미 동기화가 완료되었습니다. ({targetSceneName})", LogManager.LogCategory.System);
                return;
            }

            // [최적화]: 로비 씬으로 진입하는 로딩인 경우 리모트 데이터 동기화 스킵 (인게임 진입 시에만 수행)
            if (targetSceneName == SceneNames.Lobby)
            {
                LogManager.Log("[RemoteDataUpdateManager] 로비 씬 진입 감지 - 리모트 데이터 동기화를 스킵합니다.", LogManager.LogCategory.System);
                return;
            }

            LogManager.Log($"[RemoteDataUpdateManager] 씬 초기화 단계 (Target: {targetSceneName}) - 데이터 동기화 대기 시작", LogManager.LogCategory.System);
            await UpdateAllRemoteDataAsync(ct: this.GetCancellationTokenOnDestroy());
            LogManager.Log("[RemoteDataUpdateManager] 씬 초기화 단계 - 데이터 동기화 완료", LogManager.LogCategory.System);
        }
        #endregion

        #region 외부 인터페이스 구현 (IRemoteDataUpdateService)

        // UpdateAllRemoteDataAsync는 이미 구현되어 있음
        #endregion

        #region 에디터 설정

        [Header("데이터베이스 참조")]
        [SerializeField] private SkillDatabase m_skillDatabase;
        [SerializeField] private StageDatabase m_stageDatabase;

        #endregion

        #region 내부 필드

        private RemoteDataService m_remoteService;
        private bool m_isSyncCompleted = false;

        #endregion

        #region 프로퍼티

        /// <summary> [설명]: 리모트 데이터 동기화 완료 여부입니다. </summary>
        public bool IsSyncCompleted => m_isSyncCompleted;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            
            m_remoteService = new RemoteDataService();

            // 에디터에서 할당되지 않았을 경우 리소스를 통해 탐색 시도
            if (m_skillDatabase == null) m_skillDatabase = Resources.Load<SkillDatabase>("Data/SkillDatabase");
            if (m_stageDatabase == null) m_stageDatabase = Resources.Load<StageDatabase>("Data/StageDatabase");
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// [설명]: 모든 데이터 타입에 대해 원격 업데이트를 확인하고, 변경 사항이 있으면 지정된 데이터베이스를 리로드합니다.
        /// </summary>
        public async UniTask UpdateAllRemoteDataAsync(SkillDatabase skillDb = null, StageDatabase stageDb = null, CancellationToken ct = default, bool force = false)
        {
            if (m_isSyncCompleted && !force)
            {
                Debug.Log("<color=white>[RemoteDataUpdateManager]</color> 리모트 데이터가 이미 동기화되었습니다. (Force=False)");
                return;
            }

            Debug.Log("<color=yellow>[RemoteDataUpdateManager]</color> 리모트 데이터 동기화를 시작합니다...");

            var targetSkillDb = skillDb != null ? skillDb : m_skillDatabase;
            var targetStageDb = stageDb != null ? stageDb : m_stageDatabase;

            try
            {
                // 1. Skill 데이터 동기화 및 로드 (신규 업데이트가 없더라도 항상 로컬 캐시 적용)
                bool skillUpdated = await m_remoteService.CheckAndUpdateDataAsync("Skill", ct, force);
                if (targetSkillDb != null)
                {
                    string localSkillData = m_remoteService.GetLocalJsonData("Skill");
                    if (!string.IsNullOrEmpty(localSkillData))
                    {
                        targetSkillDb.LoadDataFromJSON(localSkillData);
                    }
                    if (skillUpdated) Debug.Log("<color=green>[RemoteDataUpdateManager]</color> Skill 데이터베이스가 신규 리모트 버전으로 최신화되었습니다.");
                }

                // 2. Stage 데이터 동기화 및 로드 (신규 업데이트가 없더라도 항상 로컬 캐시 적용)
                bool stageUpdated = await m_remoteService.CheckAndUpdateDataAsync("Stage", ct, force);
                if (targetStageDb != null)
                {
                    string localStageData = m_remoteService.GetLocalJsonData("Stage");
                    if (!string.IsNullOrEmpty(localStageData))
                    {
                        targetStageDb.LoadDataFromJSON(localStageData);
                    }
                    if (stageUpdated) Debug.Log("<color=green>[RemoteDataUpdateManager]</color> Stage 데이터베이스가 신규 리모트 버전으로 최신화되었습니다.");
                }

                m_isSyncCompleted = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[RemoteDataUpdateManager]</color> 동기화 과정 중 치명적 오류 발생: {ex.Message}");
            }

            Debug.Log("<color=yellow>[RemoteDataUpdateManager]</color> 모든 데이터 동기화 프로세스가 종료되었습니다.");
        }

        #endregion
    }
}
