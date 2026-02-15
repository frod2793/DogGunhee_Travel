using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using InGame.Player.Player_Base;
using InGame.Test;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 인게임 캐릭터 및 무기 테스트를 위한 디버그/치트 패널 관리 클래스입니다.
    /// 캐릭터 교체, 무기 추가/레벨업/삭제 등의 기능을 제공합니다.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        #region 에디터 설정

        [Header("UI 참조 - 캐릭터")]
        [SerializeField, Tooltip("캐릭터 선택 드롭다운")] private TMP_Dropdown m_characterDropdown;
        [SerializeField, Tooltip("캐릭터 변경 버튼")] private Button m_changeCharacterButton;

        [Header("UI 참조 - 무기")]
        [SerializeField, Tooltip("추가할 무기 선택 드롭다운")] private TMP_Dropdown m_weaponDropdown;
        [SerializeField, Tooltip("무기 추가 버튼")] private Button m_addWeaponButton;
        [SerializeField, Tooltip("보유 무기 목록 컨테이너")] private RectTransform m_ownedWeaponsContainer;
        [SerializeField, Tooltip("보유 무기 아이템 프리팹 (TestWeaponItem)")] private TestWeaponItem m_ownedWeaponItemPrefab;

        [Header("무기 생성 옵션")]
        [SerializeField, Tooltip("시작 레벨 입력 필드")] private TMP_InputField m_startLevelInput;
        [SerializeField, Tooltip("진화 상태로 시작 여부 토글")] private Toggle m_startEvolvedToggle;

        [Header("패널 애니메이션")]
        [SerializeField, Tooltip("패널 열기/닫기 토글 버튼")] private Button m_toggleButton;
        [SerializeField, Tooltip("애니메이션 대상 패널 트랜스폼")] private Transform m_panelTransform;
        [SerializeField, Tooltip("패널이 숨겨질 위치")] private Transform m_hiddenPosition;
        [SerializeField, Tooltip("패널이 보여질 위치")] private Transform m_shownPosition;
        [SerializeField, Tooltip("애니메이션 지속 시간")] private float m_animationDuration = 0.3f;

        [Header("UI 참조 - 게임 흐름")]
        [SerializeField, Tooltip("스테이지 클리어 테스트 버튼")] private Button m_clearStageButton;

        [Header("데이터 참조")]
        [SerializeField, Tooltip("전체 스킬 데이터베이스")] private SkillDatabase m_skillDatabase;

        #endregion

        #region 내부 필드 및 데이터 구조

        private GameManager m_gameManager;
        private List<SkillData> m_allWeaponSkills;
        private readonly List<GameObject> m_spawnedWeaponItems = new List<GameObject>();
        private readonly List<CharacterInfo> m_loadedCharacters = new List<CharacterInfo>();

        private bool m_isPanelOpen = false;
        private bool m_isAnimating = false;

        /// <summary>
        /// [설명]: 테스트 패널에서 관리할 캐릭터 정보를 담는 내부 클래스입니다.
        /// </summary>
        private class CharacterInfo
        {
            public int Index;
            public string Name;
        }

        #endregion

        #region 공개 프로퍼티

        public Transform PanelTransform => m_panelTransform;
        public Transform HiddenPosition => m_hiddenPosition;
        public Transform ShownPosition => m_shownPosition;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            if (m_gameManager == null)
            {
                Debug.LogError("[TestManager] GameManager를 찾을 수 없어 패널을 비활성화합니다.");
                gameObject.SetActive(false);
                return;
            }

            InitializeUI();
        }

        private void Start()
        {
            // 데이터 비동기 로드 시작
            InitializeDataAsync().Forget();

            // 초기 상태 설정
            InitializePanelPosition();

            // 이벤트 구독
            GameManager.OnPlayerChanged += HandlePlayerChanged;
        }

        private void OnEnable()
        {
            if (m_gameManager != null && m_gameManager.SpawnedPlayer != null)
            {
                RefreshOwnedWeaponList();
            }
        }

        private void OnDestroy()
        {
            GameManager.OnPlayerChanged -= HandlePlayerChanged;

            if (m_panelTransform != null)
            {
                m_panelTransform.DOKill();
            }
        }

        #endregion

        #region 초기화 및 데이터 로드

        /// <summary>
        /// [설명]: Addressables를 통해 사용 가능한 캐릭터 목록을 로드하고 드롭다운을 구성합니다.
        /// </summary>
        private async UniTaskVoid InitializeDataAsync()
        {
            if (m_skillDatabase == null)
            {
                Debug.LogWarning("[TestManager] SkillDatabase가 할당되지 않았습니다.");
                return;
            }

            if (m_characterDropdown != null)
            {
                m_characterDropdown.interactable = false;
            }

            m_loadedCharacters.Clear();
            var characterNames = new List<string>();

            int charIndex = 0;
            while (true)
            {
                string key = $"Player_Character_{charIndex}";

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

                        m_loadedCharacters.Add(new CharacterInfo { Index = charIndex, Name = charName });
                        characterNames.Add($"[{charIndex}] {charName}");

                        Addressables.Release(assetHandle);
                    }

                    Addressables.Release(locationsHandle);
                    charIndex++;
                }
                else
                {
                    Addressables.Release(locationsHandle);
                    break;
                }
            }

            if (m_characterDropdown != null)
            {
                m_characterDropdown.ClearOptions();
                m_characterDropdown.AddOptions(characterNames);
                m_characterDropdown.interactable = true;
            }

            m_allWeaponSkills = m_skillDatabase.allSkills
                .Where(s => s.skillType == SkillType.Weapon)
                .ToList();

            if (m_weaponDropdown != null)
            {
                m_weaponDropdown.ClearOptions();
                m_weaponDropdown.AddOptions(m_allWeaponSkills.Select(s => s.skillName).ToList());
            }
        }

        /// <summary>
        /// [설명]: UI 버튼 이벤트를 연결합니다.
        /// </summary>
        private void InitializeUI()
        {
            if (m_changeCharacterButton != null)
            {
                m_changeCharacterButton.onClick.AddListener(OnChangeCharacterClicked);
            }

            if (m_addWeaponButton != null)
            {
                m_addWeaponButton.onClick.AddListener(OnAddWeaponClicked);
            }

            if (m_clearStageButton != null)
            {
                m_clearStageButton.onClick.AddListener(OnClearStageClicked);
            }

            if (m_toggleButton != null)
            {
                m_toggleButton.onClick.AddListener(() => TogglePanelAsync().Forget());
            }
        }

        /// <summary>
        /// [설명]: 패널의 초기 위치를 숨김 위치로 설정합니다.
        /// </summary>
        private void InitializePanelPosition()
        {
            if (m_panelTransform != null && m_hiddenPosition != null)
            {
                m_panelTransform.position = m_hiddenPosition.position;
                m_isPanelOpen = false;
            }
        }

        #endregion

        #region UI 상호작용 및 로직

        /// <summary>
        /// [설명]: 패널 열기/닫기 애니메이션을 수행합니다.
        /// </summary>
        private async UniTaskVoid TogglePanelAsync()
        {
            if (m_isAnimating || m_panelTransform == null || m_hiddenPosition == null || m_shownPosition == null)
            {
                return;
            }

            m_isAnimating = true;
            m_isPanelOpen = !m_isPanelOpen;

            Vector3 targetPosition = m_isPanelOpen ? m_shownPosition.position : m_hiddenPosition.position;
            Ease ease = m_isPanelOpen ? Ease.OutCubic : Ease.InCubic;

            await m_panelTransform.DOMove(targetPosition, m_animationDuration)
                .SetEase(ease)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            m_isAnimating = false;
        }

        /// <summary>
        /// [설명]: 플레이어 교체 이벤트를 처리합니다.
        /// </summary>
        private void HandlePlayerChanged(PlayerBase player)
        {
            RefreshOwnedWeaponList();
        }

        /// <summary>
        /// [설명]: 현재 플레이어가 보유한 무기 목록 UI를 갱신합니다.
        /// </summary>
        private void RefreshOwnedWeaponList()
        {
            foreach (var item in m_spawnedWeaponItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            m_spawnedWeaponItems.Clear();

            if (m_gameManager == null || m_gameManager.SpawnedPlayer == null || m_ownedWeaponItemPrefab == null)
            {
                return;
            }

            foreach (var weapon in m_gameManager.SpawnedPlayer.Weapons)
            {
                if (weapon == null)
                {
                    continue;
                }

                TestWeaponItem itemInstance = Instantiate(m_ownedWeaponItemPrefab, m_ownedWeaponsContainer);
                itemInstance.Setup(weapon, OnLevelUpWeaponClicked, OnRemoveWeaponClicked);

                m_spawnedWeaponItems.Add(itemInstance.gameObject);
            }
        }

        /// <summary>
        /// [설명]: 캐릭터 변경 버튼 클릭 시 선택된 캐릭터로 재생성을 요청합니다.
        /// </summary>
        private void OnChangeCharacterClicked()
        {
            if (m_characterDropdown == null)
            {
                return;
            }

            int selectedIndex = m_characterDropdown.value;
            if (selectedIndex < 0 || selectedIndex >= m_loadedCharacters.Count)
            {
                return;
            }

            int charIndexToSpawn = m_loadedCharacters[selectedIndex].Index;

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SelectCharacterIndex = charIndexToSpawn;
            }

            m_gameManager.ChangeCharacterAndWeapon_Spawn().Forget();
        }

        /// <summary>
        /// [설명]: 무기 추가 버튼 클릭 시 선택된 무기를 장착시킵니다.
        /// </summary>
        private void OnAddWeaponClicked()
        {
            if (m_allWeaponSkills == null || m_allWeaponSkills.Count == 0)
            {
                return;
            }

            if (m_gameManager == null || m_gameManager.SpawnedPlayer == null)
            {
                return;
            }

            int selectedIndex = m_weaponDropdown != null ? m_weaponDropdown.value : 0;
            if (selectedIndex < 0 || selectedIndex >= m_allWeaponSkills.Count)
            {
                return;
            }

            SkillData selectedSkill = m_allWeaponSkills[selectedIndex];

            if (m_gameManager.SpawnedPlayer.Weapons.Any(w => w.SkillCode == selectedSkill.skillCode))
            {
                Debug.LogWarning($"[TestManager] 이미 보유한 무기입니다: {selectedSkill.skillName}");
                return;
            }

            int startLevel = 1;
            if (m_startLevelInput != null && !string.IsNullOrEmpty(m_startLevelInput.text))
            {
                if (int.TryParse(m_startLevelInput.text, out int parsedLevel))
                {
                    startLevel = Mathf.Clamp(parsedLevel, 1, 8);
                }
            }

            bool startEvolved = m_startEvolvedToggle != null && m_startEvolvedToggle.isOn;

            EquipWeaponAsync(selectedSkill, startLevel, startEvolved).Forget();
        }

        private async UniTaskVoid EquipWeaponAsync(SkillData skill, int level, bool evolved)
        {
            await m_gameManager.EquipNewWeapon(skill, true, level, evolved);
            RefreshOwnedWeaponList();
        }

        private void OnLevelUpWeaponClicked(string skillCode)
        {
            if (m_gameManager.SpawnedPlayer == null)
            {
                return;
            }

            var weapon = m_gameManager.SpawnedPlayer.Weapons.FirstOrDefault(w => w.SkillCode == skillCode);
            if (weapon != null)
            {
                weapon.LevelUp();
                RefreshOwnedWeaponList();
            }
        }

        private void OnRemoveWeaponClicked(string skillCode)
        {
            if (m_gameManager == null)
            {
                return;
            }

            m_gameManager.RemoveWeaponForTest(skillCode);
            RefreshOwnedWeaponList();
        }

        private void OnClearStageClicked()
        {
            if (m_gameManager != null)
            {
                m_gameManager.ClearStageForTest();
                
                // 테스트 패널 닫기 (시각적 확인을 위해)
                TogglePanelAsync().Forget();
            }
        }

        #endregion
    }
}