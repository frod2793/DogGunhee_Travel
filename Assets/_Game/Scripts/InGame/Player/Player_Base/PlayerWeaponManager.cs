using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InGame.Weaphon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 무기 관리를 전담하는 POCO 클래스입니다.
    /// </summary>
    public class PlayerWeaponManager
    {
        #region 내부 변수
        private readonly Transform m_playerTransform;
        private List<WeaphonBase> m_weapons = new List<WeaphonBase>(); // Legacy
        private List<IWeaponController> m_controllers = new List<IWeaponController>(); // New System
        private System.Func<Vector3> m_targetProvider;
        #endregion

        #region 프로퍼티
        public IReadOnlyList<WeaphonBase> Weapons => m_weapons.AsReadOnly();
        public int WeaponCount => m_weapons.Count + m_controllers.Count;
        #endregion

        #region 생성자
        public PlayerWeaponManager(Transform playerTransform)
        {
            m_playerTransform = playerTransform;
            m_weapons.Clear();
            m_controllers.Clear();
        }

        public void SetTargetProvider(System.Func<Vector3> provider)
        {
            m_targetProvider = provider;
        }
        #endregion

        #region 무기 관리
        public void AddWeapon(WeaphonBase weapon)
        {
            if (weapon != null)
            {
                m_weapons.Add(weapon);
            }
        }

        public void AddController(IWeaponController controller)
        {
            if (controller != null)
            {
                m_controllers.Add(controller);
            }
        }

        public void EquipWeapon(InGame.Weaphon.Base.WeaponDataSO data)
        {
            if (data == null) return;

            IWeaponController controller = null;

            // 간단한 팩토리 로직 (나중에 별도 클래스로 분리 가능)
            // SkillCode나 다른 식별자로 구분
            if (data.SkillCode == "Bone") // 예: "Bone"
            {
                controller = new InGame.Weaphon.Controllers.BoneWeaponController();
            }
            // else if (data.SkillCode == "Flame") ...
            
            if (controller != null)
            {
                controller.Init(data, m_playerTransform, m_targetProvider);
                m_controllers.Add(controller);
                Debug.Log($"[PlayerWeaponManager] Equipped {data.WeaponName}");
            }
            else
            {
                Debug.LogWarning($"[PlayerWeaponManager] No controller found for {data.WeaponName} ({data.SkillCode})");
            }
        }

        public void RemoveWeapon(string skillCode)
        {
            // Legacy remove
            var weaponToRemove = m_weapons.FirstOrDefault(w => w.skillCode == skillCode);
            if (weaponToRemove != null)
            {
                m_weapons.Remove(weaponToRemove);
                Object.Destroy(weaponToRemove.gameObject);
            }
            
            // New system remove (TODO: IWeaponController에 식별자 추가 필요)
        }

        public bool HasWeapon(string skillCode)
        {
            return m_weapons.Any(w => w.skillCode == skillCode);
            // TODO: Check m_controllers as well
        }

        public void AttackAll(Vector3 direction)
        {
            foreach (var weapon in m_weapons)
            {
                if (weapon != null)
                {
                    weapon.Weaphon_Attack(direction);
                }
            }
            
            foreach (var controller in m_controllers)
            {
                if (controller != null)
                {
                    controller.Attack(direction);
                }
            }
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
                controller.OnUpdate(deltaTime);
            }
        }

        /// <summary>
        /// 매 프레임 후반에 호출되어 무기 위치를 플레이어와 동기화합니다. (LateUpdate()에서 호출)
        /// </summary>
        public void OnLateUpdate()
        {
            if (m_playerTransform == null) return;
            
            // Legacy Weapons
            foreach (var weapon in m_weapons)
            {
                if (weapon != null)
                {
                    weapon.transform.position = m_playerTransform.position;
                }
            }

            // New Controllers
            foreach (var controller in m_controllers)
            {
                controller.OnLateUpdate();
            }
        }
        #endregion
    }
}
