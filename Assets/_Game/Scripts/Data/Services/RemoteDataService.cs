using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using InGame.Data;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 구글 스프레드시트 데이터를 원격에서 가져오고 로컬에 캐싱하는 서비스입니다.
    /// </summary>
    public class RemoteDataService
    {
        #region 내부 필드

        private const string k_BaseUrl = "https://script.google.com/macros/s/AKfycbwTSH5RK_8H_oN2DmfIHrs4E3AKm-4yD-U0GrLAvm4StEoJefTNk3h3gULQz_7oTyns/exec"; // 실제 배포 시 GAS URL로 대체 필요
        private readonly string m_savePath;

        #endregion

        #region 초기화

        public RemoteDataService()
        {
            m_savePath = Application.persistentDataPath + "/DataCache";
            if (!Directory.Exists(m_savePath))
            {
                Directory.CreateDirectory(m_savePath);
            }
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// [설명]: 리모트와 로컬의 데이터를 비교하고 필요 시 업데이트합니다.
        /// </summary>
        /// <param name="dataType">데이터 종류 (Skill, Stage 등)</param>
        public async UniTask<bool> CheckAndUpdateDataAsync(string dataType, CancellationToken ct = default, bool force = false)
        {
            try
            {
                // 1. 서버 버전 정보 확인 (실제 구현 시 API 파라미터로 구분)
                string requestUrl = $"{k_BaseUrl}?action=get_version&type={dataType}";
                string serverVersion = await GetTextFromUrlAsync(requestUrl, ct);

                if (string.IsNullOrEmpty(serverVersion))
                {
                    Debug.LogWarning($"[RemoteDataService] 서버에서 {dataType} 버전 정보를 가져오지 못했습니다.");
                    return false;
                }

                // 2. 로컬 버전 확인
                string localVersionPath = Path.Combine(m_savePath, $"{dataType}_version.txt");
                string localVersion = File.Exists(localVersionPath) ? File.ReadAllText(localVersionPath) : string.Empty;

                // 3. 버전 비교 및 다운로드 (force가 true면 무조건 다운로드)
                if (localVersion != serverVersion || force)
                {
                    if (force)
                    {
                        Debug.Log($"<color=cyan>[RemoteDataService]</color> <b>{dataType}</b> 강제 동기화 트리거됨.");
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>[RemoteDataService]</color> <b>{dataType}</b> 업데이트 발견! (구버전: {localVersion} -> 신버전: {serverVersion})");
                    }
                    
                    string dataUrl = $"{k_BaseUrl}?action=get_data&type={dataType}";
                    string jsonData = await GetTextFromUrlAsync(dataUrl, ct);

                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        // JSON 데이터 및 버전 저장
                        File.WriteAllText(Path.Combine(m_savePath, $"{dataType}.json"), jsonData);
                        File.WriteAllText(localVersionPath, serverVersion);
                        
                        Debug.Log($"<color=green>[RemoteDataService]</color> <b>{dataType}</b> 데이터 다운로드 및 로컬 캐시 저장 완료.");
                        return true;
                    }
                }
                else
                {
                    Debug.Log($"<color=white>[RemoteDataService]</color> {dataType} 데이터가 이미 최신 상태입니다. (버전: {localVersion})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteDataService] {dataType} 업데이트 중 오류 발생: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// [설명]: 로컬에 캐시된 JSON 데이터를 읽어옵니다.
        /// </summary>
        public string GetLocalJsonData(string dataType)
        {
            string path = Path.Combine(m_savePath, $"{dataType}.json");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            return null;
        }

        #endregion

        #region 내부 메서드

        private async UniTask<string> GetTextFromUrlAsync(string url, CancellationToken ct)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                await request.SendWebRequest().WithCancellation(ct);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[RemoteDataService] URL 요청 실패 ({url}): {request.error}");
                    return null;
                }

                return request.downloadHandler.text;
            }
        }

        #endregion
    }
}
