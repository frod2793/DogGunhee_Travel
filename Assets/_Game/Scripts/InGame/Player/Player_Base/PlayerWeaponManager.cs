using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 무기 관리를 전담하는 POCO 클래스입니다.
    /// [리팩토링] WeaponBase 의존성을 제거하고 IWeaponController로 통합했습니다.
    /// </summary>
    public class PlayerWeaponManager
    {
        #region 내부 변수

        private readonly Transform m_playerTransform;
        private readonly List<IWeaponController> m_controllers = new List<IWeaponController>();
        private System.Func<Vector3> m_targetProvider;

        #endregion

        #region 프로퍼티

        /// <summary>
        /// 현재 장착된 무기 컨트롤러 목록입니다.
        /// </summary>
        public IReadOnlyList<IWeaponController> Controllers => m_controllers.AsReadOnly();

        /// <summary>
        /// 현재 장착된 무기 개수입니다.
        /// </summary>
        public int WeaponCount => m_controllers.Count;

        #endregion

        #region 생성자

        public PlayerWeaponManager(Transform playerTransform)
        {
            m_playerTransform = playerTransform;
            m_controllers.Clear();
        }

        public void SetTargetProvider(System.Func<Vector3> provider)
        {
            m_targetProvider = provider;
        }

        #endregion

        #region 무기 관리

        /// <summary>
        /// WeaponFactory를 통해 무기를 장착합니다.
        /// </summary>
        public void EquipWeapon(WeaponDataSO data, SkillData skillData = null)
        {
            if (data == null) return;

            // 중복 장착 방지
            if (HasWeapon(data.SkillCode))
            {
                LogManager.LogWarning($"[PlayerWeaponManager] {data.WeaponName}은(는) 이미 장착되어 있습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            // WeaponFactory를 통해 컨트롤러 생성
            IWeaponController controller = InGame.Weapon.WeaponFactory.CreateController(
                data, 
                m_playerTransform, 
                m_targetProvider
            );
            
            if (controller != null)
            {
                // SkillData 할당 (있는 경우)
                if (skillData != null)
                {
                    controller.SkillData = skillData;
                }
                
                m_controllers.Add(controller);
                LogManager.Log($"[PlayerWeaponManager] Equipped {data.WeaponName}", LogManager.LogCategory.Weapon);
            }
            else
            {
                LogManager.LogWarning($"[PlayerWeaponManager] Failed to create controller for {data.WeaponName} ({data.SkillCode})", LogManager.LogCategory.Weapon);
            }
        }

        /// <summary>
        /// 무기 컨트롤러를 직접 추가합니다.
        /// </summary>
        public void AddController(IWeaponController controller)
        {
            if (controller != null && !m_controllers.Contains(controller))
            {
                m_controllers.Add(controller);
            }
        }

        /// <summary>
        /// 스킬 코드로 무기를 제거합니다.
        /// </summary>
        public void RemoveWeapon(string skillCode)
        {
            var controllerToRemove = m_controllers.FirstOrDefault(c => c.SkillCode == skillCode);
            if (controllerToRemove != null)
            {
                controllerToRemove.Dispose();
                m_controllers.Remove(controllerToRemove);
                LogManager.Log($"[PlayerWeaponManager] Removed weapon: {skillCode}", LogManager.LogCategory.Weapon);
            }
        }

        /// <summary>
        /// 스킬 코드로 무기 보유 여부를 확인합니다.
        /// </summary>
        public bool HasWeapon(string skillCode)
        {
            return m_controllers.Any(c => c.SkillCode == skillCode);
        }

        /// <summary>
        /// 스킬 코드로 무기 컨트롤러를 가져옵니다.
        /// </summary>
        public IWeaponController GetWeapon(string skillCode)
        {
            return m_controllers.FirstOrDefault(c => c.SkillCode == skillCode);
        }

        /// <summary>
        /// 모든 무기로 공격합니다.
        /// </summary>
        public void AttackAll(Vector3 direction)
        {
            foreach (var controller in m_controllers)
            {
                controller?.Attack(direction);
            }
        }

        /// <summary>
        /// 모든 무기를 제거합니다.
        /// </summary>
        public void ClearAllWeapons()
        {
            foreach (var controller in m_controllers)
            {
                controller?.Dispose();
            }
            m_controllers.Clear();
        }

        #endregion

        #region 업데이트 루프

        /// <summary>
        /// 매 프레임 호출되어 무기 로직을 수행합니다. (Update()에서 호출)
        /// </summary>
        public void OnUpdate()
        {
            float deltaTime = Time.deltaTime;
            foreach (var controller in m_controllers)
            {
                controller?.OnUpdate(deltaTime);
            }
        }

        /// <summary>
        /// 매 프레임 후반에 호출되어 무기 위치를 플레이어와 동기화합니다. (LateUpdate()에서 호출)
        /// </summary>
        public void OnLateUpdate()
        {
            foreach (var controller in m_controllers)
            {
                controller?.OnLateUpdate();
            }
        }

        #endregion
    }
}
