using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 무기 인벤토리와 알고리즘(공격 명령 위임, 업데이트 로직)을 총괄하는 POCO 클래스입니다.
    /// WeaponBase 대신 IWeaponController 인터페이스를 사용하여 결합도를 낮췄습니다.
    /// </summary>
    public class PlayerWeaponManager
    {
        #region 내부 상태 및 캐시
        
        private readonly Transform m_playerTransform;
        private readonly List<IWeaponController> m_controllers = new List<IWeaponController>();
        private System.Func<Vector3> m_targetProvider;
        
        #endregion

        #region 프로퍼티

        /// <summary>
        /// 현재 장착된 모든 무기 컨트롤러 목록입니다.
        /// </summary>
        public IReadOnlyList<IWeaponController> Controllers => m_controllers.AsReadOnly();

        /// <summary>
        /// 현재 장착된 무기의 총 개수입니다.
        /// </summary>
        public int WeaponCount => m_controllers.Count;

        #endregion

        #region 초기화

        public PlayerWeaponManager(Transform playerTransform)
        {
            m_playerTransform = playerTransform;
            m_controllers.Clear();
        }

        /// <summary>
        /// 무기가 공격 시 타겟 방향을 계산할 때 사용할 델리게이트를 등록합니다.
        /// </summary>
        public void SetTargetProvider(System.Func<Vector3> provider)
        {
            m_targetProvider = provider;
        }

        #endregion

        #region 무기 관리 로직

        /// <summary>
        /// WeaponFactory를 호출하여 새로운 무기를 생성하고 장착합니다. 중복 장착은 허용하지 않습니다.
        /// </summary>
        public void EquipWeapon(WeaponDataSO data, SkillData skillData = null)
        {
            if (data == null) return;

            // 이미 동일한 스킬 코드를 가진 무기가 있는지 확인
            if (HasWeapon(data.SkillCode))
            {
                LogManager.LogWarning($"[PlayerWeaponManager] {data.WeaponName}은(는) 이미 장착되어 있습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            // Factory를 통해 해당 무기 타입에 맞는 컨트롤러 생성
            IWeaponController controller = InGame.Weapon.WeaponFactory.CreateController(
                data, 
                m_playerTransform, 
                m_targetProvider
            );
            
            if (controller != null)
            {
                // 인게임 데이터(스탯 등) 연동을 위한 SkillData 할당
                if (skillData != null)
                {
                    controller.SkillData = skillData;
                }
                
                m_controllers.Add(controller);
                LogManager.Log($"[PlayerWeaponManager] Equipped {data.WeaponName}", LogManager.LogCategory.Weapon);
            }
            else
            {
                LogManager.LogWarning($"[PlayerWeaponManager] Failed to create controller for {data.WeaponName}", LogManager.LogCategory.Weapon);
            }
        }

        /// <summary>
        /// 외부에서 생성된 무기 컨트롤러를 관리 목록에 직접 등록합니다.
        /// </summary>
        public void AddController(IWeaponController controller)
        {
            if (controller != null && !m_controllers.Contains(controller))
            {
                m_controllers.Add(controller);
            }
        }

        /// <summary>
        /// 지정된 스킬 코드를 가진 무기를 제거하고 리소스를 해제합니다.
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

        public bool HasWeapon(string skillCode) => m_controllers.Any(c => c.SkillCode == skillCode);
        public IWeaponController GetWeapon(string skillCode) => m_controllers.FirstOrDefault(c => c.SkillCode == skillCode);

        /// <summary>
        /// 현재 장착된 모든 무기에 동시에 공격 명령을 내립니다.
        /// </summary>
        public void AttackAll(Vector3 direction)
        {
            foreach (var controller in m_controllers)
            {
                controller?.Attack(direction);
            }
        }

        /// <summary>
        /// 모든 무기를 제거하고 초기화합니다.
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

        #region 시스템 업데이트

        /// <summary>
        /// 매 프레임 무기의 쿨다운 및 내부 상태를 갱신합니다.
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
        /// 무기 비주얼의 위치를 플레이어와 동기화합니다.
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
