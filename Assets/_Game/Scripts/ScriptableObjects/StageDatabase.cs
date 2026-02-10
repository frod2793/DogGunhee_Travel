using System;
using System.Collections.Generic;
using System.Xml;
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
    /// XML로부터 스테이지 정보를 로드하고 관리하는 데이터베이스 클래스입니다.
    /// ScriptableObject를 통해 에디터 및 런타임에서 데이터를 제공합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Game/Stage/StageDatabase")]
    public class StageDatabase : ScriptableObject
    {
        #region 필드 설정

        [Header("스테이지 데이터")] [Tooltip("로드된 스테이지 목록입니다.")] [SerializeField]
        private List<StageData> m_stages = new List<StageData>();

        [Header("리소스 설정")] [Tooltip("스테이지 데이터 XML 파일의 상대 경로 (Resources 폴더 기준)")] [SerializeField]
        private string m_xmlPath = "Data/StageData";

        #endregion

        #region 프로퍼티

        /// <summary>모든 스테이지 데이터 목록 (읽기 전용)</summary>
        public IReadOnlyList<StageData> Stages => m_stages;

        #endregion

        #region Unity 라이프사이클

        private void OnEnable()
        {
            // 에디터나 런타임에서 활성화될 때 자동 로드
            LoadDataFromXML();
        }

        #endregion

        #region 데이터 로드 로직

        /// <summary>
        /// 지정된 경로의 XML 파일을 파싱하여 스테이지 데이터를 메모리에 로드합니다.
        /// </summary>
        [ContextMenu("Load From XML")]
        public void LoadDataFromXML()
        {
            TextAsset xmlFile = Resources.Load<TextAsset>(m_xmlPath);
            if (xmlFile == null)
            {
                Debug.LogWarning($"[StageDatabase] XML 파일을 찾을 수 없습니다: {m_xmlPath}");
                return;
            }

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlFile.text);

                m_stages.Clear();

                XmlNodeList stageNodes = xmlDoc.SelectNodes("Stages/Stage");
                if (stageNodes == null) return;

                foreach (XmlNode stageNode in stageNodes)
                {
                    StageData stage = new StageData
                    {
                        stageId = int.Parse(stageNode.Attributes["id"].Value),
                        stageName = stageNode.Attributes["name"].Value
                    };

                    XmlNodeList waveNodes = stageNode.SelectNodes("Wave");
                    if (waveNodes != null)
                    {
                        foreach (XmlNode waveNode in waveNodes)
                        {
                            WaveData wave = new WaveData
                            {
                                waveId = int.Parse(waveNode.Attributes["id"].Value),
                                waveName = waveNode.Attributes["name"]?.Value ??
                                           $"Wave {waveNode.Attributes["id"].Value}",
                                duration = float.Parse(waveNode.Attributes["duration"].Value),
                                spawnInterval = float.Parse(waveNode.Attributes["spawnInterval"].Value),
                                waitDuration = waveNode.Attributes["wait"] != null
                                    ? float.Parse(waveNode.Attributes["wait"].Value)
                                    : 3.0f,
                                count = waveNode.Attributes["count"] != null
                                    ? int.Parse(waveNode.Attributes["count"].Value)
                                    : 0
                            };

                            XmlNodeList mobNodes = waveNode.SelectNodes("Mob");
                            if (mobNodes != null)
                            {
                                foreach (XmlNode mobNode in mobNodes)
                                {
                                    wave.mobs.Add(new MobSpawnData
                                    {
                                        mobKey = mobNode.Attributes["key"].Value,
                                        spawnRate = int.Parse(mobNode.Attributes["rate"].Value)
                                    });
                                }
                            }

                            stage.waves.Add(wave);
                        }
                    }

                    m_stages.Add(stage);
                }

                LogManager.Log($"[StageDatabase] {m_stages.Count}개의 스테이지 데이터를 성공적으로 로드했습니다.",
                    LogManager.LogCategory.ObjectPoolSpawner);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageDatabase] XML 파싱 중 오류 발생: {ex.Message}");
            }
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
