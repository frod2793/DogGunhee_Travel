using System;
using System.Collections.Generic;

namespace InGame.Data
{
    #region 데이터 모델 (DTO)

    /// <summary>
    /// [설명]: 구글 스프레드시트에서 가져온 원본 데이터를 담는 루트 컨테이너 DTO입니다.
    /// </summary>
    [Serializable]
    public class SheetDataWrapper<T>
    {
        public List<T> data;
        public string version;
    }

    /// <summary>
    /// [설명]: 리모트에서 가져올 스킬 정보의 메타데이터를 담는 DTO입니다.
    /// </summary>
    [Serializable]
    public class SkillDescriptionDTO
    {
        public string key;
        public string Name;
        public string SkillType;
        public string UpgradeItemCode;
        public string FlavorText;
        public string Description;
        public string Stats;
        public string WeaponAddressableKey;
        public List<StatValueDTO> StatValues;
        public EvolutionDTO Evolution;
        public List<UpgradeLevelDTO> Upgrades;
    }

    [Serializable]
    public class StatValueDTO
    {
        public string name;
        public float value;
    }

    [Serializable]
    public class EvolutionDTO
    {
        public string Name;
        public string FlavorText;
    }

    [Serializable]
    public class UpgradeLevelDTO
    {
        public int num;
        public string stat;
        public float value;
        public string mode;
    }

    /// <summary>
    /// [설명]: 리모트에서 가져올 스테이지 및 웨이브 정보를 담는 DTO입니다.
    /// </summary>
    [Serializable]
    public class StageDataDTO
    {
        public int id;
        public string name;
        public List<WaveDataDTO> Waves;
    }

    [Serializable]
    public class WaveDataDTO
    {
        public int id;
        public string name;
        public float duration;
        public float spawnInterval;
        public float wait;
        public int count;
        public bool isBossWave;  // [추가] 보스 웨이브 여부
        public string assetKey;  // [추가] 어드레서블 에셋 키
        public List<MobSpawnDTO> Mobs;
    }

    [Serializable]
    public class MobSpawnDTO
    {
        public string key;
        public int rate;
    }

    #endregion
}
