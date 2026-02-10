using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 무기 인벤토리를 관리하고 무기별 동작(업데이트, 공격)을 위임하는 클래스입니다.
    /// <br/> PlayerController에 의해 생성 및 갱신됩니다.
    /// </summary>
    public class PlayerWeaponManager
    {
        #region 1. 내부 변수 및 캐시

        // 무기 리스트 (Zero-Allocation 루프를 위해 List 유지)
        private readonly List<IWeaponController> m_controllers = new List<IWeaponController>();

        // 타겟팅 델리게이트
        private Func<Vector3> m_targetProvider;

        #endregion

        #region 2. 공개 프로퍼티

        /// <summary>
        /// 현재 장착된 모든 무기 컨트롤러 목록 (읽기 전용)
        /// </summary>
        public IReadOnlyList<IWeaponController> Controllers => m_controllers;

        #endregion

        #region 3. 생성자 및 초기화

        /// <summary>
        /// 무기 매니저를 초기화합니다.
        /// </summary>
        public PlayerWeaponManager()
        {
            m_controllers.Clear();
        }

        /// <summary>
        /// 무기가 공격 방향을 결정할 때 사용할 타겟 제공 함수를 설정합니다.
        /// </summary>
        public void SetTargetProvider(Func<Vector3> provider)
        {
            m_targetProvider = provider;
        }

        #endregion

        #region 4. 무기 장착 및 해제 (Inventory)

        /// <summary>
        /// 이미 생성된 무기 컨트롤러를 직접 등록합니다.
        /// </summary>
        public void AddController(IWeaponController controller)
        {
            if (controller == null) return;

            // 중복 확인 (Linq 대신 for 루프 사용)
            for (int i = 0; i < m_controllers.Count; i++)
            {
                if (m_controllers[i] == controller) return;
            }

            m_controllers.Add(controller);

            // 장착 즉시 발동
            Vector3 initialTarget = m_targetProvider != null ? m_targetProvider.Invoke() : Vector3.zero;
            controller.Attack(initialTarget);
        }

        /// <summary>
        /// 특정 스킬 코드를 가진 무기를 제거합니다.
        /// </summary>
        public void RemoveWeapon(string skillCode)
        {
            for (int i = 0; i < m_controllers.Count; i++)
            {
                if (m_controllers[i].SkillCode == skillCode)
                {
                    m_controllers[i].Dispose();
                    m_controllers.RemoveAt(i);
                    LogManager.Log($"[PlayerWeaponManager] 무기 제거됨: {skillCode}", LogManager.LogCategory.Weapon);
                    return;
                }
            }
        }

        /// <summary>
        /// 모든 무기를 제거하고 리소스를 해제합니다.
        /// </summary>
        public void ClearAllWeapons()
        {
            for (int i = 0; i < m_controllers.Count; i++)
            {
                m_controllers[i]?.Dispose();
            }

            m_controllers.Clear();
        }

        #endregion

        #region 5. 제어 및 업데이트 (Control & Update)

        /// <summary>
        /// 매 프레임(Update) 호출되어 무기들의 상태를 갱신합니다.
        /// </summary>
        public void OnUpdate()
        {
            float deltaTime = Time.deltaTime;
            int count = m_controllers.Count;
            for (int i = 0; i < count; i++)
            {
                m_controllers[i]?.OnUpdate(deltaTime);
            }
        }

        /// <summary>
        /// 매 프레임 후반(LateUpdate) 호출되어 무기 비주얼 위치를 동기화합니다.
        /// </summary>
        public void OnLateUpdate()
        {
            int count = m_controllers.Count;
            for (int i = 0; i < count; i++)
            {
                m_controllers[i]?.OnLateUpdate();
            }
        }

        #endregion
    }
}