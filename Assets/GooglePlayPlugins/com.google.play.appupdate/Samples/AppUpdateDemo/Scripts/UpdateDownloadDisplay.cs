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
using System.Collections.Generic;
using Google.Play.Common.LoadingScreen;
using UnityEngine;
using UnityEngine.UI;

namespace Google.Play.AppUpdate.Samples.AppUpdateDemo.Scripts
{
    /// <summary>
    /// Provides controls and status displays for downloading App Update
    /// </summary>
    public class UpdateDownloadDisplay : MonoBehaviour
    {
        public Text statusText;
        public Image colorTint;
        public LoadingBar loadingBar;
        public ScrollingFillAnimator scrollingFill;
        public Color errorColor;
        public Color successColor;
        public Color neutralColor;


        private readonly IDictionary<AppUpdateStatus, Color> _colorsByStatus =
            new Dictionary<AppUpdateStatus, Color>();

        private const float ActiveScrollSpeed = 2.5f;

        /// <summary>
        /// Configure the display so that the loading bar color changes when the status is set to the specified one.
        /// </summary>
        public void BindColor(Color color, AppUpdateStatus status)
        {
            _colorsByStatus.Add(status, color);
        }

        public void SetProgressBarDisplayActive(bool progressBarActive)
        {
            scrollingFill.gameObject.SetActive(progressBarActive);
            colorTint.color = neutralColor;
            loadingBar.gameObject.SetActive(progressBarActive);
            SetScrolling(progressBarActive);
        }

        public void SetScrolling(bool scrolling)
        {
            scrollingFill.ScrollSpeed = scrolling ? ActiveScrollSpeed : 0f;
        }

        public void SetInitialStatus(bool isDownloaded)
        {
            SetProgress(0f);
            statusText.text = isDownloaded
                ? AppUpdateStatus.Downloaded.ToString()
                : AppUpdateStatus.Pending.ToString();
        }

        public void SetStatus(AppUpdateStatus status)
        {
            SetStatusText(status.ToString());

            if (status != AppUpdateStatus.Downloading)
            {
                SetScrolling(false);
            }

            if (_colorsByStatus.ContainsKey(status))
            {
                colorTint.color = _colorsByStatus[status];
            }
        }

        public void SetStatusText(string text)
        {
            statusText.text = text;
        }

        public void SetProgress(float progress)
        {
            loadingBar.SetProgress(progress);
        }

        public string FormatSize(ulong numBytes)
        {
            if (numBytes < 2)
            {
                return numBytes + " B";
            }

            var units = new[] {"B", "KB", "MB", "GB", "TB", "PB", "EB"};
            var unitIndex = (int) Math.Floor(Math.Log10(numBytes)) / 3;
            var shiftedBytes = numBytes / Math.Pow(1000, unitIndex);
            return string.Format("{0:0.##} {1}", shiftedBytes, units[unitIndex]);
        }
    }
}