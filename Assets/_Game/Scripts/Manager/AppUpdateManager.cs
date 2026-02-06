using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

#if UNITY_ANDROID
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif

namespace InGame.Manager
{
    public class AppUpdateManager : MonoBehaviour
    {
        public static AppUpdateManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async UniTask CheckForUpdateAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var appUpdateManager = new Google.Play.AppUpdate.AppUpdateManager();

                // 1. 업데이트 정보 요청 (Operation을 받음)
                PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> appUpdateInfoOperation = 
                    appUpdateManager.GetAppUpdateInfo();

                // 2. 작업이 끝날 때까지 대기 (UniTask가 CustomYieldInstruction을 await 함)
                await appUpdateInfoOperation;

                if (!appUpdateInfoOperation.IsSuccessful)
                {
                    HandleUpdateFailure($"업데이트 정보 조회 실패: {appUpdateInfoOperation.Error}");
                    return;
                }

                // 3. 결과 추출
                AppUpdateInfo appUpdateInfo = appUpdateInfoOperation.GetResult();

                // 4. 즉시 업데이트 옵션 생성 (수정된 부분: Enum 대신 Options 객체 사용)
                var updateOptions = AppUpdateOptions.ImmediateAppUpdateOptions();

                // 업데이트가 가능하고, 해당 옵션이 허용된 경우
                if (appUpdateInfo.UpdateAvailability == UpdateAvailability.UpdateAvailable &&
                    appUpdateInfo.IsUpdateTypeAllowed(updateOptions))
                {
                    // 5. 업데이트 시작 요청
                    var startUpdateOperation = appUpdateManager.StartUpdate(appUpdateInfo, updateOptions);

                    // 업데이트 작업 대기 (사용자가 업데이트를 취소하거나 실패할 때까지)
                    await startUpdateOperation;

                    // 6. 결과 확인
                    if (!startUpdateOperation.IsDone)
                    {
                         // startUpdateOperation.Error 또는 startUpdateOperation.GetResult()로 상태 확인 가능
                        HandleUpdateFailure($"업데이트 시작 실패 또는 취소됨. Error: {startUpdateOperation.Error}");
                    }
                    else
                    {
                        // 즉시 업데이트가 성공하면 앱이 재시작되므로 이 코드는 보통 실행되지 않음
                        // 하지만 Resume 상황 등을 고려해 처리 가능
                    }
                }
            }
            catch (Exception e)
            {
                HandleUpdateFailure($"업데이트 확인 중 예외 발생: {e.Message}");
            }
#else
            // 에디터나 다른 플랫폼에서는 통과
            await UniTask.CompletedTask;
#endif
        }

        private void HandleUpdateFailure(string message)
        {
            Debug.LogError($"[AppUpdateManager] {message}");
      //      Application.Quit();
        }
    }
}