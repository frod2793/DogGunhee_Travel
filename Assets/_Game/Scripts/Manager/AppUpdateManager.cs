using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

#if UNITY_ANDROID
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 구글 플레이 스토어를 통한 앱 강제 업데이트를 관리하는 클래스입니다.
    /// 안드로이드 플랫폼에서 앱 시작 시 업데이트 가용성을 체크하고 즉시 업데이트를 유도합니다.
    /// </summary>
    public class AppUpdateManager : MonoBehaviour, IAppUpdateService
    {
        #region 업데이트 로직

        /// <summary>
        /// [설명]: 앱 업데이트가 필요한지 비동기로 체크합니다.
        /// 안드로이드 플랫폼이 아니거나 에디터 환경인 경우 즉시 완료됩니다.
        /// </summary>
        public async UniTask CheckForUpdateAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var appUpdateManager = new Google.Play.AppUpdate.AppUpdateManager();

                // 1. 업데이트 정보 요청
                PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> appUpdateInfoOperation = 
                    appUpdateManager.GetAppUpdateInfo();

                // 2. 작업 완료 대기
                await appUpdateInfoOperation;

                if (!appUpdateInfoOperation.IsSuccessful)
                {
                    HandleUpdateFailure($"업데이트 정보 조회 실패: {appUpdateInfoOperation.Error}");
                    return;
                }

                // 3. 결과 추출
                AppUpdateInfo appUpdateInfo = appUpdateInfoOperation.GetResult();

                // 4. 즉시 업데이트 옵션 생성
                var updateOptions = AppUpdateOptions.ImmediateAppUpdateOptions();

                // 업데이트가 가능하고 해당 옵션이 허용된 경우
                if (appUpdateInfo.UpdateAvailability == UpdateAvailability.UpdateAvailable &&
                    appUpdateInfo.IsUpdateTypeAllowed(updateOptions))
                {
                    // 5. 업데이트 시작 요청
                    var startUpdateOperation = appUpdateManager.StartUpdate(appUpdateInfo, updateOptions);

                    // 업데이트 완료(또는 실패/취소)까지 대기
                    await startUpdateOperation;

                    // 6. 결과 확인
                    if (!startUpdateOperation.IsDone)
                    {
                        HandleUpdateFailure($"업데이트 시작 실패 또는 취소됨. Error: {startUpdateOperation.Error}");
                    }
                }
            }
            catch (Exception e)
            {
                HandleUpdateFailure($"업데이트 확인 중 예외 발생: {e.Message}");
            }
#else
            await UniTask.CompletedTask;
#endif
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// [설명]: 업데이트 프로세스 중 발생한 오류를 처리합니다.
        /// </summary>
        private void HandleUpdateFailure(string message)
        {
            Debug.LogError($"[AppUpdateManager] {message}");
        }

        #endregion
    }
}