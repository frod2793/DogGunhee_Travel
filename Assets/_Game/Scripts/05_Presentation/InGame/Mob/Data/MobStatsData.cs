using UnityEngine;

namespace InGame.Mob.Data
{
    /// <summary>
    /// [설명]: 몬스터의 기본 스탯 정보를 저장하고 관리하는 데이터 컨테이너(ScriptableObject)입니다.
    /// 기획자가 유니티 에디터에서 데이터를 쉽게 수정하여 밸런싱을 조절할 수 있도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMobStats", menuName = "Game/Mob/Stats")]
    public class MobStatsData : ScriptableObject
    {
        #region 핵심 스탯 데이터

        [Header("기본 정보")]
        [SerializeField, Tooltip("몬스터의 이름")]
        private string m_mobName = "New Mob";

        /// <summary>
        /// [설명]: 몬스터 이름
        /// </summary>
        public string MobName => m_mobName;

        [Header("전투 스탯")]
        [SerializeField, Tooltip("최대 체력")]
        private float m_maxHp = 100f;

        /// <summary>
        /// [설명]: 최대 체력
        /// </summary>
        public float MaxHp => m_maxHp;

        [SerializeField, Tooltip("이동 속도")]
        private float m_moveSpeed = 3f;

        /// <summary>
        /// [설명]: 초당 이동 거리
        /// </summary>
        public float MoveSpeed => m_moveSpeed;

        [SerializeField, Tooltip("공격력")]
        private float m_attackDamage = 10f;

        /// <summary>
        /// [설명]: 기본 공격 데미지
        /// </summary>
        public float AttackDamage => m_attackDamage;

        [SerializeField, Tooltip("공격 속도")]
        private float m_attackSpeed = 1f;

        /// <summary>
        /// [설명]: 초당 공격 횟수
        /// </summary>
        public float AttackSpeed => m_attackSpeed;

        [SerializeField, Tooltip("공격 사거리")]
        private float m_attackRange = 1.5f;

        /// <summary>
        /// [설명]: 공격이 도달하는 거리
        /// </summary>
        public float AttackRange => m_attackRange;

        [SerializeField, Range(0f, 1f), Tooltip("경직 저항력 (0: 저항 없음, 1: 완전 저항)")]
        private float m_stunResistance = 0f;

        /// <summary>
        /// [설명]: 상태 이상(스턴 등)에 대한 저항 확률
        /// </summary>
        public float StunResistance => m_stunResistance;

        [Header("AI 설정")]
        [SerializeField, Tooltip("대상 탐색 범위")]
        private float m_searchRange = 8f;

        /// <summary>
        /// [설명]: 플레이어나 타겟을 인식하는 범위
        /// </summary>
        public float SearchRange => m_searchRange;

        [SerializeField, Tooltip("배회 중 대기 시간 범위 (최소, 최대)")]
        private Vector2 m_wanderWaitRange = new Vector2(1f, 3f);

        /// <summary>
        /// [설명]: 이동 후 다음 행동까지의 대기 시간 랜덤 범위
        /// </summary>
        public Vector2 WanderWaitRange => m_wanderWaitRange;

        #endregion
    }
}