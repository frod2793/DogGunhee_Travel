using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Test;
using InGame.Weapon.Base;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace InGame.Manager
{
    /// <summary>
    /// 인게임 캐릭터 및 무기 테스트를 위한 디버그/치트 패널입니다.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        [Header("UI 참조")] [SerializeField] private TMP_Dropdown m_characterDropdown;
        [SerializeField] private Button m_changeCharacterButton;
        [Space] [SerializeField] private TMP_Dropdown m_weaponDropdown;
        [SerializeField] private Button m_addWeaponButton;
        [Space] [SerializeField] private RectTransform m_ownedWeaponsContainer;
        [SerializeField] private TestWeaponItem m_ownedWeaponItemPrefab;

        [Header("무기 생성 옵션")] [SerializeField] private TMP_InputField m_startLevelInput;
        [SerializeField] private Toggle m_startEvolvedToggle;

        [Header("패널 애니메이션")] [SerializeField] private Button m_toggleButton;
        public Transform panelTransform;
        public Transform hiddenPosition;
        public Transform shownPosition;
        [SerializeField] private float m_animationDuration = 0.3f;

        [Header("데이터")] [SerializeField] private SkillDatabase m_skillDatabase;

        private GameManager m_gameManager;
        private List<SkillData> m_allWeaponSkills;
        private List<GameObject> m_spawnedWeaponItems = new List<GameObject>();
        private List<CharacterInfo> m_loadedCharacters = new List<CharacterInfo>();

        private bool m_isPanelOpen = false;
        private bool m_isAnimating = false;

        private class CharacterInfo
        {
            public int Index;
            public string Name;
        }

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            if (m_gameManager == null)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        private async void Start()
        {
            InitializeUI();
            await InitializeDataAsync();
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

        private async UniTask InitializeDataAsync()
        {
            if (m_skillDatabase == null) return;

            m_characterDropdown.interactable = false;
            m_loadedCharacters.Clear();
            var characterNames = new List<string>();

            int i = 0;
            while (true)
            {
                string key = $"Player_Character_{i}";
                var locationsHandle = Addressables.LoadResourceLocationsAsync(key);
                await locationsHandle;

                if (locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result.Count > 0)
                {
                    var assetHandle = Addressables.LoadAssetAsync<GameObject>(key);
                    await assetHandle;

                    if (assetHandle.Status == AsyncOperationStatus.Succeeded && assetHandle.Result != null)
                    {
                        GameObject prefab = assetHandle.Result;
                        string charName = prefab.name.Replace("Player_Character_", "");
                        m_loadedCharacters.Add(new CharacterInfo { Index = i, Name = charName });
                        characterNames.Add(charName);
                        Addressables.Release(assetHandle);
                    }

                    Addressables.Release(locationsHandle);
                    i++;
                }
                else
                {
                    Addressables.Release(locationsHandle);
                    break;
                }
            }

            m_characterDropdown.ClearOptions();
            m_characterDropdown.AddOptions(characterNames);
            m_characterDropdown.interactable = true;

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
            if (panelTransform == null || hiddenPosition == null) return;
            panelTransform.position = hiddenPosition.position;
            m_isPanelOpen = false;
        }

        private async UniTaskVoid TogglePanelAsync()
        {
            if (m_isAnimating || panelTransform == null || hiddenPosition == null || shownPosition == null) return;

            m_isAnimating = true;
            m_isPanelOpen = !m_isPanelOpen;

            Vector3 targetPosition = m_isPanelOpen ? shownPosition.position : hiddenPosition.position;
            Ease ease = m_isPanelOpen ? Ease.OutCubic : Ease.InCubic;

            await panelTransform.DOMove(targetPosition, m_animationDuration)
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
            if (selectedIndex < 0 || selectedIndex >= m_loadedCharacters.Count) return;

            int characterIndexToSpawn = m_loadedCharacters[selectedIndex].Index;

            PlayerDataManager.Instance.SelectCharacterIndex = characterIndexToSpawn;
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

            int startLevel = 1;
            if (m_startLevelInput != null && !string.IsNullOrEmpty(m_startLevelInput.text))
            {
                int.TryParse(m_startLevelInput.text, out startLevel);
                startLevel = Mathf.Clamp(startLevel, 1, WeaponBase.k_MaxLevel);
            }

            bool startEvolved = m_startEvolvedToggle != null && m_startEvolvedToggle.isOn;

            m_gameManager.EquipNewWeapon(selectedSkill, true, startLevel, startEvolved)
                .ContinueWith(RefreshOwnedWeaponList);
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