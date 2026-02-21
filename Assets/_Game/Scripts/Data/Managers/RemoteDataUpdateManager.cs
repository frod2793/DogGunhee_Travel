using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using InGame.Services;

namespace InGame.Data.Managers
{
    /// <summary>
    /// [설명]: 리모트 데이터(구글 시트 기반 JSON)의 업데이트를 조율하고 데이터베이스를 갱신하는 매니저 클래스입니다.
    /// </summary>
    public class RemoteDataUpdateManager : MonoBehaviour, IRemoteDataUpdateService
    {
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
