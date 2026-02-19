using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.ObjectPool; 
using InGame.Weapon.Base; 
using InGame.Weapon.Controllers; 

// 전략 패턴 및 코어 네임스페이스
using InGame.Weapon.Core;
using InGame.Weapon.Strategies;

namespace InGame.Weapon
{
    /// <summary>
    /// [설명]: 무기 컨트롤러(IWeaponController)를 생성하는 정적 팩토리 클래스입니다.
    /// SkillCode(String)를 키(Key)로 사용하여 적절한 무기 인스턴스를 반환하며, 런타임 초기화 시 기본 무기들을 등록합니다.
    /// </summary>
    public static class WeaponFactory
    {
        #region 내부 상태

        /// <summary> 스킬 코드와 생성 함수(Delegate)를 매핑하는 딕셔너리 </summary>
        private static readonly Dictionary<string, Func<IWeaponController>> s_factoryMap 
            = new Dictionary<string, Func<IWeaponController>>();

        #endregion

        #region 초기화 및 등록

        /// <summary>
        /// [설명]: 게임 구동 시(씬 로드 전) 기본 무기들을 팩토리에 등록합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterDefaultWeapons()
        {
            s_factoryMap.Clear();

            // 1. 전용 컨트롤러 클래스를 사용하는 무기들
            Register("WP_BONE",     () => new BoneWeaponController());
            Register("WP_PUNCH",    () => new CatPunchWeaponController());
            Register("WP_PEARL",    () => new JinjooWeaponController());
            Register("WP_LANDING",  () => new ShieldWeaponController());
            Register("WP_FRIENDS",  () => new FriendsWeaponController());
            Register("WP_FIRE",     () => new FlameWeaponController());
            Register("WP_SMELL",    () => new SmellWeaponController());

            // 2. 전략 패턴(Strategy)을 사용하는 공용 컨트롤러 무기들
            // 부메랑 전략
            Register("WP_BOOMERANG", () => new WeaponController(new BoomerangStrategy()));
            
            // 공전체(위성) 전략
            Register("WP_BALL",      () => new WeaponController(new OrbitProjectileStrategy()));

            // 추적 장판(먹물) 전략
            // 제네릭 전략 패턴 활용 예시
            Register("WP_INK",       () => new WeaponController(new FollowingAuraStrategy<BlackWaterEffect>()));

            LogManager.Log($"[WeaponFactory] 초기화 완료. 총 {s_factoryMap.Count}개의 무기 등록됨.", LogManager.LogCategory.Weapon);
        }

        /// <summary>
        /// [설명]: 새로운 무기 컨트롤러 생성 로직을 동적으로 등록합니다.
        /// </summary>
        /// <param name="skillCode">고유 스킬 코드 (XML/Data Key)</param>
        /// <param name="creator">생성 델리게이트 (람다)</param>
        public static void Register(string skillCode, Func<IWeaponController> creator)
        {
            if (string.IsNullOrEmpty(skillCode))
            {
                LogManager.LogWarning("[WeaponFactory] 등록 실패: SkillCode가 비어있습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            if (creator == null)
            {
                LogManager.LogWarning($"[WeaponFactory] 등록 실패: '{skillCode}'의 생성 함수가 null입니다.", LogManager.LogCategory.Weapon);
                return;
            }

            if (s_factoryMap.ContainsKey(skillCode))
            {
                LogManager.LogWarning($"[WeaponFactory] 중복 경고: '{skillCode}'가 이미 등록되어 있어 덮어씁니다.", LogManager.LogCategory.Weapon);
            }

            s_factoryMap[skillCode] = creator;
        }

        /// <summary>
        /// [설명]: 등록된 모든 팩토리 정보를 초기화합니다. (테스트 또는 리셋 용도)
        /// </summary>
        public static void ClearRegistrations()
        {
            s_factoryMap.Clear();
        }

        #endregion

        #region 생성 로직

        /// <summary>
        /// [설명]: SkillCode를 기반으로 무기 컨트롤러를 생성하고 초기화(Init)하여 반환합니다.
        /// </summary>
        /// <param name="data">무기 설정 데이터 (ScriptableObject)</param>
        /// <param name="ownerTransform">무기 소유자(플레이어) Transform</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        /// <param name="targetProvider">타겟 방향 제공 함수</param>
        /// <returns>초기화된 무기 컨트롤러 (실패 시 null)</returns>
        public static IWeaponController CreateController(
            WeaponDataSO data,
            Transform ownerTransform,
            WeaponPoolManager poolManager,
            Func<Vector3> targetProvider)
        {
            // 1. 유효성 검사
            if (data == null)
            {
                LogManager.LogError("[WeaponFactory] 생성 실패: WeaponDataSO가 null입니다.", LogManager.LogCategory.Weapon);
                return null;
            }

            if (ownerTransform == null)
            {
                LogManager.LogError("[WeaponFactory] 생성 실패: Owner Transform이 null입니다.", LogManager.LogCategory.Weapon);
                return null;
            }

            string skillCode = data.SkillCode;

            // 2. 팩토리 조회
            if (!s_factoryMap.TryGetValue(skillCode, out var creator))
            {
                LogManager.LogError($"[WeaponFactory] 생성 실패: '{skillCode}'에 해당하는 무기가 등록되지 않았습니다.", LogManager.LogCategory.Weapon);
                return null;
            }

            try
            {
                // 3. 인스턴스 생성
                IWeaponController controller = creator();

                // 4. 의존성 주입 및 초기화
                controller.Init(data, ownerTransform, poolManager, targetProvider);

                return controller;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[WeaponFactory] '{skillCode}' 생성 중 예외 발생: {ex.Message}", LogManager.LogCategory.Weapon);
                return null;
            }
        }

        /// <summary>
        /// [설명]: 해당 스킬 코드의 무기가 팩토리에 등록되어 있는지 확인합니다.
        /// </summary>
        public static bool IsRegistered(string skillCode)
        {
            return !string.IsNullOrEmpty(skillCode) && s_factoryMap.ContainsKey(skillCode);
        }

        #endregion
    }
}