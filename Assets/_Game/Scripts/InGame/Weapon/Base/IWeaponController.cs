using UnityEngine;
using InGame.ObjectPool; // WeaponPoolManager 참조

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 모든 무기(투사체, 근접, 오라 등)의 동작과 상태를 제어하는 컨트롤러 인터페이스입니다.
    /// <br/> PlayerWeaponManager가 이 인터페이스를 통해 무기를 통합 관리합니다.
    /// </summary>
    public interface IWeaponController
    {
        #region 1. 식별자 및 데이터 (Identity & Data)

        /// <summary>
        /// 무기를 고유하게 식별하는 스킬 코드 (예: "WP_MAGIC_WAND")
        /// </summary>
        string SkillCode { get; }

        /// <summary>
        /// UI 표시에 사용되는 무기 이름
        /// </summary>
        string WeaponName { get; }

        /// <summary>
        /// UI 표시에 사용되는 아이콘 스프라이트
        /// </summary>
        Sprite Thumbnail { get; }

        /// <summary>
        /// 현재 적용된 스킬 데이터 (데미지, 쿨타임 등 수치 정보)
        /// </summary>
        SkillData SkillData { get; set; }

        #endregion

        #region 2. 상태 및 레벨 (State & Progression)

        /// <summary>
        /// 현재 무기의 레벨
        /// </summary>
        int CurrentLevel { get; }

        /// <summary>
        /// 도달 가능한 최대 레벨
        /// </summary>
        int MaxLevel { get; }

        /// <summary>
        /// 무기가 진화(Evolution) 단계를 거쳤는지 여부
        /// </summary>
        bool IsEvolved { get; }

        #endregion

        #region 3. 생명주기 (Lifecycle)

        /// <summary>
        /// 무기 컨트롤러를 초기화하고 의존성을 주입합니다.
        /// </summary>
        /// <param name="data">무기 설정 데이터 (ScriptableObject)</param>
        /// <param name="owner">무기를 소유한 주체(플레이어)의 Transform</param>
        /// <param name="poolManager">투사체 생성에 사용할 오브젝트 풀 매니저</param>
        /// <param name="targetProvider">공격 방향/타겟을 결정하는 델리게이트 함수</param>
        void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager, System.Func<Vector3> targetProvider);

        /// <summary>
        /// 매 프레임 호출되어 쿨타임 계산 및 공격 로직을 수행합니다.
        /// </summary>
        /// <param name="deltaTime">프레임 경과 시간 (Time.deltaTime)</param>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 매 프레임 후반부에 호출되어 무기 비주얼의 위치 동기화 등을 수행합니다.
        /// </summary>
        void OnLateUpdate();

        /// <summary>
        /// 무기가 제거되거나 게임이 종료될 때 리소스를 정리합니다.
        /// </summary>
        void Dispose();

        #endregion

        #region 4. 동작 (Actions)

        /// <summary>
        /// 지정된 방향으로 공격(투사체 발사, 휘두르기 등)을 수행합니다.
        /// </summary>
        /// <param name="direction">공격 방향 벡터 (정규화됨)</param>
        void Attack(Vector3 direction);

        /// <summary>
        /// 무기 레벨을 1단계 상승시키고 스탯을 갱신합니다.
        /// </summary>
        void LevelUp();

        #endregion
    }
}