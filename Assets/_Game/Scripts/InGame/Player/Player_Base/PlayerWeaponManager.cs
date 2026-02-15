using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어가 보유한 모든 무기 인벤토리를 관리하고 무기별 동작(상태 업데이트, 공격 발동)을 총괄하는 로직 클래스입니다.
    /// PlayerController 또는 PlayerBase에 속해 프레임별 업데이트 인터페이스를 제공합니다.
    /// </summary>
    public class PlayerWeaponManager
    {
        #region 내부 필드

        /// <summary> 장착된 무기 컨트롤러들의 리스트 (GC 최소화를 위해 리스트 재사용) </summary>
        private readonly List<IWeaponController> m_controllers = new List<IWeaponController>();

        /// <summary> 무기가 공격 시 조준할 방향 데이터를 제공하는 외부 함수 대리자 </summary>
        private Func<Vector3> m_targetProvider;

        /// <summary> 사운드 매니저 참조 </summary>
        private InGame.Services.ISoundManager m_soundManager;

        #endregion

        #region 공개 프로퍼티

        /// <summary> [설명]: 현재 플레이어가 장착하여 활성화된 모든 무기 컨트롤러의 읽기 전용 목록입니다. </summary>
        public IReadOnlyList<IWeaponController> Controllers => m_controllers;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 무기 매니저 인스턴스를 생성하고 내부 리스트를 클리어합니다.
        /// </summary>
        public PlayerWeaponManager()
        {
            m_controllers.Clear();
        }

        /// <summary>
        /// [설명]: 무기들이 공격 방향을 결정할 때 참조할 타겟 정보 공급원을 주입합니다.
        /// </summary>
        /// <param name="provider">공격 방향 벡터를 반환하는 함수</param>
        public void SetTargetProvider(Func<Vector3> provider)
        {
            m_targetProvider = provider;
        }

        /// <summary>
        /// [설명]: 사운드 매니저를 설정합니다. 무기 추가 시 자동으로 주입됩니다.
        /// </summary>
        public void SetSoundManager(InGame.Services.ISoundManager soundManager)
        {
            m_soundManager = soundManager;
        }

        #endregion

        #region 무기 장착 및 해제

        /// <summary>
        /// [설명]: 새로운 무기 컨트롤러를 목록에 추가하고 즉시 첫 번째 공격 시퀀스를 작동시킵니다.
        /// </summary>
        /// <param name="controller">추가할 무기 컨트롤러 인스턴스</param>
        public void AddController(IWeaponController controller)
        {
            if (controller == null)
            {
                return;
            }

            // 중복 장착 방지 (GC Allocation 방지를 위해 for 루프 활용)
            for (int i = 0; i < m_controllers.Count; i++)
            {
                if (m_controllers[i] == controller)
                {
                    return;
                }
            }

            m_controllers.Add(controller);

            // 무기 컨트롤러에 사운드 매니저 주입
            if (controller is WeaponControllerBase baseController && m_soundManager != null)
            {
                baseController.SetSoundManager(m_soundManager);
            }

            // 장착과 동시에 초기 타겟 방향으로 공격 시도
            Vector3 initialTarget = m_targetProvider != null ? m_targetProvider.Invoke() : Vector3.zero;
            controller.Attack(initialTarget);
        }

        /// <summary>
        /// [설명]: 특정 고유 스킬 코드를 가진 무기를 인벤토리에서 제거하고 관련 리소스를 정리합니다.
        /// </summary>
        /// <param name="skillCode">제거할 무기의 고유 식별 코드</param>
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
        /// [설명]: 장착된 모든 무기를 제거하고 각각의 Dispose 메서드를 호출하여 메모리를 해제합니다.
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

        #region 제어 및 업데이트

        /// <summary>
        /// [설명]: 매 프레임 업데이트에서 장착된 모든 무기의 내부 로직(쿨다운 등)을 갱신합니다.
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
        /// [설명]: 프레임 후반부 업데이트에서 무기의 비주얼 위치나 회전값을 타겟에 물리적으로 동기화합니다.
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