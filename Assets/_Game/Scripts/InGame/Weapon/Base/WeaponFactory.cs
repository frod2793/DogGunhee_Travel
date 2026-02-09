using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Weapon.Base;
using InGame.Weapon.Controllers;

namespace InGame.Weapon
{
    /// <summary>
    /// 무기 컨트롤러를 생성하는 팩토리 클래스입니다.
    /// SkillCode를 기반으로 적절한 IWeaponController 구현체를 반환하며, OCP(개방-폐쇄 원칙)를 준수합니다.
    /// </summary>
    public static class WeaponFactory
    {
        #region 내부 상태 및 변수

        /// <summary>
        /// SkillCode와 생성 함수를 매핑하는 딕셔너리
        /// </summary>
        private static readonly Dictionary<string, Func<IWeaponController>> s_controllerFactories 
            = new Dictionary<string, Func<IWeaponController>>();

        #endregion

        #region 팩토리 초기화 및 관리

        /// <summary>
        /// 게임 시작 시 기본 무기들을 팩토리에 등록합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterDefaultWeapons()
        {
            // [기획 데이터] XML의 'key' 속성과 일치하도록 팩토리 매핑 등록
            Register("WP_BONE", () => new BoneWeaponController());
            Register("WP_PUNCH", () => new CatPunchWeaponController());
            Register("WP_PEARL", () => new JinjooWeaponController());
            Register("WP_LANDING", () => new ShieldWeaponController());
            Register("WP_FRIENDS", () => new FriendsWeaponController());
            Register("WP_BOOMERANG", () => new InGame.Weapon.Core.WeaponController(new InGame.Weapon.Strategies.BoomerangStrategy()));
            Register("WP_FIRE", () => new FlameWeaponController());
            Register("WP_BALL", () => new InGame.Weapon.Core.WeaponController(new InGame.Weapon.Strategies.OrbitProjectileStrategy()));
            
            // WP_INK: 플레이어 추적형 장판(먹물) 효과를 위해 FollowingAuraStrategy 사용
            Register("WP_INK", () => new InGame.Weapon.Core.WeaponController(
                new InGame.Weapon.Strategies.FollowingAuraStrategy<BlackWaterEffect>()
            ));
            
            Register("WP_SMELL", () => new SmellWeaponController());

            LogManager.Log("[WeaponFactory] 모든 기본 무기 컨트롤러 등록 완료", LogManager.LogCategory.Weapon);
        }

        /// <summary>
        /// 새로운 무기 컨트롤러 팩토리를 동적으로 등록합니다.
        /// </summary>
        /// <param name="skillCode">무기의 스킬 코드</param>
        /// <param name="factory">생성 델리게이트</param>
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
        /// 등록된 팩토리 정보를 모두 삭제합니다.
        /// </summary>
        public static void ClearRegistrations()
        {
            s_controllerFactories.Clear();
        }

        #endregion

        #region 무기 인스턴스 생성 로직

        /// <summary>
        /// SkillCode를 기반으로 실제 장착 가능한 무기 컨트롤러를 생성하고 초기화합니다.
        /// </summary>
        /// <param name="data">무기 데이터 (SO)</param>
        /// <param name="playerTransform">플레이어 위치 정보</param>
        /// <param name="targetProvider">타겟 방향 제공자</param>
        /// <returns>생성된 무기 컨트롤러 객체</returns>
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

            // 인스턴스 생성
            IWeaponController controller = factory();
            
            // 초기화 (Init)
            controller.Init(data, playerTransform, targetProvider);

            LogManager.Log($"[WeaponFactory] '{data.WeaponName}' 생성 및 초기화 완료", LogManager.LogCategory.Weapon);
            return controller;
        }

        /// <summary>
        /// 특정 무기가 공장에 등록되어 있는지 여부를 확인합니다.
        /// </summary>
        public static bool IsRegistered(string skillCode)
        {
            return s_controllerFactories.ContainsKey(skillCode);
        }

        #endregion
    }
}
