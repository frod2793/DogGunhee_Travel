using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame
{
    #region 데이터 구조

    /// <summary>
    /// 몬스터 스폰 정보를 담는 데이터 클래스입니다.
    /// </summary>
    [Serializable]
    public class MobSpawnData
    {
        [Tooltip("Addressable 프리팹 키")] public string mobKey;

        [Tooltip("등장 확률 가중치")] public int spawnRate;
    }

    /// <summary>
    /// 개별 웨이브의 설정 데이터를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    public class WaveData
    {
        [Tooltip("웨이브 고유 번호")] public int waveId;

        [Tooltip("웨이브 이름 (UI 표시용)")] public string waveName;

        [Tooltip("웨이브 지속 시간 (초)")] public float duration;

        [Tooltip("몬스터 스폰 간격 (초)")] public float spawnInterval;

        [Tooltip("웨이브 종료 후 다음 웨이브까지의 대기 시간 (초)")]
        public float waitDuration;

        [Tooltip("웨이브 내 총 몬스터 스폰 수 (duration이 0일 때 사용)")]
        public int count;

        [Tooltip("보스 웨이브 여부")] public bool isBossWave;

        [Tooltip("해당 웨이브에서 등장하는 몬스터 목록")] public List<MobSpawnData> mobs = new List<MobSpawnData>();
    }

    /// <summary>
    /// 스테이지 전체 정보를 구성하는 데이터 클래스입니다.
    /// </summary>
    [Serializable]
    public class StageData
    {
        [Tooltip("스테이지 고유 번호")] public int stageId;

        [Tooltip("스테이지 이름")] public string stageName;

        [Tooltip("스테이지에 포함된 웨이브 목록")] public List<WaveData> waves = new List<WaveData>();
    }

    #endregion

    /// <summary>
    /// 리모트(구글 시트/JSON)로부터 스테이지 정보를 로드하고 관리하는 데이터베이스 클래스입니다.
    /// ScriptableObject를 통해 에디터 및 런타임에서 데이터를 제공합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Game/Stage/StageDatabase")]
    public class StageDatabase : ScriptableObject
    {
        #region 필드 설정

        [Header("스테이지 데이터")] 
        [Tooltip("로드된 스테이지 목록입니다.")] 
        [SerializeField] private List<StageData> m_stages = new List<StageData>();

        private const string k_DataType = "Stage";

        #endregion

        #region 프로퍼티

        /// <summary>모든 스테이지 데이터 목록 (읽기 전용)</summary>
        public IReadOnlyList<StageData> Stages => m_stages;

        #endregion

        #region Unity 라이프사이클

        private void OnEnable()
        {
            // 초기화 시 로컬 캐시에서 로드
            LoadFromLocalCache();
        }

        #endregion

        #region 데이터 로드 로직

        /// <summary>
        /// [설명]: 로컬 캐시에서 데이터를 로드합니다.
        /// </summary>
        public void LoadFromLocalCache()
        {
            string jsonData = GetCachedJson();
            if (!string.IsNullOrEmpty(jsonData))
            {
                LoadDataFromJSON(jsonData);
            }
        }

        /// <summary>
        /// [설명]: JSON 데이터를 기반으로 스테이지 목록을 구축합니다.
        /// </summary>
        public void LoadDataFromJSON(string jsonData)
        {
            if (string.IsNullOrEmpty(jsonData)) return;

            try
            {
                var wrapper = JsonUtility.FromJson<InGame.Data.SheetDataWrapper<InGame.Data.StageDataDTO>>(jsonData);
                if (wrapper == null || wrapper.data == null) return;

                m_stages.Clear();

                foreach (var stageDto in wrapper.data)
                {
                    StageData stage = new StageData
                    {
                        stageId = stageDto.id,
                        stageName = stageDto.name
                    };

                    if (stageDto.Waves != null)
                    {
                        foreach (var waveDto in stageDto.Waves)
                        {
                            WaveData wave = new WaveData
                            {
                                waveId = waveDto.id,
                                waveName = string.IsNullOrEmpty(waveDto.name) ? $"Wave {waveDto.id}" : waveDto.name,
                                duration = waveDto.duration,
                                spawnInterval = waveDto.spawnInterval,
                                waitDuration = waveDto.wait > 0 ? waveDto.wait : 3.0f,
                                count = waveDto.count,
                                isBossWave = waveDto.isBossWave
                            };

                            if (waveDto.Mobs != null)
                            {
                                foreach (var mobDto in waveDto.Mobs)
                                {
                                    wave.mobs.Add(new MobSpawnData
                                    {
                                        mobKey = mobDto.key,
                                        spawnRate = mobDto.rate
                                    });
                                }
                            }

                            stage.waves.Add(wave);
                        }
                    }

                    m_stages.Add(stage);
                }

                Debug.Log($"<color=white>[StageDatabase]</color> JSON 데이터 로드 완료 (스테이지 총 <b>{m_stages.Count}</b>개)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageDatabase] JSON 파싱 중 오류 발생: {ex.Message}");
            }
        }

        private string GetCachedJson()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "DataCache", $"{k_DataType}.json");
            if (System.IO.File.Exists(path))
            {
                return System.IO.File.ReadAllText(path);
            }
            return string.Empty;
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// 특정 ID를 가진 스테이지 데이터를 반환합니다.
        /// </summary>
        public StageData GetStage(int id)
        {
            return m_stages.Find(s => s.stageId == id);
        }

        #endregion
    }
}
