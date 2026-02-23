using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame
{
    /// <summary>
    /// [설명]: 이펙트 타입(Enum)과 이펙트 프리팹(GameObject)을 1:1로 매핑하는 데이터 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class EffectMapping
    {
        #region 내부 변수 및 프로퍼티

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
    /// [설명]: 게임 내 모든 이펙트 정보를 관리하는 데이터 에셋(ScriptableObject)입니다.
    /// EffectType에 해당하는 프리팹을 조회할 수 있는 리스트를 제공합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectData", menuName = "VamserLike/Effect Data", order = 0)]
    public class EffectData : ScriptableObject
    {
        #region 에디터 설정

        [Header("데이터 설정")]
        [Tooltip("등록할 이펙트 리스트")]
        [FormerlySerializedAs("effects")] // 기존 데이터 보존
        [SerializeField] private List<EffectMapping> m_effects;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 외부에서 접근 가능한 이펙트 리스트 (읽기 전용)
        /// </summary>
        public IReadOnlyList<EffectMapping> Effects => m_effects;

        #endregion
    }
}