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
    /// Provides controls and status displays for In-App Update get update info functionality.
    /// </summary>
    public class GetAppUpdateInfoAction : MonoBehaviour
    {
        public Text statusText;
        public Button getAppUpdateInfoButton;
        public AppUpdateInfo appUpdateInfoResult;

        private AppUpdateManager _appUpdateManager;

        public void Start()
        {
            _appUpdateManager = new AppUpdateManager();
            getAppUpdateInfoButton.onClick.AddListener(ButtonEventGetUpdateInfo);
            Debug.Log("In-App Update Unity app Initialized");
        }

        private void Update()
        {
            // Provides an interface to test via key press.
            if (Input.anyKeyDown && Input.GetKeyDown(KeyCode.I))
            {
                StartCoroutine(GetUpdateInfoCoroutine());
            }
        }

        private void ButtonEventGetUpdateInfo()
        {
            StartCoroutine(GetUpdateInfoCoroutine());
        }

        private IEnumerator GetUpdateInfoCoroutine()
        {
            var appUpdateInfo = _appUpdateManager.GetAppUpdateInfo();
            yield return appUpdateInfo;

            Debug.Log("Get update info finished");
            if (appUpdateInfo.Error != AppUpdateErrorCode.NoError)
            {
                var failedStatusMessage =
                    String.Format("GetUpdateInfoCoroutine Failed: {0}", appUpdateInfo.Error.ToString());
                SetStatus(failedStatusMessage);
                yield break;
            }

            appUpdateInfoResult = appUpdateInfo.GetResult();
            SetStatus(appUpdateInfoResult.ToString().Replace(' ', '\n'));
        }

        private void SetStatus(string text)
        {
            statusText.text = string.Format("Status: {0}", text);
        }
    }
}