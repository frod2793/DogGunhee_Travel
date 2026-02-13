using UnityEngine;

namespace InGame.Mob.Data
{
    /// <summary>
    /// 몬스터의 기본 스탯 정보를 담는 ScriptableObject입니다.
    /// <br/> 기획자가 데이터를 쉽게 수정하고 관리할 수 있도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMobStats", menuName = "Game/Mob/Stats")]
    public class MobStatsData : ScriptableObject
    {
        #region 핵심 스탯 데이터
        
        [Header("기본 정보")]
        [SerializeField] private string m_mobName = "New Mob";
        public string MobName => m_mobName;

        [Header("전투 스탯")]
        [SerializeField] private float m_maxHp = 100f;
        public float MaxHp => m_maxHp;

        [SerializeField] private float m_moveSpeed = 3f;
        public float MoveSpeed => m_moveSpeed;

        [SerializeField] private float m_attackDamage = 10f;
        public float AttackDamage => m_attackDamage;

        [SerializeField] private float m_attackSpeed = 1f;
        public float AttackSpeed => m_attackSpeed;

        [SerializeField] private float m_attackRange = 1.5f;
        public float AttackRange => m_attackRange;

        [SerializeField, Range(0f, 1f)] private float m_stunResistance = 0f;
        public float StunResistance => m_stunResistance;

        [Header("AI 설정")]
        [SerializeField] private float m_searchRange = 8f;
        public float SearchRange => m_searchRange;

        [SerializeField] private Vector2 m_wanderWaitRange = new Vector2(1f, 3f);
        public Vector2 WanderWaitRange => m_wanderWaitRange;

        #endregion
    }
}
