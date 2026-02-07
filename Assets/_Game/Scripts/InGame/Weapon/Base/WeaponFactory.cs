using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Weapon.Base;
using InGame.Weapon.Controllers;

namespace InGame.Weapon
{
    /// <summary>
    /// 무기 컨트롤러를 생성하는 팩토리 클래스입니다.
    /// SkillCode를 기반으로 적절한 IWeaponController 구현체를 반환합니다.
    /// OCP(개방-폐쇄 원칙)를 준수하여 새 무기 추가 시 기존 코드 수정을 최소화합니다.
    /// </summary>
    public static class WeaponFactory
    {
        #region 팩토리 등록 시스템

        // SkillCode와 생성 함수를 매핑하는 딕셔너리
        private static readonly Dictionary<string, Func<IWeaponController>> s_controllerFactories 
            = new Dictionary<string, Func<IWeaponController>>();

        /// <summary>
        /// 새로운 무기 컨트롤러 팩토리를 등록합니다.
        /// </summary>
        /// <param name="skillCode">무기의 스킬 코드</param>
        /// <param name="factory">컨트롤러 생성 함수</param>
        public static void Register(string skillCode, Func<IWeaponController> factory)
        {
            if (string.IsNullOrEmpty(skillCode))
            {
                Debug.LogWarning("[WeaponFactory] SkillCode가 비어있어 등록할 수 없습니다.");
                return;
            }

            if (s_controllerFactories.ContainsKey(skillCode))
            {
                Debug.LogWarning($"[WeaponFactory] '{skillCode}'이(가) 이미 등록되어 있습니다. 덮어씁니다.");
            }

            s_controllerFactories[skillCode] = factory;
        }

        /// <summary>
        /// 등록된 모든 팩토리를 초기화합니다.
        /// </summary>
        public static void ClearRegistrations()
        {
            s_controllerFactories.Clear();
        }

        #endregion

        #region 기본 무기 등록 (정적 초기화)

        /// <summary>
        /// 기본 무기들을 등록합니다. 게임 시작 시 1회 호출하세요.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterDefaultWeapons()
        {
            // [Refactoring] XML의 'key' 속성(SkillCode)과 정확히 일치하도록 등록합니다.
            Register("WP_BONE", () => new BoneWeaponController());
            Register("WP_PUNCH", () => new CatPunchWeaponController());
            Register("WP_PEARL", () => new JinjooWeaponController());
            Register("WP_LANDING", () => new ShieldWeaponController());
            Register("WP_FRIENDS", () => new FriendsWeaponController());
            Register("WP_BOOMERANG", () => new BoomerangWeaponController());
            Register("WP_FIRE", () => new FlameWeaponController());
            Register("WP_BALL", () => new BallplayWeaponController());
            Register("WP_INK", () => new BlackWaterWeaponController());
            Register("WP_SMELL", () => new SmellWeaponController());
            
            LogManager.Log("[WeaponFactory] 모든 실질 무기 컨트롤러 등록 완료 (XML Key 호환)", LogManager.LogCategory.Weapon);
        }

        #endregion

        #region 무기 생성

        /// <summary>
        /// SkillCode를 기반으로 무기 컨트롤러를 생성합니다.
        /// </summary>
        /// <param name="data">무기 데이터</param>
        /// <param name="playerTransform">플레이어 트랜스폼</param>
        /// <param name="targetProvider">타겟 위치 제공 함수</param>
        /// <returns>생성된 무기 컨트롤러 (등록되지 않은 경우 null)</returns>
        public static IWeaponController CreateController(
            WeaponDataSO data, 
            Transform playerTransform, 
            Func<Vector3> targetProvider)
        {
            if (data == null)
            {
                Debug.LogWarning("[WeaponFactory] WeaponDataSO가 null입니다.");
                return null;
            }

            string skillCode = data.SkillCode;

            if (!s_controllerFactories.TryGetValue(skillCode, out var factory))
            {
                Debug.LogWarning($"[WeaponFactory] '{skillCode}' 무기가 등록되지 않았습니다.");
                return null;
            }

            IWeaponController controller = factory();
            controller.Init(data, playerTransform, targetProvider);
            
            LogManager.Log($"[WeaponFactory] '{data.WeaponName}' 무기 생성 완료", LogManager.LogCategory.Weapon);
            return controller;
        }

        /// <summary>
        /// 특정 SkillCode가 등록되어 있는지 확인합니다.
        /// </summary>
        public static bool IsRegistered(string skillCode)
        {
            return s_controllerFactories.ContainsKey(skillCode);
        }

        #endregion
    }
}
