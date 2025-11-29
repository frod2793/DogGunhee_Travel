using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DogGuns_Games.vamsir;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace DogGuns_Games.Test
{
    /// <summary>
    /// 인게임 캐릭터 및 무기 테스트를 위한 디버그/치트 패널입니다.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Dropdown m_characterDropdown;
        [SerializeField] private Button m_changeCharacterButton;
        [Space]
        [SerializeField] private TMP_Dropdown m_weaponDropdown;
        [SerializeField] private Button m_addWeaponButton;
        [Space]
        [SerializeField] private RectTransform m_ownedWeaponsContainer;
        [SerializeField] private TestWeaponItem m_ownedWeaponItemPrefab;

        [Header("Panel Animation")]
        [SerializeField] private Button m_toggleButton;
        [SerializeField] private RectTransform m_panelRectTransform;
        [SerializeField] private float m_animationDuration = 0.3f;

        [Header("Data")]
        [SerializeField] private SkillDatabase m_skillDatabase;

        private GameManager m_gameManager;
        private List<SkillData> m_allWeaponSkills;
        private List<GameObject> m_spawnedWeaponItems = new List<GameObject>();

        private bool m_isPanelOpen = false;
        private bool m_isAnimating = false;

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            if (m_gameManager == null)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        private void Start()
        {
            InitializeData();
            InitializeUI();
            InitializePanel();
            
            GameManager.OnPlayerChanged += (player) => RefreshOwnedWeaponList();
        }

        private void OnEnable()
        {
            if (m_gameManager != null)
            {
                RefreshOwnedWeaponList();
            }
        }

        private void InitializeData()
        {
            if (m_skillDatabase == null) return;

            m_characterDropdown.ClearOptions();
            m_characterDropdown.AddOptions(new List<string> { "캐릭터 0", "캐릭터 1" }); // 임시

            m_allWeaponSkills = m_skillDatabase.allSkills
                .Where(s => s.skillType == SkillType.Weapon)
                .ToList();
            
            m_weaponDropdown.ClearOptions();
            m_weaponDropdown.AddOptions(m_allWeaponSkills.Select(s => s.skillName).ToList());
        }

        private void InitializeUI()
        {
            m_changeCharacterButton.onClick.AddListener(OnChangeCharacter);
            m_addWeaponButton.onClick.AddListener(OnAddWeapon);
            m_toggleButton.onClick.AddListener(() => TogglePanelAsync().Forget());
        }

        private void InitializePanel()
        {
            if (m_panelRectTransform == null) return;
            
            Canvas.ForceUpdateCanvases();
            
            // [수정] 피봇을 고려한 정확한 숨김 위치 계산
            float panelWidth = m_panelRectTransform.rect.width;
            float pivotX = m_panelRectTransform.pivot.x;
            float hiddenX = -panelWidth * (1 - pivotX); // 오른쪽 끝이 앵커에 오도록 계산
            
            m_panelRectTransform.anchoredPosition = new Vector2(hiddenX, m_panelRectTransform.anchoredPosition.y);
            m_isPanelOpen = false;
        }

        private async UniTaskVoid TogglePanelAsync()
        {
            if (m_isAnimating || m_panelRectTransform == null) return;

            m_isAnimating = true;
            m_isPanelOpen = !m_isPanelOpen;

            Canvas.ForceUpdateCanvases();
            
            // [수정] 피봇을 고려한 정확한 목표 위치 계산
            float panelWidth = m_panelRectTransform.rect.width;
            float pivotX = m_panelRectTransform.pivot.x;
            
            float shownX = panelWidth * pivotX; // 왼쪽 끝이 앵커에 오도록 계산
            float hiddenX = -panelWidth * (1 - pivotX); // 오른쪽 끝이 앵커에 오도록 계산

            float targetX = m_isPanelOpen ? shownX : hiddenX;
            Ease ease = m_isPanelOpen ? Ease.OutCubic : Ease.InCubic;

            await m_panelRectTransform.DOAnchorPosX(targetX, m_animationDuration)
                .SetEase(ease)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            m_isAnimating = false;
        }

        private void RefreshOwnedWeaponList()
        {
            foreach (var item in m_spawnedWeaponItems)
            {
                Destroy(item);
            }
            m_spawnedWeaponItems.Clear();

            if (m_gameManager.SpawnedPlayer == null || m_ownedWeaponItemPrefab == null) return;

            foreach (var weapon in m_gameManager.SpawnedPlayer.Weapons)
            {
                TestWeaponItem itemInstance = Instantiate(m_ownedWeaponItemPrefab, m_ownedWeaponsContainer);
                itemInstance.Setup(weapon, LevelUpWeapon, RemoveWeapon);
                m_spawnedWeaponItems.Add(itemInstance.gameObject);
            }
        }

        private void OnChangeCharacter()
        {
            int selectedIndex = m_characterDropdown.value;
            PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = selectedIndex;
            m_gameManager.ChangeCharacterAndWeapon_Spawn().Forget();
        }

        private void OnAddWeapon()
        {
            if (m_allWeaponSkills == null || m_allWeaponSkills.Count == 0) return;

            int selectedIndex = m_weaponDropdown.value;
            SkillData selectedSkill = m_allWeaponSkills[selectedIndex];

            if (m_gameManager.SpawnedPlayer.Weapons.Any(w => w.skillCode == selectedSkill.skillCode))
            {
                Debug.LogWarning($"[TestManager] 이미 보유한 무기({selectedSkill.skillName})입니다.");
                return;
            }

            m_gameManager.EquipNewWeapon(selectedSkill).ContinueWith(RefreshOwnedWeaponList);
        }

        private void LevelUpWeapon(string skillCode)
        {
            var weapon = m_gameManager.SpawnedPlayer?.Weapons.FirstOrDefault(w => w.skillCode == skillCode);
            if (weapon != null)
            {
                weapon.UpgradeLevel();
                RefreshOwnedWeaponList();
            }
        }

        private void RemoveWeapon(string skillCode)
        {
            m_gameManager.RemoveWeaponForTest(skillCode);
            RefreshOwnedWeaponList();
        }
    }
}