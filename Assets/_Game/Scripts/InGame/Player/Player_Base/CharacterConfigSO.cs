using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 캐릭터별 고유 데이터를 관리하는 ScriptableObject입니다.
    /// 구체적인 클래스 상속 없이 데이터 설정만으로 캐릭터 차별화가 가능하도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "Game/Player/CharacterConfig")]
    public class CharacterConfigSO : ScriptableObject
    {
        #region 설정 데이터

        [Header("기본 정보")]
        [FormerlySerializedAs("CharacterName")] [SerializeField] private string m_characterName;

        [Header("사운드 설정")]
        [FormerlySerializedAs("HitSoundKey")] [SerializeField] private SoundKeys m_hitSoundKey = SoundKeys.playerHit;
        [FormerlySerializedAs("DeathSoundKey")] [SerializeField] private SoundKeys m_deathSoundKey = SoundKeys.PlayerDeth;

        [Header("초기 능력치")]
        [FormerlySerializedAs("BaseMaxHealth")] [SerializeField] private float m_baseMaxHealth = 100f;
        [FormerlySerializedAs("BaseMoveSpeed")] [SerializeField] private float m_baseMoveSpeed = 5f;
        [FormerlySerializedAs("BaseAttackPower")] [SerializeField] private float m_baseAttackPower = 10f;

        [Header("시각 효과")]
        [FormerlySerializedAs("HitEffectPrefab")] [SerializeField] private GameObject m_hitEffectPrefab;

        #endregion

        #region 프로퍼티

        public string CharacterName => m_characterName;
        public SoundKeys HitSoundKey => m_hitSoundKey;
        public SoundKeys DeathSoundKey => m_deathSoundKey;
        public float BaseMaxHealth => m_baseMaxHealth;
        public float BaseMoveSpeed => m_baseMoveSpeed;
        public float BaseAttackPower => m_baseAttackPower;
        public GameObject HitEffectPrefab => m_hitEffectPrefab;

        #endregion
    }
}
