using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 진주(Pearl) 무기의 전투 판정(쿨타임) 및 트레일러 시각 효과를 설정하는 뷰 컴포넌트입니다.
    /// WeaponPoolManager나 무기 프리팹에 부착되어 로직(Controller/Logic)에서 참조합니다.
    /// </summary>
    public class PearlWeaponView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("전투 튜닝")]
        [Tooltip("동일한 몬스터에게 다시 데미지를 입히기까지의 최소 대기 시간(초)입니다.")]
        [FormerlySerializedAs("HitCooldown")]
        [Range(0.05f, 2.0f)] // 너무 짧으면 연산 부하, 너무 길면 성능 저하 방지
        [SerializeField] private float m_hitCooldown = 0.5f;

        [Header("트레일(꼬리) 설정")]
        [Tooltip("트레일이 화면에 유지되는 시간(초)입니다.")]
        [FormerlySerializedAs("TrailTime")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float m_trailTime = 0.3f;

        [Tooltip("트레일 시작 지점(투사체 쪽)의 두께입니다.")]
        [FormerlySerializedAs("TrailStartWidth")]
        [Range(0.01f, 1.0f)]
        [SerializeField] private float m_trailStartWidth = 0.2f;

        [Tooltip("트레일 끝 지점(사라지는 쪽)의 두께입니다.")]
        [FormerlySerializedAs("TrailEndWidth")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float m_trailEndWidth = 0.0f;

        [Header("트레일 색상")]
        [Tooltip("기본 상태(Lv1)의 트레일 색상입니다.")]
        [FormerlySerializedAs("TrailColorLv1")]
        [SerializeField] private Color m_trailColorLv1 = new Color(1f, 1f, 1f, 0.5f);

        [Tooltip("진화 상태(Lv2 이상)의 트레일 색상입니다.")]
        [FormerlySerializedAs("TrailColorLv2")]
        [SerializeField] private Color m_trailColorLv2 = new Color(1f, 0f, 1f, 0.5f);

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// 동일 대상 타격 쿨타임 (초)
        /// </summary>
        public float HitCooldown => m_hitCooldown;

        /// <summary>
        /// 트레일 지속 시간
        /// </summary>
        public float TrailTime => m_trailTime;

        /// <summary>
        /// 트레일 시작 폭
        /// </summary>
        public float TrailStartWidth => m_trailStartWidth;

        /// <summary>
        /// 트레일 끝 폭
        /// </summary>
        public float TrailEndWidth => m_trailEndWidth;

        /// <summary>
        /// 기본 트레일 색상
        /// </summary>
        public Color TrailColorLv1 => m_trailColorLv1;

        /// <summary>
        /// 진화 트레일 색상
        /// </summary>
        public Color TrailColorLv2 => m_trailColorLv2;

        #endregion
    }
}