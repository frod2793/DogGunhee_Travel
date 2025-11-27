// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Google.Play.AppUpdate.Samples.AppUpdateDemo.Scripts
{
    /// <summary>
    /// Provides controls and status displays to start an in-app update flow of the specified update type.
    /// </summary>
    public class StartUpdateFlowAction : MonoBehaviour
    {
        public Text statusText;
        public Button startUpdateFlowButton;
        public RectTransform buttonOutline;
        public GetAppUpdateInfoAction getAppUpdateInfoAction;
        public UpdateDownloader updateDownloader;
        public AppUpdateType appUpdateType;

        private AppUpdateManager _appUpdateManager;

        public void Start()
        {
            if (appUpdateType == AppUpdateType.Flexible)
            {
                updateDownloader.SetInitialStatus();
            }

            _appUpdateManager = new AppUpdateManager();
            ChangeButtonDisplay(false);
            startUpdateFlowButton.onClick.AddListener(ButtonEventStartUpdateFlow);
        }

        protected void Update()
        {
            // Provides an interface to test via key press.
            if (Input.anyKeyDown && Input.GetKeyDown(KeyCode.M) && appUpdateType == AppUpdateType.Immediate)
            {
                StartCoroutine(StartUpdateCoroutine());
            }

            if (Input.anyKeyDown && Input.GetKeyDown(KeyCode.F) && appUpdateType == AppUpdateType.Flexible)
            {
                StartCoroutine(StartUpdateCoroutine());
            }

            if (getAppUpdateInfoAction != null && getAppUpdateInfoAction.appUpdateInfoResult != null)
            {
                ChangeButtonDisplay(true);
            }
        }

        public void ButtonEventStartUpdateFlow()
        {
            StartCoroutine(StartUpdateCoroutine());
        }

        private void ChangeButtonDisplay(Boolean isShown)
        {
            startUpdateFlowButton.gameObject.SetActive(isShown);
            buttonOutline.gameObject.SetActive(isShown);
            statusText.gameObject.SetActive(isShown);
        }

        private IEnumerator StartUpdateCoroutine()
        {
            if (!getAppUpdateInfoAction)
            {
                throw new NullReferenceException(
                    "Start Update Flow Failed: Cannot find getAppUpdateInfoAction");
            }

            if (appUpdateType == AppUpdateType.Flexible && !updateDownloader)
            {
                throw new NullReferenceException(
                    "Start Update Flow Failed: Cannot find updateDownloader");
            }

            // Reset display status
            SetStatusText("(none)");

            var appUpdateOptions = appUpdateType == AppUpdateType.Immediate
                ? AppUpdateOptions.ImmediateAppUpdateOptions()
                : AppUpdateOptions.FlexibleAppUpdateOptions();
            var appUpdateInfo = getAppUpdateInfoAction.appUpdateInfoResult;

            Debug.Log("Starting update request");
            var startUpdateRequest = _appUpdateManager.StartUpdate(appUpdateInfo, appUpdateOptions);

            if (appUpdateType == AppUpdateType.Flexible)
            {
                if (startUpdateRequest.IsDone)
                {
                    SetStatusText(startUpdateRequest.Status.ToString());
                    yield break;
                }

                if (appUpdateInfo.UpdateAvailability == UpdateAvailability.UpdateAvailable)
                {
                    // Reset progress bar status to 0.
                    updateDownloader.SetDownloadProgress(0, 0);
                }

                while (!startUpdateRequest.IsDone)
                {
                    if (startUpdateRequest.Status == AppUpdateStatus.Canceled ||
                        startUpdateRequest.Error != AppUpdateErrorCode.NoError)
                    {
                        break;
                    }

                    updateDownloader.SetDownloadDisplayActive(true);
                    updateDownloader.display.SetScrolling(true);
                    SetStatusText(startUpdateRequest.Status.ToString());
                    updateDownloader.SetDownloadProgress(startUpdateRequest.BytesDownloaded,
                        startUpdateRequest.DownloadProgress);
                    yield return null;
                }

                updateDownloader.display.SetStatus(startUpdateRequest.Status);
            }

            yield return startUpdateRequest;
            Debug.Log("Start update request finished");

            switch (startUpdateRequest.Status)
            {
                case AppUpdateStatus.Canceled:
                    SetStatusText("Update operation canceled by user.");
                    yield break;
                case AppUpdateStatus.Failed:
                    if (appUpdateInfo.AppUpdateStatus == AppUpdateStatus.Downloaded)
                    {
                        SetStatusText(string.Format(
                            "Download already present! Call complete update to start installation: {0}",
                            startUpdateRequest.Error));
                    }
                    else
                    {
                        SetStatusText(string.Format("Update operation failed due to {0}. Try refreshing AppUpdateInfo.",
                            startUpdateRequest.Error));
                    }

                    yield break;
                case AppUpdateStatus.Downloading:
                    SetStatusText("Update is being downloading.");
                    yield break;
                case AppUpdateStatus.Downloaded:
                    updateDownloader.SetDownloadProgress(startUpdateRequest.TotalBytesToDownload, 1f);
                    SetStatusText("Update is Downloaded. Call complete update to start installation");
                    yield break;
                case AppUpdateStatus.Unknown:
                    SetStatusText("Update operation result unknown.");
                    yield break;
                default:
                    SetStatusText("Unexpected update operation status.");
                    yield break;
            }
        }

        private void SetStatusText(string text)
        {
            statusText.text = string.Format("Status: {0}", text);
        }
    }
}