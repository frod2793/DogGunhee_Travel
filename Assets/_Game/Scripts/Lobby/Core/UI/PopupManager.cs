using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.UI
{
    /// <summary>
    /// [설명]: UI 팝업 스택을 관리하는 싱글톤 매니저입니다.
    /// ESC 키 입력 시 최상단 팝업을 닫는 기능을 수행합니다.
    /// </summary>
    public class PopupManager : MonoBehaviour, IPopupService
    {

        #region 내부 변수

        /// <summary>
        /// 현재 활성화된 팝업들의 닫기 액션을 저장하는 스택입니다.
        /// </summary>
        private readonly Stack<Action> m_closePopupActions = new Stack<Action>();

        #endregion

        #region 유니티 생명주기
        
        private void Update()
        {
            // ESC 키 입력 시 최상단 팝업 닫기
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseTopPopup();
            }
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// [설명]: 팝업을 닫을 때 실행할 액션을 등록합니다. (스택에 Push)
        /// </summary>
        /// <param name="closeAction">팝업을 비활성화하거나 파괴하는 메서드</param>
        public void RegisterPopup(Action closeAction)
        {
            if (closeAction != null)
            {
                m_closePopupActions.Push(closeAction);
                LogManager.Log("[PopupManager] 새 팝업 클로즈 액션 등록됨", LogManager.LogCategory.System);
            }
            else
            {
                LogManager.LogError("[PopupManager] 등록하려는 팝업 클로즈 액션이 유효하지 않습니다(null).", LogManager.LogCategory.System);
            }
        }

        /// <summary>
        /// [설명]: 최상단 팝업을 닫습니다. (스택에서 Pop & Invoke)
        /// </summary>
        public void CloseTopPopup()
        {
            if (m_closePopupActions.Count > 0)
            {
                var action = m_closePopupActions.Pop();
                action?.Invoke();
                LogManager.Log("[PopupManager] 최상단 팝업 닫기 액션 실행됨", LogManager.LogCategory.System);
            }
        }

        /// <summary>
        /// [설명]: 마지막으로 등록된 팝업 닫기 액션을 제거합니다. (실행하지 않고 스택에서 제거)
        /// </summary>
        public void RemoveLastPopupAction()
        {
            if (m_closePopupActions.Count > 0)
            {
                m_closePopupActions.Pop();
                LogManager.Log("[PopupManager] 최상단 팝업 액션 제거됨 (실행 안함)", LogManager.LogCategory.System);
            }
        }

        /// <summary>
        /// [설명]: 팝업 스택을 초기화합니다.
        /// </summary>
        public void ClearAllPopups()
        {
            m_closePopupActions.Clear();
            LogManager.Log("[PopupManager] 모든 팝업 스택 초기화됨", LogManager.LogCategory.System);
        }

        #endregion
    }
}
