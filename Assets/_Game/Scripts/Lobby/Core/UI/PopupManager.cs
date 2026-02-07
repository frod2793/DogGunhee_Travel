using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.UI
{
    /// <summary>
    /// UI 팝업 스택을 관리하는 싱글톤 매니저
    /// ESC 키 입력 시 최상단 팝업을 닫는 기능을 수행합니다.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        #region 싱글톤

        private static PopupManager s_instance;
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

        #region 내부 상태

        private readonly Stack<Action> m_closePopupActions = new Stack<Action>();

        #endregion

        #region Unity 라이프사이클

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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseTopPopup();
            }
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// 팝업을 닫을 때 실행할 액션을 등록합니다. (스택에 Push)
        /// </summary>
        public void RegisterPopup(Action closeAction)
        {
            if (closeAction != null)
            {
                m_closePopupActions.Push(closeAction);
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
            }
        }

        /// <summary>
        /// 팝업 스택을 초기화합니다.
        /// </summary>
        public void ClearAllPopups()
        {
            m_closePopupActions.Clear();
        }

        #endregion
    }
}
