using UnityEngine.UI;

namespace Lobby.UI
{
    /// <summary>
    /// [설명]: 버튼 컴포넌트 편의를 위한 확장 메서드 클래스입니다.
    /// </summary>
    public static class ButtonExtensions
    {
        /// <summary>
        /// [설명]: 기존 리스너를 모두 제거하고 새로운 액션을 등록합니다.
        /// </summary>
        public static void SetOnClick(this Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }
    }
}
