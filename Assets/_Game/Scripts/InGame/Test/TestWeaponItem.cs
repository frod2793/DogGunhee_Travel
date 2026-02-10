using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using InGame.Weapon.Base;

namespace InGame.Test
{
    /// <summary>
    /// TestManager UI에서 개별 무기 항목(아이콘, 정보, 레벨업/삭제 버튼)을 제어하는 뷰 클래스입니다.
    /// </summary>
    public class TestWeaponItem : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("UI 참조")]
        [SerializeField, Tooltip("무기 아이콘 이미지")] 
        private Image m_iconImage;
        
        [SerializeField, Tooltip("무기 정보 텍스트 (이름/레벨)")] 
        private TMP_Text m_infoText;
        
        [SerializeField, Tooltip("레벨업 버튼")] 
        private Button m_levelUpButton;
        
        [SerializeField, Tooltip("삭제 버튼")] 
        private Button m_removeButton;

        #endregion

        #region 2. 초기화 및 설정

        /// <summary>
        /// UI 항목을 특정 무기 데이터로 초기화하고 이벤트를 연결합니다.
        /// </summary>
        /// <param name="weapon">표시할 무기 컨트롤러 인터페이스</param>
        /// <param name="onLevelUp">레벨업 버튼 클릭 콜백 (SkillCode 반환)</param>
        /// <param name="onRemove">삭제 버튼 클릭 콜백 (SkillCode 반환)</param>
        public void Setup(IWeaponController weapon, Action<string> onLevelUp, Action<string> onRemove)
        {
            // 데이터 유효성 검사
            if (weapon == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            UpdateVisuals(weapon);
            UpdateButtonEvents(weapon, onLevelUp, onRemove);
        }

        #endregion

        #region 3. 내부 로직

        /// <summary>
        /// 아이콘과 텍스트 정보를 갱신합니다.
        /// </summary>
        private void UpdateVisuals(IWeaponController weapon)
        {
            // 1. 아이콘 설정
            if (m_iconImage != null)
            {
                if (weapon.Thumbnail != null)
                {
                    m_iconImage.sprite = weapon.Thumbnail;
                    m_iconImage.enabled = true;
                }
                else
                {
                    m_iconImage.enabled = false;
                }
            }

            // 2. 텍스트 설정
            if (m_infoText != null)
            {
                // SkillData가 없으면 WeaponName 사용 (안전 처리)
                string weaponName = weapon.SkillData != null ? weapon.SkillData.skillName : weapon.WeaponName;
                string evolutionTag = weapon.IsEvolved ? " <color=yellow>[진화]</color>" : "";
                
                m_infoText.text = $"{weaponName} (Lv.{weapon.CurrentLevel}){evolutionTag}";
            }
        }

        /// <summary>
        /// 버튼의 상태를 설정하고 리스너를 연결합니다.
        /// </summary>
        private void UpdateButtonEvents(IWeaponController weapon, Action<string> onLevelUp, Action<string> onRemove)
        {
            string skillCode = weapon.SkillCode;

            // 1. 레벨업 버튼
            if (m_levelUpButton != null)
            {
                m_levelUpButton.onClick.RemoveAllListeners();

                // 최대 레벨 도달 시 버튼 비활성화
                bool canLevelUp = weapon.CurrentLevel < weapon.MaxLevel;
                m_levelUpButton.interactable = canLevelUp;

                if (canLevelUp)
                {
                    m_levelUpButton.onClick.AddListener(() => onLevelUp?.Invoke(skillCode));
                }
            }

            // 2. 삭제 버튼
            if (m_removeButton != null)
            {
                m_removeButton.onClick.RemoveAllListeners();
                m_removeButton.onClick.AddListener(() => onRemove?.Invoke(skillCode));
            }
        }

        #endregion
    }
}