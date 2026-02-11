using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.UI
{
    /// <summary>
    /// UI 팝업 스택을 관리하는 싱글톤 매니저입니다.
    /// <br/>ESC 키 입력 시 최상단 팝업을 닫는 기능을 수행합니다.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        #region 1. 싱글톤 패턴

        private static PopupManager s_instance;

        /// <summary>
        /// PopupManager의 전역 접근 인스턴스입니다.
        /// </summary>
        public static PopupManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<PopupManager>();
                    if (s_instance == null)
                    {
                        var go = new GameObject("@PopupManager");
                        s_instance = go.AddComponent<PopupManager>();
                    }
                }

                return s_instance;
            }
        }

        #endregion

        #region 2. 내부 변수 및 상태

        /// <summary>
        /// 현재 활성화된 팝업들의 닫기 액션을 저장하는 스택입니다.
        /// </summary>
        private readonly Stack<Action> m_closePopupActions = new Stack<Action>();

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // ESC 키 입력 시 최상단 팝업 닫기
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseTopPopup();
            }
        }

        #endregion

        #region 4. 공개 메서드

        /// <summary>
        /// 팝업을 닫을 때 실행할 액션을 등록합니다. (스택에 Push)
        /// </summary>
        /// <param name="closeAction">팝업을 비활성화하거나 파괴하는 메서드</param>
        public void RegisterPopup(Action closeAction)
        {
            if (closeAction != null)
            {
                m_closePopupActions.Push(closeAction);
                LogManager.LogError("팝업 클로즈 액션이 유효하지 않습니다.", LogManager.LogCategory.System);
            }
        }

        /// <summary>
        /// 최상단 팝업을 닫습니다. (스택에서 Pop & Invoke)
        /// </summary>
        public void CloseTopPopup()
        {
            if (m_closePopupActions.Count > 0)
            {
                var action = m_closePopupActions.Pop();
                action?.Invoke();
                LogManager.Log("[PopupManager] 새 팝업 등록됨", LogManager.LogCategory.System);
            }
        }

        /// <summary>
        /// 마지막으로 등록된 팝업 닫기 액션을 제거합니다. (실행하지 않고 스택에서 제거)
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
        /// 팝업 스택을 초기화합니다.
        /// </summary>
        public void ClearAllPopups()
        {
            m_closePopupActions.Clear();
            LogManager.Log("[PopupManager] 모든 팝업 스택 초기화됨", LogManager.LogCategory.System);
        }

        #endregion
    }
}
