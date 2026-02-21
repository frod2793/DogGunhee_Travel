using System;
using UnityEngine;
using InGame;

namespace Lobby.Core
{
    /// <summary>
    /// [설명]: 로비의 씬 전환 및 팝업 제어 로직을 실질적으로 수행하는 구체 클래스입니다.
    /// </summary>
    public class LobbyNavigator : ILobbyNavigator
    {
        private readonly OptionPopupView m_optionPopupPrefab;
        private readonly Transform m_popupParent;
        private readonly InGame.Services.ISoundManager m_soundManager;
        private readonly ISceneLoader m_sceneLoader;
        private readonly InGame.UI.IPopupService m_popupService;

        private OptionPopupView m_currentOptionPopup;

        public LobbyNavigator(
            OptionPopupView optionPopupPrefab, 
            Transform popupParent, 
            InGame.Services.ISoundManager soundManager,
            ISceneLoader sceneLoader,
            InGame.UI.IPopupService popupService)
        {
            m_optionPopupPrefab = optionPopupPrefab;
            m_popupParent = popupParent;
            m_soundManager = soundManager;
            m_sceneLoader = sceneLoader;
            m_popupService = popupService;
        }

        public void LoadScene(string sceneName, object payload = null)
        {
            if (m_sceneLoader != null)
            {
                m_sceneLoader.LoadScene(sceneName, payload);
            }
        }

        public void OpenOptionPopup()
        {
            if (m_optionPopupPrefab == null) return;

            if (m_currentOptionPopup != null)
            {
                UnityEngine.Object.Destroy(m_currentOptionPopup.gameObject);
            }

            m_currentOptionPopup = UnityEngine.Object.Instantiate(m_optionPopupPrefab, m_popupParent);
            m_currentOptionPopup.Initialize(m_soundManager, m_popupService);

            RegisterPopup(() =>
            {
                if (m_currentOptionPopup != null)
                {
                    UnityEngine.Object.Destroy(m_currentOptionPopup.gameObject);
                    m_currentOptionPopup = null;
                }
            });
        }

        public void CloseTopPopup()
        {
            if (m_popupService != null)
            {
                m_popupService.CloseTopPopup();
            }
        }

        public void RegisterPopup(Action closeAction)
        {
            if (m_popupService != null)
            {
                m_popupService.RegisterPopup(closeAction);
            }
        }
    }
}
