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
using UnityEngine;

namespace Google.Play.AppUpdate.Samples.AppUpdateDemo.Scripts
{
    /// <summary>
    /// Provides controls for downloading an app update
    /// </summary>
    public class UpdateDownloader : MonoBehaviour
    {
        public UpdateDownloadDisplay display;
        public bool showSize;

        public void Start()
        {
            display.BindColor(display.successColor, AppUpdateStatus.Downloaded);
            display.BindColor(display.errorColor, AppUpdateStatus.Failed);
        }

        public void SetDownloadDisplayActive(Boolean isActive)
        {
            display.SetProgressBarDisplayActive(isActive);
        }

        public void SetInitialStatus()
        {
            display.SetScrolling(false);
            SetDownloadDisplayActive(false);
        }

        public void SetDownloadProgress(ulong bytesDownloaded, float downloadProgress)
        {
            if (showSize)
            {
                var downloadSize = string.Format("{0}", display.FormatSize(bytesDownloaded));
                display.SetStatusText(String.Format("{0} downloaded({1}%)", downloadSize, downloadProgress * 100));
            }
            else
            {
                display.SetStatusText(String.Format("{0}%", downloadProgress * 100));
            }

            display.SetProgress(downloadProgress);
        }
    }
}