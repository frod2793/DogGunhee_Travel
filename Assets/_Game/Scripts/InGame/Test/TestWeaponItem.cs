using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using InGame.Weapon.Base;

namespace InGame.Test
{
    /// <summary>
    /// [설명]: TestManager UI에서 개별 무기 항목(아이콘, 정보, 레벨업/삭제 버튼)을 제어하는 뷰 클래스입니다.
    /// 디버그 도구 내에서 플레이어가 보유한 무기의 실시간 상태를 시각화하고 조작 인터페이스를 제공합니다.
    /// </summary>
    public class TestWeaponItem : MonoBehaviour
    {
        #region 에디터 설정

        [Header("UI 참조")]
        [SerializeField, Tooltip("무기의 대표 이미지를 표시하는 아이콘 이미지")]
        private Image m_iconImage;

        [SerializeField, Tooltip("무기 이름과 현재 레벨을 표시하는 텍스트")]
        private TMP_Text m_infoText;

        [SerializeField, Tooltip("클릭 시 해당 무기의 레벨을 올리는 버튼")]
        private Button m_levelUpButton;

        [SerializeField, Tooltip("클릭 시 해당 무기를 인벤토리에서 제거하는 버튼")]
        private Button m_removeButton;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: UI 항목을 특정 무기 데이터에 맞춰 갱신하고 버튼 이벤트를 할당합니다.
        /// </summary>
        /// <param name="weapon">인터페이스 형태의 무기 컨트롤러 데이터</param>
        /// <param name="onLevelUp">레벨업 버튼 클릭 시 실행될 콜백 (매개변수: 스킬코드)</param>
        /// <param name="onRemove">삭제 버튼 클릭 시 실행될 콜백 (매개변수: 스킬코드)</param>
        public void Setup(IWeaponController weapon, Action<string> onLevelUp, Action<string> onRemove)
        {
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

        #region 내부 비즈니스 로직

        /// <summary>
        /// [설명]: 무기 아이콘 이미지와 텍스트 정보를 최신 데이터로 업데이트합니다.
        /// </summary>
        private void UpdateVisuals(IWeaponController weapon)
        {
            // 1. 아이콘 이미지 동기화
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

            // 2. 무기 정보 텍스트 동기화
            if (m_infoText != null)
            {
                // SkillData 원본 정보 우선 활용
                string weaponName = weapon.SkillData != null ? weapon.SkillData.skillName : weapon.WeaponName;
                string evolutionTag = weapon.IsEvolved ? " <color=yellow>[진화]</color>" : "";

                m_infoText.text = $"{weaponName} (Lv.{weapon.CurrentLevel}){evolutionTag}";
            }
        }

        /// <summary>
        /// [설명]: 레벨업 및 삭제 버튼의 활성화 상태를 제어하고 클릭 이벤트를 구독합니다.
        /// </summary>
        private void UpdateButtonEvents(IWeaponController weapon, Action<string> onLevelUp, Action<string> onRemove)
        {
            string skillCode = weapon.SkillCode;

            // 레벨업 버튼 설정
            if (m_levelUpButton != null)
            {
                m_levelUpButton.onClick.RemoveAllListeners();

                // 무기가 최대 레벨 미만일 때만 버튼 활성화
                bool canLevelUp = weapon.CurrentLevel < weapon.MaxLevel;
                m_levelUpButton.interactable = canLevelUp;

                if (canLevelUp)
                {
                    m_levelUpButton.onClick.AddListener(() =>
                    {
                        if (onLevelUp != null)
                        {
                            onLevelUp.Invoke(skillCode);
                        }
                    });
                }
            }

            // 삭제 버튼 설정
            if (m_removeButton != null)
            {
                m_removeButton.onClick.RemoveAllListeners();
                m_removeButton.onClick.AddListener(() =>
                {
                    if (onRemove != null)
                    {
                        onRemove.Invoke(skillCode);
                    }
                });
            }
        }

        #endregion
    }
}