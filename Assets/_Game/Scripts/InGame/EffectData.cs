using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame
{
    /// <summary>
    /// 이펙트 타입과 프리팹을 연결하는 데이터 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class EffectMapping
    {
        #region 1. 변수 및 프로퍼티

        [Tooltip("이펙트의 타입 (Enum)")]
        [FormerlySerializedAs("type")] // 기존 데이터 보존
        [SerializeField] private EffectType m_type;

        [Tooltip("생성할 이펙트 프리팹")]
        [FormerlySerializedAs("prefab")] // 기존 데이터 보존
        [SerializeField] private GameObject m_prefab;

        public EffectType Type => m_type;
        public GameObject Prefab => m_prefab;

        #endregion
    }

    /// <summary>
    /// 게임 내 모든 이펙트 정보를 관리하는 데이터 에셋(SO)입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectData", menuName = "VamserLike/Effect Data", order = 0)]
    public class EffectData : ScriptableObject
    {
        #region 1. 인스펙터 설정 (Inspector)

        [Header("데이터 설정")]
        [Tooltip("등록할 이펙트 리스트")]
        [FormerlySerializedAs("effects")] // 기존 데이터 보존
        [SerializeField] private List<EffectMapping> m_effects;

        #endregion

        #region 2. 프로퍼티 (데이터 접근)

        /// <summary>
        /// 외부에서 접근 가능한 이펙트 리스트 (읽기 전용)
        /// </summary>
        public IReadOnlyList<EffectMapping> Effects => m_effects;

        #endregion
    }
}