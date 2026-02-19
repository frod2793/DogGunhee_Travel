using UnityEngine;
using System;
using System.Collections.Generic;

namespace InGame.Data
{
    /// <summary>
    /// [설명]: 프로젝트 전역 로그 설정을 관리하는 ScriptableObject입니다.
    /// 카테고리별 활성화 여부 및 텍스트 컬러를 정의합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "LogSettings", menuName = "Game/Settings/LogSettings")]
    public class LogSettings : ScriptableObject
    {
        #region 에디터 설정
        [Header("전체 출력 제어")]
        [SerializeField, Tooltip("일반 로그 표시 여부")] private bool m_enableLog = true;
        [SerializeField, Tooltip("경고 로그 표시 여부")] private bool m_enableWarning = true;
        [SerializeField, Tooltip("에러 로그 표시 여부")] private bool m_enableError = true;

        [Header("카테고리별 상세 설정")]
        [SerializeField, Tooltip("카테고리별 활성화 및 컬러 설정")]
        private List<CategorySetting> m_categorySettings = new List<CategorySetting>();
        #endregion

        #region 프로퍼티
        public bool EnableLog => m_enableLog;
        public bool EnableWarning => m_enableWarning;
        public bool EnableError => m_enableError;
        public IReadOnlyList<CategorySetting> CategorySettings => m_categorySettings;
        #endregion

        #region 내부 구조체
        [Serializable]
        public struct CategorySetting
        {
            public LogManager.LogCategory Category;
            public bool Enabled;
            public Color Color;

            public CategorySetting(LogManager.LogCategory category, bool enabled, Color color)
            {
                Category = category;
                Enabled = enabled;
                Color = color;
            }
        }
        #endregion
    }
}
