using UnityEngine;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기 컨트롤러가 공통적으로 가져야 할 기능을 정의하는 인터페이스입니다.
    /// </summary>
    public interface IWeaponController
    {
        #region 식별자 및 데이터

        /// <summary>
        /// 무기 식별용 스킬 코드 (예: WP_BONE)
        /// </summary>
        string SkillCode { get; }

        /// <summary>
        /// 무기의 이름
        /// </summary>
        string WeaponName { get; }

        /// <summary>
        /// 기획 데이터 상의 스킬 정보
        /// </summary>
        SkillData SkillData { get; set; }

        /// <summary>
        /// 무기의 썸네일 이미지
        /// </summary>
        Sprite Thumbnail { get; }

        #endregion

        #region 레벨 및 상태

        /// <summary>
        /// 현재 무기 레벨
        /// </summary>
        int CurrentLevel { get; }

        /// <summary>
        /// 최대 강화 가능 레벨
        /// </summary>
        int MaxLevel { get; }

        /// <summary>
        /// 무기가 진화(Evolved) 상태인지 여부
        /// </summary>
        bool IsEvolved { get; }

        #endregion

        #region 생명주기 및 업데이트

        /// <summary>
        /// 무기 컨트롤러를 초기화합니다.
        /// </summary>
        /// <param name="data">무기 데이터</param>
        /// <param name="owner">무기를 소유한 대상의 Transform</param>
        /// <param name="getTargetDirection">공격 방향을 결정하는 함수</param>
        void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection);

        /// <summary>
        /// 매 프레임 업데이트 로직을 수행합니다.
        /// </summary>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 다른 Update 로직이 끝난 후 호출되는 물리/위치 보정 로직입니다.
        /// </summary>
        void OnLateUpdate();

        #endregion

        #region 무기 동작 및 관리

        /// <summary>
        /// 무기의 레벨을 1단계 높입니다.
        /// </summary>
        void LevelUp();

        /// <summary>
        /// 무기 사용을 중단하고 관련 리소스를 정리합니다.
        /// </summary>
        void Dispose();

        /// <summary>
        /// 특정 방향으로 공격을 수행합니다.
        /// </summary>
        void Attack(Vector3 direction);

        #endregion
    }
}
