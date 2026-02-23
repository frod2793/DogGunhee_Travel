using System;

namespace InGame.Data
{
    #region 데이터 모델 (DTO)
    /// <summary>
    /// [설명]: 플레이어의 핵심 데이터를 담는 전송 객체(DTO)입니다. 
    /// ScriptableObject 의존성을 제거한 순수 C# 클래스(POCO)입니다.
    /// </summary>
    [Serializable]
    public class PlayerDataDTO
    {
        public string Nickname;
        public string UID;
        public int Currency1; // Gold
        public int Currency2; // Diamond
        public float Experience;
        public int Level;
        public int SelectCharacterIndex;
        public int SelectSkinIndex;
        public int SelectWeaponIndex;
        public int TotalMobKillCount;
        public int NowPlayMobKillCount;
        public int IngameCoin;

        /// <summary>
        /// [설명]: 기본값으로 데이터를 초기화합니다.
        /// </summary>
        public void Initialize(string nickname, string uid)
        {
            this.Nickname = nickname;
            this.UID = uid;
            this.Currency1 = 0;
            this.Currency2 = 0;
            this.Experience = 0f;
            this.Level = 1;
            this.SelectCharacterIndex = 0;
            this.SelectWeaponIndex = 0;
        }
    }
    #endregion
}
