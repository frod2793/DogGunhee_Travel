using UnityEngine;

namespace Vamser_like
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "GameData/PlayerData")]
    public class PlayerData : ScriptableObject
    {
        public string nickname; // 플레이어 닉네임
        public string UID; // 6자리의 랜덤 숫자
        public int currency1; // 재화1 (예: 코인)
        public int currency2; // 재화2 (예: 보석)
        public float experience; // 경험치량
        public int level; // 플레이어 레벨
        public int selectCharacterIndex; // 선택한 캐릭터 인덱스
        public int selelcWeaponIndex; // 선택한 무기 인덱스
        public int totalmobKillCount; // 죽인 몹 수
        public int nowPlayMObkillCOunt; // 현재 죽인 몹 수
        public int ingameCoin; // 게임 중 획득한 코인
        
        // 기본값으로 초기화
        public void InitializePlayerData(string playerName, string uid)
        {
            nickname = playerName;
            Debug.Log("nickname: " + nickname);
            UID = uid;
            currency1 = 0; // 초기 재화1
            currency2 = 0; // 초기 재화2
            experience = 0f; // 초기 경험치
            level = 1; // 초기 레벨
        }
    }
}