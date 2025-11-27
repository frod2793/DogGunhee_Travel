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
    /// Provides controls for In-App Update complete update flow functionality.
    /// </summary>
    public class CompleteUpdateAction : MonoBehaviour
    {
        public Text statusText;
        public Button completeUpdateButton;

        private AppUpdateManager _appUpdateManager;

        public void Start()
        {
            _appUpdateManager = new AppUpdateManager();
            completeUpdateButton.onClick.AddListener(ButtonEventCompleteUpdate);
        }

        private void Update()
        {
            // Provides an interface to test via key press.
            if (Input.anyKeyDown && Input.GetKeyDown(KeyCode.C))
            {
                StartCoroutine(CompleteUpdateCoroutine());
            }
        }

        private void ButtonEventCompleteUpdate()
        {
            StartCoroutine(CompleteUpdateCoroutine());
        }

        private IEnumerator CompleteUpdateCoroutine()
        {
            Debug.Log("Starting complete update");
            var result = _appUpdateManager.CompleteUpdate();
            yield return result;

            if (result.Error != AppUpdateErrorCode.NoError)
            {
                var failedStatusMessage = String.Format("CompleteUpdate Failed: {0}", result.Error.ToString());
                SetStatus(failedStatusMessage);
            }

            // This code here should never be reached if the CompleteUpdate call was successful since the app should be
            // restarted.
        }

        private void SetStatus(string text)
        {
            statusText.text = string.Format("Status: {0}", text);
        }
    }
}