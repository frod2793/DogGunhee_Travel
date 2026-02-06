using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using InGame.Weaphon.Base;
using InGame.vamsir;

namespace InGame.Test
{
    /// <summary>
    /// TestManager의 소지 무기 목록에 표시될 각 UI 항목을 제어하는 스크립트입니다.
    /// </summary>
    public class TestWeaponItem : MonoBehaviour
    {
        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_infoText;
        [SerializeField] private Button m_levelUpButton;
        [SerializeField] private Button m_removeButton;

        /// <summary>
        /// UI 항목을 특정 무기 데이터와 이벤트에 맞게 설정합니다.
        /// </summary>
        /// <param name="weapon">표시할 무기 인스턴스</param>
        /// <param name="onLevelUp">레벨업 버튼 클릭 시 호출될 콜백 (skillCode 전달)</param>
        /// <param name="onRemove">제거 버튼 클릭 시 호출될 콜백 (skillCode 전달)</param>
        public void Setup(WeaphonBase weapon, Action<string> onLevelUp, Action<string> onRemove)
        {
            if (weapon == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // 1. UI 내용 채우기
            if (m_iconImage != null)
            {
                m_iconImage.sprite = weapon.Thumnail;
                m_iconImage.enabled = weapon.Thumnail != null;
            }

            if (m_infoText != null)
            {
                string evolutionText = weapon.isEvolved ? " (진화)" : "";
                m_infoText.text = $"{weapon.skillData.skillName} (Lv.{weapon.CurrentLevel}{evolutionText})";
            }

            // 2. 버튼 이벤트 연결
            // 기존 리스너를 모두 제거하여 중복 연결을 방지합니다.
            m_levelUpButton.onClick.RemoveAllListeners();
            m_removeButton.onClick.RemoveAllListeners();

            // 레벨업 버튼은 최대 레벨이 아닐 때만 활성화
            bool canLevelUp = weapon.CurrentLevel < WeaphonBase.k_MaxLevel;
            m_levelUpButton.interactable = canLevelUp;
            if (canLevelUp)
            {
                m_levelUpButton.onClick.AddListener(() => onLevelUp?.Invoke(weapon.skillCode));
            }

            m_removeButton.onClick.AddListener(() => onRemove?.Invoke(weapon.skillCode));
        }
    }
}