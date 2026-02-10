using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 캐릭터별 고유 데이터를 관리하는 ScriptableObject(SO)입니다.
    /// <br/> 구체적인 클래스 상속 없이 데이터 설정만으로 캐릭터의 차별화가 가능하도록 설계되었습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "Game/Player/CharacterConfig")]
    public class CharacterConfigSO : ScriptableObject
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 기본 정보")]
        [FormerlySerializedAs("CharacterName")] 
        [SerializeField, Tooltip("UI 및 로그에 표시될 캐릭터 식별 이름")] 
        private string m_characterName;

        [Header("2. 초기 능력치 (Stats)")]
        [FormerlySerializedAs("BaseMaxHealth")] 
        [SerializeField, Range(1, 1000), Tooltip("게임 시작 시 적용되는 기본 최대 체력")] 
        private float m_baseMaxHealth = 100f;
        
        [FormerlySerializedAs("BaseMoveSpeed")] 
        [SerializeField, Range(1, 20), Tooltip("기본 이동 속도")] 
        private float m_baseMoveSpeed = 5f;
        
        [FormerlySerializedAs("BaseAttackPower")] 
        [SerializeField, Range(0, 1000), Tooltip("기본 공격력")] 
        private float m_baseAttackPower = 10f;

        [Header("3. 사운드 설정 (Audio)")]
        [FormerlySerializedAs("HitSoundKey")] 
        [SerializeField, Tooltip("피격 시 재생될 효과음 키")] 
        private SoundKeys m_hitSoundKey = SoundKeys.playerHit;
        
        [FormerlySerializedAs("DeathSoundKey")] 
        [SerializeField, Tooltip("사망 시 재생될 효과음 키")] 
        private SoundKeys m_deathSoundKey = SoundKeys.PlayerDeth;

        [Header("4. 시각 효과 (Visuals)")]
        [FormerlySerializedAs("HitEffectPrefab")] 
        [SerializeField, Tooltip("피격 시 생성될 이펙트 프리팹 (옵션)")] 
        private GameObject m_hitEffectPrefab;

        #endregion

        #region 2. 공개 프로퍼티 (Accessors)

        /// <summary>
        /// 캐릭터의 표시 이름입니다.
        /// </summary>
        public string CharacterName => m_characterName;

        /// <summary>
        /// 초기 최대 체력값입니다.
        /// </summary>
        public float BaseMaxHealth => m_baseMaxHealth;

        /// <summary>
        /// 초기 이동 속도입니다.
        /// </summary>
        public float BaseMoveSpeed => m_baseMoveSpeed;

        /// <summary>
        /// 초기 공격력입니다.
        /// </summary>
        public float BaseAttackPower => m_baseAttackPower;

        /// <summary>
        /// 피격음 사운드 키입니다.
        /// </summary>
        public SoundKeys HitSoundKey => m_hitSoundKey;

        /// <summary>
        /// 사망음 사운드 키입니다.
        /// </summary>
        public SoundKeys DeathSoundKey => m_deathSoundKey;

        /// <summary>
        /// 피격 이펙트 프리팹입니다. (null일 수 있음)
        /// </summary>
        public GameObject HitEffectPrefab => m_hitEffectPrefab;

        #endregion
    }
}