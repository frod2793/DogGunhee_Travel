using TMPro;

namespace InGame.UI.Views
{
    /// <summary>
    /// TMP_Text 확장 메서드 (Null 체크 포함)
    /// </summary>
    public static class TextExtensions
    {
        // 포맷 스트링 없이 단순 텍스트 설정
        public static void SetSafeText(this TMP_Text text, string content)
        {
            if (text != null) text.text = content;
        }

        // 포맷 스트링과 단일 인자
        public static void SetSafeText(this TMP_Text text, string format, object arg)
        {
            if (text != null) text.text = string.Format(format, arg);
        }
        
        // 포맷 스트링과 2개의 인자
        public static void SetSafeText(this TMP_Text text, string format, object arg0, object arg1)
        {
            if (text != null) text.text = string.Format(format, arg0, arg1);
        }

        // 포맷 스트링과 3개의 인자
        public static void SetSafeText(this TMP_Text text, string format, object arg0, object arg1, object arg2)
        {
            if (text != null) text.text = string.Format(format, arg0, arg1, arg2);
        }
    }
}
