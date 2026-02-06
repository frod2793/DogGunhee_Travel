using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 캐릭터별 고유 데이터를 관리하는 ScriptableObject입니다.
    /// 구체적인 클래스 상속 없이 데이터 설정만으로 캐릭터 차별화가 가능하도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "Game/Player/CharacterConfig")]
    public class CharacterConfigSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string CharacterName;
        
        [Header("사운드 키")]
        public SoundKeys HitSoundKey = SoundKeys.playerHit;
        public SoundKeys DeathSoundKey = SoundKeys.PlayerDeth;

        [Header("초기 스탯")]
        public float BaseMaxHealth = 100f;
        public float BaseMoveSpeed = 5f;
        public float BaseAttackPower = 10f;

        [Header("이펙트 설정 (필요 시)")]
        public GameObject HitEffectPrefab;
    }
}
